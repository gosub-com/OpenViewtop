// Copyright (C) 2016 by Jeremy Spiller, all rights reserved.

using System;
using System.Text;
using System.Net;
using System.IO;
using System.Collections.Generic;

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

        /// <summary>
        /// Handle a web remote view request (each request is in its own thread)
        /// </summary>
        public void ProcessWebRemoteViewerRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            string query = request.QueryString["query"];
            if (query == null)
            {
                FileServer.SendError(response, "Query must include 'query' parameter", 400);
                return;
            }
            if (query == "info")
            {
                FileServer.SendResponse(response, "{JSON GOES HERE - Describe screens}", 200);
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
                newSession.ProcessWebRemoteViewerRequest(context);
                return;
            }

            string sidStr = request.QueryString["sid"];
            long sid;
            if (sidStr == null || !long.TryParse(sidStr, out sid))
            {
                FileServer.SendError(response, "Query must include 'sid'", 400);
                return;
            }
            ViewtopSession session;
            lock (mLock)
            {
                if (!mSessions.TryGetValue(sid, out session))
                {
                    FileServer.SendError(response, "Unknown 'sid'", 400);
                    return;
                }
            }
            session.ProcessWebRemoteViewerRequest(context);
        }

        private void PurgeInactiveSessions()
        {
            lock (mLock)
            {
                var now = DateTime.Now;
                var timedOutSessions = new List<long>();
                foreach (var sessionKv in mSessions)
                    if ((now - sessionKv.Value.LastRequestTime).TotalSeconds > CONNECTION_TIMEOUT_SEC)
                        timedOutSessions.Add(sessionKv.Key);
                foreach (var sessionId in timedOutSessions)
                    mSessions.Remove(sessionId);
            }
        }
    }
}
