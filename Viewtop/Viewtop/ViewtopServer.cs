// Copyright (C) 2016 by Jeremy Spiller, all rights reserved.

using System;
using System.Text;
using System.Net;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;

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
            try
            {
                TryProcessWebRemoteViewerRequest(context);
            }
            catch (Exception ex)
            {
                SendJsonError(context.Response, "Error processing viewtop request: " + ex.Message);
                Debug.WriteLine("Error processing viewtop request: " + ex.Message);
            }
        }

        /// <summary>
        /// Error messages from the viewtop server are in JSON with code 200
        /// </summary>
        public static void SendJsonError(HttpListenerResponse response, string message)
        {
            FileServer.SendResponse(response, @"{""FAIL"":""" +
                message.Replace("\"", "\\\"").Replace("\\", "\\\\")
                + @"""}", 400);
        }

        /// <summary>
        /// Handle a web remote view request (each request is in its own thread)
        /// </summary>
        void TryProcessWebRemoteViewerRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            string query = request.QueryString["query"];
            if (query == null)
            {
                SendJsonError(response, "Query must include 'query' parameter");
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
                SendJsonError(response, "Query must include 'sid'");
                return;
            }
            ViewtopSession session;
            lock (mLock)
            {
                if (!mSessions.TryGetValue(sid, out session))
                {
                    SendJsonError(response, "Unknown 'sid'");
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
