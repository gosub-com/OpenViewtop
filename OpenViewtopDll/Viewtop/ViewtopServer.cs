// Copyright (C) 2016 by Jeremy Spiller, all rights reserved.

using System;
using System.Text;
using System.Net;
using System.IO;
using System.Collections.Generic;
using Gosub.Http;
using System.Threading.Tasks;
using System.Threading;
using Newtonsoft.Json;

namespace Gosub.Viewtop
{
    /// <summary>
    /// Handle Viewtop requests (each request in its own thread)
    /// </summary>
    public class ViewtopServer
    {
        const int CONNECTION_TIMEOUT_SEC = 30;

        static Encoding sUtf8 = new UTF8Encoding(false); // No byte order mark
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

        List<string> mLocalIpAddresses = new List<string>()
        {
            "10.",
            "192.168.",
            "172.16.", "172.17.", "172.18.", "172.19.", "172.20.", "172.21.", "172.22.", "172.23.",
            "172.24.", "172.25.", "172.26.", "172.27.", "172.28.", "172.29.", "172.30.", "172.31.",
        };

        // JSON serialized data
        public class ComputerInfo
        {
            public string ComputerName = "";
            public string Name = "";
            public string LocalIp = "";
            public string[] LocalIps = new string[0];
            public string PublicIp = "";
            public string HttpsPort = "";
            public string HttpPort = "";
            public string Status = "";
        }

        public ComputerInfo LocalComputerInfo { get; set; } = new ComputerInfo();
        public ComputerInfo[] RemoteComputerInfo { get; set; } = new ComputerInfo[0];

        // REST URL: /ovt/info
        class Info
        {
            // Serialized and set to browser
            public ComputerInfo Local;
            public ComputerInfo[] Remotes;
        }
        // JSON serialized login data
        class LoginInfo
        {
            public string Event = "";
            public bool LoggedIn;
            public string Message = "";
            public string ComputerName = "(unknown)";
        }
        // JSON serialized username
        class UsernameInfo
        {
            public string Event = "";
            public string Username = "";
        }
        // JSON serialized challenge data
        class ChallengeInfo
        {
            public string Event = "";
            public long Sid;
            public string Challenge = "";
            public string Salt = "";
        }
        // JSON serialized challenge response data
        class ChallengeResponseInfo
        {
            public string Event = "";
            public string Username = "";
            public string PasswordHash = "";
        }

        /// <summary>
        /// Handle an Open Viewtop HTTP or Websocket request
        /// </summary>
        async public Task ProcessOpenViewtopRequestAsync(HttpContext context)
        {
            // Process web sockets to allow login and authentication
            var request = context.Request;
            if (request.Path == "/ovt/ws" && context.Request.IsWebSocketRequest)
            {
                PurgeInactiveSessions();
                await ProcessOpenViewtopWebSocketsAsync(context.AcceptWebSocket("viewtop"));
                return;
            }
            if (request.Path == "/ovt/info")
            {
                // Do not send when IP address is on the public internet.  TBD: Allow 172.17, etc.
                var ip = ((IPEndPoint)context.RemoteEndPoint).Address.ToString();
                bool localIp = mLocalIpAddresses.FindIndex((a) => ip.StartsWith(a)) >= 0;

                var info = new Info();
                info.Local = LocalComputerInfo;
                info.Remotes = localIp ? RemoteComputerInfo : new ComputerInfo[0];
                await context.SendResponseAsync(JsonConvert.SerializeObject(info));
                return;
            }
            // Requests with a valid SID (session id) get routed to the session
            long sid = request.Query["sid", 0];
            if (sid != 0)
            {
                ViewtopSession session;
                lock (mLock)
                    mSessions.TryGetValue(sid, out session);
                if (session == null)
                    throw new HttpException(400, "Unknown 'sid'");
                await session.ProcessOpenViewtopRequestAsync(context);
                return;
            }

            // *** Serve public static files ***

            // Convert path to Windows, strip leading "\", and choose "index" if no name is given
            string path = request.Path.Replace('/', Path.DirectorySeparatorChar);
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
            if (request.Method != "GET")
                throw new HttpException(405, "Invalid HTTP request: Only GET method is allowed for serving ");

            if (mMimeTypes.TryGetValue(Path.GetExtension(path).ToLower(), out string contentType))
                context.Response.ContentType = contentType;
            await context.SendFileAsync(path);
        }

        async Task ProcessOpenViewtopWebSocketsAsync(WebSocket websocket)
        {
            // Read username
            var ms = new MemoryStream();
            await websocket.ReceiveAsync(ms, CancellationToken.None);
            var login = ReadJson<UsernameInfo>(ms);
            if (login.Event != "Username")
                throw new HttpException(400, "Expecting 'Username' event");

            // Send challenge
            var challenge = new ChallengeInfo();
            challenge.Event = "Challenge";
            challenge.Sid = Util.GenerateRandomId();
            challenge.Challenge = Util.GenerateSalt();
            var user = UserFile.Load().Find(login.Username == null ? "" : login.Username);
            challenge.Salt = user != null ? user.Salt : Util.GenerateSalt();
            await WriteJson(websocket, challenge);

            // Read challenge response
            await websocket.ReceiveAsync(ms, CancellationToken.None);
            var challengeResponse = ReadJson<ChallengeResponseInfo>(ms);
            if (challengeResponse.Event != "ChallengeResponse")
                throw new HttpException(400, "Expecting 'ChallengeResponse' event");

            // Authenticate user and send response, sets mAuthenticated to true if they got in
            var authenticateResponse = Authenticate(challengeResponse.Username, challenge.Challenge, challengeResponse.PasswordHash);
            if (!authenticateResponse.LoggedIn)
            {
                // End session if not authenticated
                await WriteJson(websocket, authenticateResponse);
                await websocket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Invalid user name or password", CancellationToken.None);
                return;
            }
            // Create session, send response
            var newSession = new ViewtopSession(challenge.Sid);
            lock (mLock)
                mSessions[challenge.Sid] = newSession;
            await WriteJson(websocket, authenticateResponse);

            // Process the rest of the stream in its own session
            await newSession.ProcessOpenViewtopWebSocketsAsync(websocket);
        }

        private LoginInfo Authenticate(string username, string challenge, string passwordHash)
        {
            var login = new LoginInfo();
            if (username != null && passwordHash != null)
            {
                var user = UserFile.Load().Find(username);
                if (user != null && user.VerifyPassword(challenge, passwordHash))
                {
                    login.Event = "LoggedIn";
                    login.LoggedIn = true;
                    try { login.ComputerName = Dns.GetHostName(); }
                    catch { }
                    return login;
                }
            }
            login.Event = "Close";
            login.Message = "Invalid user name or password";
            return login;
        }

        /// <summary>
        /// Read JSON from a stream
        /// </summary>
        public static T ReadJson<T>(Stream s)
        {
            var jsonSerializer = new JsonSerializer();
            using (var sr = new StreamReader(s, sUtf8, true, 1024, true))
            using (var jr = new JsonTextReader(sr))
                return jsonSerializer.Deserialize<T>(jr);
        }

        /// <summary>
        /// Save JSON to a web socket stream
        /// </summary>
        public static async Task WriteJson(WebSocket websocket, object obj)
        {
            var jsonSerializer = new JsonSerializer();
            var websocketStream = new MemoryStream();
            websocketStream.Position = 0;
            websocketStream.SetLength(0);
            using (var sw = new StreamWriter(websocketStream, sUtf8, 1024, true))
            using (var jw = new JsonTextWriter(sw))
                jsonSerializer.Serialize(jw, obj);
            await websocket.SendAsync(new ArraySegment<byte>(websocketStream.GetBuffer(), 0, (int)websocketStream.Length), WebSocketMessageType.Text, true, CancellationToken.None);
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

    }
}
