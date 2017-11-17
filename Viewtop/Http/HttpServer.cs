﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Diagnostics;
using System.IO;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace Gosub.Http
{
    /// <summary>
    /// RFC 7230 and 7231: HTTP Server
    /// </summary>
    public class HttpServer
    {
        public delegate Task HttpHandler(HttpContext HttpStream);

        object mLock = new object();
        TcpListener mListener;
        HttpHandler mHttpHandler;
        X509Certificate mCertificate;

        CancellationToken mCancellationToken = CancellationToken.None; // TBD: Implement cancellation token
        int mConcurrentRequests;
        int mMaxConcurrentRequests;

        public HttpServer()
        {
        }

        public int HeaderTimeout { get; set; } = 10000;
        public int BodyTimeout { get; set; } = 60000;

        /// <summary>
        /// When true, the server uses a synchrounous threading model.  
        /// This decreases latency at the expense of needing more CPU.
        /// </summary>
        public bool Sync { get; set; }

        /// <summary>
        /// Call this with a certificate to use SSL, or with NULL to disable SSL
        /// </summary>
        public void UseSsl(X509Certificate certificate)
        {
            mCertificate = certificate;
        }

        /// <summary>
        /// Start server in background thread.  
        /// </summary>
        public void Start(TcpListener listener, HttpHandler httpHandler)
        {
            mHttpHandler = httpHandler;
            mListener = listener;
            mListener.Start();

            // Process incoming connections in background thread 
            // since we don't want it running on the GUI thread
            Task.Run( async () =>
            {
                try
                {
                    while (true)
                    {
                        var client = await mListener.AcceptTcpClientAsync();
                        ThreadPool.QueueUserWorkItem(delegate { TryProcessRequestsAsync(client); });
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("HttpServer exception: " + ex.Message);
                }
            });
        }

        public void Stop()
        {
            mListener.Stop();
        }

        async void TryProcessRequestsAsync(TcpClient client)
        {
            int concurrentRequests = Interlocked.Add(ref mConcurrentRequests, 1);
            Thread.VolatileWrite(ref mMaxConcurrentRequests, Math.Max(concurrentRequests, Thread.VolatileRead(ref mMaxConcurrentRequests)));            

            using (client)
            {
                HttpContext context = null;
                try
                {
                    // Setup stream or SSL stream
                    client.NoDelay = true;
                    var tcpStream = (Stream)client.GetStream();
                    bool isSecure = false;
                    if (mCertificate != null)
                    {
                        // Wrap stream in an SSL stream, and authenticate
                        var sslStream = new SslStream(tcpStream, false);
                        await sslStream.AuthenticateAsServerAsync(mCertificate);
                        tcpStream = sslStream;
                        isSecure = true;
                    }
                    // Process requests on this possibly persistent TCP stream
                    var reader = new HttpReader(tcpStream, mCancellationToken, Sync);
                    var writer = new HttpWriter(tcpStream, mCancellationToken, Sync);
                    context = new HttpContext(client, reader, writer, isSecure);
                    await ProcessRequests(context, reader, writer).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    try
                    {
                        await ProcessExceptionAsync(context, ex);
                    }
                    catch (Exception doubleFaultEx)
                    {
                        Debug.WriteLine("DOUBLE FAULT EXCEPTION while processing exception: " + doubleFaultEx.Message);
                    }
                }
            }
            Interlocked.Add(ref mConcurrentRequests, -1);
        }

        /// <summary>
        /// Process requests as long as there is not an error
        /// </summary>
        async Task ProcessRequests(HttpContext context, HttpReader reader, HttpWriter writer)
        {
            int persistentConnections = 0;
            do
            {
                persistentConnections++;

                // Read header
                reader.ReadTimeout = HeaderTimeout;
                if (!await context.ReadHttpHeaderAsync().ConfigureAwait(false))
                    return; // Connection closed

                // Handle body
                reader.ReadTimeout = HeaderTimeout;
                writer.WriteTimeout = HeaderTimeout;
                try
                {
                    // Process request
                    if (mHttpHandler == null)
                        throw new HttpException(503, "HTTP request handler not installed");
                    await mHttpHandler(context).ConfigureAwait(false);
                }
                catch (HttpException ex) when (ex.KeepConnectionOpen && !context.Response.HeaderSent)
                {
                    // Keep the persistent connection open only if it's OK to do so.
                    await ProcessExceptionAsync(context, ex);
                }
                await writer.FlushHeaderInternal();

                // Any of these problems will terminate a persistent connection
                var response = context.Response;
                var request = context.Request;
                var websocket = request.IsWebSocketRequest;
                if (!response.HeaderSent)
                    throw new HttpException(500, "Request handler did not send a response for: " + context.Request.TargetFull);
                if (!websocket && reader.Position != request.ContentLength && request.ContentLength >= 0)
                    throw new HttpException(500, "Request handler did not read correct number of bytes: " + request.TargetFull);
                if (!websocket && writer.Position != response.ContentLength)
                    throw new HttpException(500, "Request handler did not write correct number of bytes: " + request.TargetFull);
            } while (!context.Request.IsWebSocketRequest && context.Response.KeepAlive);
        }

        // Try to send an error message back to the client
        async Task ProcessExceptionAsync(HttpContext context, Exception ex)
        {
            // Unwrap aggregate exceptions.  Async likes to throw these.
            var aggEx = ex as AggregateException;
            if (aggEx != null && aggEx.InnerException != null)
            {
                // Unwrap exception
                Debug.WriteLine("SERVER ERROR: Aggregate with " + aggEx.InnerExceptions.Count + " inner exceptions");
                ex = ex.InnerException;
            }

            var code = 500;
            var message = "Server error";
            var httpEx = ex as HttpException;
            if (httpEx != null && httpEx.Code/100 != 5 )
            {
                // Allow message to client
                code = httpEx.Code;
                message = httpEx.Message;
                Debug.WriteLine("CLIENT EXCEPTION " + httpEx.Code + ": " + ex.Message);
            }
            else if (httpEx != null)
            {
                // Allow code to client, but not message
                code = httpEx.Code;
                message = "Server error";
                Debug.WriteLine("SERVER HTTP EXCEPTION " + httpEx.Code + ": " + ex.Message);
            }
            else
            {
                code = 500;
                message = "Server error";
                Debug.WriteLine("SERVER EXCEPTION " + ex.GetType() + ": " + ex.Message);
            }

            // Send response to client if it looks OK to do so
            if (context != null && !context.Request.IsWebSocketRequest && !context.Response.HeaderSent)
            {
                context.Response.StatusCode = code;
                context.Response.StatusMessage = message;
                await context.SendResponseAsync(ex.Message);
            }
        }
    }
}
