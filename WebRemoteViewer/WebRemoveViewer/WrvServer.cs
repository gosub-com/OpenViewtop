// Copyright (C) 2016 by Jeremy Spiller, all rights reserved.

using System;
using System.Text;
using System.Net;
using System.IO;
using System.Collections.Generic;

namespace Gosub.WebRemoteViewer
{
    /// <summary>
    /// Handle web remote view requests (each request in its own thread)
    /// </summary>
    class WrvServer
    {
        const int CONNECTION_TIMEOUT_SEC = 30;
        object mLock = new object();

        int mSessionId = 1;
        Dictionary<int, WrvSession> mSessions = new Dictionary<int, WrvSession>();

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
                WrvSession newSession;
                lock (mLock)
                {
                    newSession = new WrvSession(mSessionId);
                    newSession.LastRequestTime = DateTime.Now;
                    mSessions[mSessionId++] = newSession;
                }
                FileServer.SendResponse(response, @"{""sid"": " + newSession.SessionId + "}", 200);
                return;
            }

            string sidStr = request.QueryString["sid"];
            int sid;
            if (sidStr == null || !int.TryParse(sidStr, out sid))
            {
                FileServer.SendError(response, "Query must include 'sid'", 400);
                return;
            }
            WrvSession session;
            lock (mLock)
            {
                if (!mSessions.TryGetValue(sid, out session))
                {
                    FileServer.SendError(response, "Unknown 'sid'", 400);
                    return;
                }
                session.LastRequestTime = DateTime.Now;
            }

            session.ProcessWebRemoteViewerRequest(context);
        }

        private void PurgeInactiveSessions()
        {
            lock (mLock)
            {
                var now = DateTime.Now;
                var timedOutSessions = new List<int>();
                foreach (var sessionKv in mSessions)
                    if ((now - sessionKv.Value.LastRequestTime).TotalSeconds > CONNECTION_TIMEOUT_SEC)
                        timedOutSessions.Add(sessionKv.Key);
                foreach (var sessionId in timedOutSessions)
                    mSessions.Remove(sessionId);
            }
        }
    }
}
