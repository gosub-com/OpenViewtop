// Copyright (C) 2016 by Jeremy Spiller, all rights reserved.

using System;
using System.Text;
using System.Threading;
using System.Net;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;

namespace Gosub.Viewtop
{
    /// <summary>
    /// Serve static files from the given root path. 
    /// </summary>
    public class FileServer
    {
        /// <summary>
        /// Handle external requests, each in its own thread
        /// </summary>
        public delegate void RequestHandler(HttpListenerContext context);

        static readonly string sPrivateFileName = Path.DirectorySeparatorChar + ".";

        HttpListener mListener;
        string []mServerNames;
        string mRootPath;
        int mRequestCount;
        Dictionary<string, RequestHandler> mExternalRequestHandler = new Dictionary<string, RequestHandler>();


        // Server name would be something like "http://localhost:8080/".
        // The root path is the location of public files to be served. 
        // Any file that begins with a "." or is in a subdirectory
        // that begins with a "." is private and will not be served.
        // (e.g. the file named .passwords will never be served, nor
        // would the file www/.myprivatefiles/private.txt).
        public FileServer(string []serverNames, string rootPath)
        {
            mServerNames = serverNames;
            mRootPath = rootPath;
        }

        /// <summary>
        /// Handle requests with the given file extension specially.
        /// Each callback is in its own thread.
        /// </summary>
        public void SetRequestHandler(string fileExtension, RequestHandler handler)
        {
            mExternalRequestHandler["." + fileExtension.Replace(".", "").ToLower()] = handler;
        }

        public void Start()
        {
            Stop();
            mListener = new HttpListener();
            foreach (var serverName in mServerNames)
                mListener.Prefixes.Add(serverName);
            mListener.Start();

            ThreadPool.QueueUserWorkItem((o) =>
            {
                try
                {
                    while (mListener.IsListening)
                    {
                        var context = mListener.GetContext();
                        ThreadPool.QueueUserWorkItem((obj) =>
                        {
                            try
                            {
                                int requestNum = Interlocked.Increment(ref mRequestCount);
                                ProcessRequest(context);
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine("ERROR: " + ex.Message + ".  " + context.Request.Url);
                            }
                            finally
                            {
                                context.Response.OutputStream.Close();
                            }
                        });
                    }
                }
                catch { } // suppress any exceptions
            });
        }

        class RequestException : Exception
        {
            public RequestException(string message) : base(message)
            {
            }
        }

        public void Stop()
        {
            if (mListener != null)
            {
                mListener.Stop();
                mListener.Close();
                mListener = null;
            }
        }

        private void ProcessRequest(HttpListenerContext context)
        {
            var request = context.Request;

            // Convert path to Windows, strip leading "\", and choose "index" if no name is given
            string path = request.Url.LocalPath.Replace('/', Path.DirectorySeparatorChar);
            while (path.Length != 0 && path[0] == Path.DirectorySeparatorChar)
                path = path.Substring(1);
            if (path.Length == 0)
                path = "index.html";
            path = Path.Combine(mRootPath, path);

            // Never serve files outside of the public subdirectory, and
            // never serve private files (files that begin with a "." or
            // are in a subdirectory that begin with a ".")
            var response = context.Response;
            if (path.Contains("..") || path.Contains(sPrivateFileName))
            {
                SendError(response, "Bad Request", 400);
                return;
            }

            // Handle external file extensions
            RequestHandler handler;
            if (mExternalRequestHandler.TryGetValue(Path.GetExtension(path).ToLower(), out handler))
            {
                handler(context);
                return;
            }

            // Static files can only be a GET request
            if (request.HttpMethod != "GET")
                throw new RequestException("Invalid HTTP request: " + request.HttpMethod);

            // If this is a file in our local subdirectory, send it to the client
            if (File.Exists(path))
            {
                // Send local file back to client
                var stream = File.OpenRead(path);
                response.ContentLength64 = stream.Length;
                stream.CopyTo(response.OutputStream);
                stream.Close();
                return;
            }

            SendError(response, "File not found", 404);
            return;
        }

        public static void SendError(HttpListenerResponse response, string message, int statusCode)
        {
            SendResponse(response, "<html><body>ERROR: " + message + "</body><html>", statusCode);
        }

        public static void SendResponse(HttpListenerResponse response, string message, int statusCode)
        {
            SendResponse(response, UTF8Encoding.UTF8.GetBytes(message), statusCode);
        }

        public static void SendResponse(HttpListenerResponse response, byte []message, int statusCode)
        {
            response.StatusCode = statusCode;
            response.ContentLength64 = message.Length;
            response.OutputStream.Write(message, 0, message.Length);
        }

        public static byte[] GetRequest(HttpListenerRequest request, int maxLength)
        {
            if (request.ContentLength64 > maxLength)
                throw new Exception("Error: Content length is too large");

            // NOTE: This needs to be fixed
            var buffer = new byte[(int)request.ContentLength64];
            if (request.InputStream.Read(buffer, 0, buffer.Length) != buffer.Length)
                throw new Exception("Error: Chunks not allowed yet");

            return buffer;
        }

    }

}
