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

namespace Gosub.Http
{
    /// <summary>
    /// RFC 7230 and 7231: HTTP Server
    /// </summary>
    public class HttpServer
    {
        public delegate void HttpHandler(HttpStream HttpStream);

        object mLock = new object();
        TcpListener mListener;
        HttpHandler mHttpHandler;

        public HttpServer()
        {
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
                        ThreadPool.QueueUserWorkItem((obj2) => { TryProcessRequest(client); });
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
        void TryProcessRequest(TcpClient client)
        {
            try
            {
                using (client)
                    ProcessRequest(client);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Server request failed: " + ex.Message);
            }
        }

        void ProcessRequest(TcpClient client)
        {
            var request = new HttpStream();
            if (request.ParseHeader(client))
            {
                if (mHttpHandler != null)
                {
                    mHttpHandler(request);
                }
                else
                {
                    request.SendResponse("HTTP request handler not installed");
                }
            }
        }
    }
}
