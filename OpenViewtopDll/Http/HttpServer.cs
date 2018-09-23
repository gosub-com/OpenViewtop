using System;
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
        public delegate Task HttpHandlerDelegate(HttpContext context);

        object mLock = new object();
        HashSet<TcpListener> mListeners = new HashSet<TcpListener>();
        CancellationToken mCancellationToken = CancellationToken.None; // TBD: Implement cancellation token
        int mConcurrentRequests;
        int mMaxConcurrentRequests;

        public HttpServer()
        {
        }

        public event HttpHandlerDelegate HttpHandler;
        public int HeaderTimeout { get; set; } = 10000;
        public int BodyTimeout { get; set; } = 60000;

        /// <summary>
        /// When true, the server uses a synchrounous threading model.  
        /// This decreases latency at the expense of needing more CPU.
        /// </summary>
        public bool Sync { get; set; }

        /// <summary>
        /// Start server on this listner in a background thread
        /// </summary>
        public void Start(TcpListener listener)
        {
            Start(listener, null);
        }

        /// <summary>
        /// Start ssl server on this listener in background thread
        /// </summary>
        public void Start(TcpListener listener, X509Certificate certificate)
        {
            listener.Start();
            lock (mLock)
                mListeners.Add(listener);

            // Process incoming connections in background thread 
            // since we don't want it running on the GUI thread
            Task.Run( async () =>
            {
                try
                {
                    while (true)
                    {
                        var client = await listener.AcceptTcpClientAsync();
                        ThreadPool.QueueUserWorkItem(delegate { TryProcessRequestsAsync(client, certificate); });
                    }
                }
                catch (Exception ex)
                {
                    Log.Write("HttpServer exception", ex);
                }
                lock (mLock)
                    mListeners.Remove(listener);
                try { listener.Stop(); }
                catch { }
            });
        }

        public void Stop()
        {
            lock (mLock)
            {
                foreach (var listener in mListeners)
                    try { listener.Stop(); }
                    catch { }
                mListeners.Clear();
            }
        }

        async void TryProcessRequestsAsync(TcpClient client, X509Certificate certificate)
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
                    if (certificate != null)
                    {
                        // Wrap stream in an SSL stream, and authenticate
                        var sslStream = new SslStream(tcpStream, false);
                        await sslStream.AuthenticateAsServerAsync(certificate);
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
                        Log.Write("DOUBLE FAULT EXCEPTION", doubleFaultEx);
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
                try
                {
                    // Read header
                    reader.ReadTimeout = HeaderTimeout;
                    var buffer = await reader.ReadHttpHeaderAsyncInternal();
                    reader.PositionInternal = 0;
                    reader.LengthInternal = HttpContext.HTTP_HEADER_MAX_SIZE;
                    if (buffer.Count == 0)
                        return;  // Connection closed

                    // Parse header
                    context.ResetRequestInternal(HttpRequest.Parse(buffer), new HttpResponse());
                }
                catch (Exception ex)
                {
                    Log.Write("HTTP header exception: " + ex.GetType() + " - " + ex.Message);
                    return; // Close connection
                }

                // Handle body
                reader.ReadTimeout = BodyTimeout;
                writer.WriteTimeout = BodyTimeout;
                try
                {
                    // Process HTTP request
                    var handler = HttpHandler;
                    if (handler == null)
                        throw new HttpException(503, "HTTP request handler not installed");
                    await handler(context).ConfigureAwait(false);
                }
                catch (HttpException ex) when (ex.KeepConnectionOpen && !context.Response.HeaderSent)
                {
                    // Keep the persistent connection open only if it's OK to do so.
                    await ProcessExceptionAsync(context, ex);
                }
                await writer.FlushAsync();

                // Any of these problems will terminate a persistent connection
                var response = context.Response;
                var request = context.Request;
                var isWebsocket = request.IsWebSocketRequest;
                if (!response.HeaderSent)
                    throw new HttpException(500, "Request handler did not send a response for: " + context.Request.Path);
                if (!isWebsocket && reader.Position != request.ContentLength && request.ContentLength >= 0)
                    throw new HttpException(500, "Request handler did not read correct number of bytes: " + request.Path);
                if (!isWebsocket && writer.Position != response.ContentLength)
                    throw new HttpException(500, "Request handler did not write correct number of bytes: " + request.Path);
            } while (!context.Request.IsWebSocketRequest && context.Response.Connection == "keep-alive");
        }

        // Try to send an error message back to the client
        async Task ProcessExceptionAsync(HttpContext context, Exception ex)
        {
            // Unwrap aggregate exceptions.  Async likes to throw these.
            var aggEx = ex as AggregateException;
            if (aggEx != null && aggEx.InnerException != null)
            {
                // Unwrap exception
                Log.Write("SERVER ERROR Aggregate with " + aggEx.InnerExceptions.Count + " inner exceptions");
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
                Log.Write("HTTP CLIENT EXCEPTION " + httpEx.Code, ex);
            }
            else if (httpEx != null)
            {
                // Allow code to client, but not message
                code = httpEx.Code;
                message = "Server error";
                Log.Write("HTTP SERVER EXCEPTION " + httpEx.Code, ex);
            }
            else
            {
                code = 500;
                message = "Server error";
                Log.Write("SERVER EXCEPTION", ex);
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
