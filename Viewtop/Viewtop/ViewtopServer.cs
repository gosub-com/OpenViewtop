// Copyright (C) 2016 by Jeremy Spiller, all rights reserved.

using System;
using System.Text;
using System.Net;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using Gosub.Http;
using System.Threading.Tasks;


namespace Gosub.Viewtop
{
    /// <summary>
    /// Handle Viewtop requests (each request in its own thread)
    /// </summary>
    class ViewtopServer
    {
        const int CONNECTION_TIMEOUT_SEC = 30;
        object mLock = new object();

        Dictionary<long, ViewtopSession> mSessions = new Dictionary<long, ViewtopSession>();

        Dictionary<string, string> mMimeTypes = new Dictionary<string, string>()
        {
            {".htm", "text/html" },
            {".html", "text/html" },
            {".jpg", "image/jpeg" },
            {".jpeg", "image/jpeg" },
            {".png", "image/png" },
            {".gif", "image/gif" },
            {".css", "text/css" },
            {".js", "application/javascript" }
        };

        /// <summary>
        /// Handle a web remote view request (each request is in its own thread)
        /// </summary>
        async public Task ProcessWebRemoteViewerRequest(HttpContext context)
        {
            var request = context.Request;
            var response = context.Response;

            // Convert path to Windows, strip leading "\", and choose "index" if no name is given
            string path = request.Target.Replace('/', Path.DirectorySeparatorChar);
            while (path.Length != 0 && path[0] == Path.DirectorySeparatorChar)
                path = path.Substring(1);
            if (path.Length == 0)
                path = "index.html";

            string publicSubdirectory = Path.Combine(Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath), "www");
            string hiddenFileName = Path.DirectorySeparatorChar + ".";

            // Never serve files outside of the public subdirectory, or that begin with a "."
            path = Path.Combine(publicSubdirectory, path);
            if (path.Contains("..") || path.Contains(hiddenFileName))
                throw new HttpException(400, "Invalid Request: File name is invalid", true);

            // Serve static pages unless it's /openviewtop.ovt
            if (request.Target != "/openviewtop.ovt")
            {
                // Static files can only be a GET request
                if (request.HttpMethod != "GET")
                    throw new HttpException(405, "Invalid HTTP request: Only GET method is allowed for serving ");

                if (mMimeTypes.TryGetValue(Path.GetExtension(path).ToLower(), out string contentType))
                    response.ContentType = contentType;
                await context.SendFileAsync(path);
                return;
            }

            if (!request.Query.TryGetValue("query", out string query))
            {
                await SendJsonErrorAsync(context, "Query must include 'query' parameter");
                return;
            }

            if (query == "startsession")
            {
                PurgeInactiveSessions();

                // Create a new session with a random session ID
                ViewtopSession newSession;
                lock (mLock)
                {
                    long sessionId;
                    do { sessionId = Util.GenerateRandomId(); } while (mSessions.ContainsKey(sessionId));
                    newSession = new ViewtopSession(sessionId);
                    mSessions[sessionId] = newSession;
                }
                await newSession.ProcessWebRemoteViewerRequestAsync(context);
                return;
            }

            if (!long.TryParse(request.Query.Get("sid"), out long sid))
            {
                await SendJsonErrorAsync(context, "Query must include 'sid'");
                return;
            }
            ViewtopSession session;
            lock (mLock)
                mSessions.TryGetValue(sid, out session);
            if (session == null)
            {
                await SendJsonErrorAsync(context, "Unknown 'sid'");
                return;
            }

            await session.ProcessWebRemoteViewerRequestAsync(context);
        }

        private void PurgeInactiveSessions()
        {
            lock (mLock)
            {
                var now = DateTime.Now;
                var timedOutSessions = new List<long>();
                foreach (var sessionKv in mSessions)
                    if (sessionKv.Value.SessionClosed)
                        timedOutSessions.Add(sessionKv.Key);
                foreach (var sessionId in timedOutSessions)
                    mSessions.Remove(sessionId);
            }
        }

        /// <summary>
        /// Error messages from the viewtop server are in JSON with code 200
        /// </summary>
        public async static Task SendJsonErrorAsync(HttpContext context, string message)
        {
            await context.SendResponseAsync(@"{""FAIL"":""" +
                message.Replace("\"", "\\\"").Replace("\\", "\\\\")
                + @"""}", 400);
        }

    }
}
