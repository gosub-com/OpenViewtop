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
        public delegate void HttpHandler(HttpConext HttpStream);

        object mLock = new object();
        TcpListener mListener;
        HttpHandler mHttpHandler;
        X509Certificate mCertificate;

        public HttpServer()
        {
        }

        public int HeaderTimeout { get; set; } = 10000;
        public int BodyTimeout { get; set; } = 60000;

        /// <summary>
        /// Call this with a certificate to use SSL, or with NULL to disable SSL
        /// </summary>
        public void UseSsl(X509Certificate certificate)
        {
            mCertificate = certificate;
        }

        /// <summary>
        /// Start server in background thread.  The http handler is called in a background thread
        /// whenever a new request comes in.
        /// </summary>
        public void Start(TcpListener listener, HttpHandler httpHandler)
        {
            mHttpHandler = httpHandler;
            mListener = listener;
            mListener.Start();

            // Process incoming connections in background thread
            ThreadPool.QueueUserWorkItem((obj) =>
            {
                try
                {
                    while (true)
                    {
                        // Wait for request
                        var client = mListener.AcceptTcpClient();
                        ThreadPool.QueueUserWorkItem((obj2) => { TryProcessRequests(client); });
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

        // Each request comes in on a new thread
        void TryProcessRequests(TcpClient client)
        {
            using (client)
            {
                // Setup stream or SSL stream
                HttpConext context;
                HttpReader reader;
                HttpWriter writer;
                try
                {
                    var tcpStream = (Stream)client.GetStream();
                    if (mCertificate != null)
                    {
                        // Wrap stream in an SSL stream, and authenticate
                        var sslStream = new SslStream(tcpStream, false);
                        sslStream.AuthenticateAsServer(mCertificate);
                        tcpStream = sslStream;
                    }
                    reader = new HttpReader(tcpStream);
                    writer = new HttpWriter(tcpStream);
                    context = new HttpConext(reader, writer);

                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Error setting up context: " + ex.Message);
                    return;
                }

                try
                {
                    // Process multiple requests on this TCP stream
                    ProcessRequests(context, reader, writer);
                }
                catch (HttpException httpEx)
                {
                    try
                    {
                        ProcessException(context, httpEx);
                    }
                    catch (Exception doubleFaultEx)
                    {
                        Debug.WriteLine("ERROR: Failed while trying to send header: " + doubleFaultEx.Message);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Server request failed: " + ex.Message);
                }
            }
        }

        private void ProcessRequests(HttpConext context, HttpReader reader, HttpWriter writer)
        {
            do
            {
                // Read header
                reader.ReadTimeout = HeaderTimeout;
                if (!context.ReadHeader())
                    return; // EOF

                // Handle body
                reader.ReadTimeout = HeaderTimeout;
                writer.WriteTimeout = HeaderTimeout;
                try
                {
                    // Process request
                    if (mHttpHandler == null)
                        throw new HttpException(503, "HTTP request handler not installed");
                    mHttpHandler(context);
                }
                catch (HttpException ex) when (!ex.TerminateConnection && !context.Response.HeaderSent)
                {
                    // Send the error back to the client, but keep the TCP connection open.
                    // An exception here bubbles up to close the connection.
                    ProcessException(context, ex);
                }

                // Any of these problems will terminate a persistent connection
                if (!context.Response.HeaderSent)
                    throw new HttpException(500, "Request handler did not send a response for: " + context.Request.TargetFull, true);
                if (reader.Position != context.Request.ContentLength && context.Request.ContentLength >= 0)
                    throw new HttpException(500, "Request handler did not read correct number of bytes: " + context.Request.TargetFull, true);
                if (writer.Position != context.Response.ContentLength)
                    throw new HttpException(500, "Request handler did not write correct number of bytes: " + context.Request.TargetFull, true);

            } while (context.Request.KeepAlive && context.Response.KeepAlive);
        }

        // Try to send the error message back to the client
        void ProcessException(HttpConext context, HttpException ex)
        {
            string message = ex.Message;
            if (ex.Code / 100 == 4)
            {
                Debug.WriteLine("CLIENT ERROR " + ex.Code + ": " + ex.Message);
            }
            else
            {
                // Mask the error text
                Debug.WriteLine("SERVER ERROR " + ex.Code + ": " + ex.Message);
                message = "ERROR";
            }

            if (!context.Response.HeaderSent && !ex.TerminateConnection)
            {
                context.Response.StatusCode = ex.Code;
                context.Response.StatusMessage = message;
                context.SendResponse(ex.Message);
            }
        }
    }
}
