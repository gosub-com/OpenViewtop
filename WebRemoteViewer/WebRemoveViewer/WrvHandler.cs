// Copyright (C) 2016 by Jeremy Spiller, all rights reserved.

using System.Text;
using System.Net;
using System.IO;

namespace Gosub.WebRemoteViewer
{
    /// <summary>
    /// Handle web remote view requests (each request in its own thread)
    /// </summary>
    class WrvHandler
    {
        Stream mImage;
        string mDraw = "";
        object mLock = new object();

        public void SetDraw(Stream image, string draw)
        {
            lock (mLock)
            {
                mImage = image;
                mDraw = draw;
            }
        }

        /// <summary>
        /// Handle a web remote view request (each request is in its own thread)
        /// </summary>
        public void ProcessWebRemoteViewerRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            string type = request.QueryString["type"];
            string seq = request.QueryString["seq"];
            Stream image = mImage;

            string errorString = null;
            long sequence;
            if (type == null || type != "image" && type != "draw")
                errorString = "Query 'type' must be 'image' or 'draw'";
            else if (seq == null || !long.TryParse(seq, out sequence))
                errorString = "Query 'seq' must be numeric";
            else if (image == null)
                errorString = "Server doesn't have an image";

            if (errorString != null)
            {
                FileServer.RespondWithErrorMessage(response, errorString, 400);
                return;
            }

            if (type == "image")
            {
                lock (mLock)
                {
                    image.Position = 0;
                    image.CopyTo(response.OutputStream);
                    return;
                }
            }
            if (type == "draw")
            {
                byte[] messageBytes = UTF8Encoding.UTF8.GetBytes(mDraw);
                response.ContentLength64 = messageBytes.Length;
                response.OutputStream.Write(messageBytes, 0, messageBytes.Length);
                return;
            }
            FileServer.RespondWithErrorMessage(response, "ERROR: Invalid query type", 400);
        }

    }
}
