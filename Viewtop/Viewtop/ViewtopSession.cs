// Copyright (C) 2016 by Jeremy Spiller, all rights reserved.

using System;
using System.Collections.Generic;
using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading;
using System.Diagnostics;
using System.Collections.Specialized;
using System.Text;
using System.IO;
using System.IO.Compression;
using Gosub.Http;
using System.Threading.Tasks;

namespace Gosub.Viewtop
{
    class ViewtopSession
    {
        // Debug - Add latency to test link
        public static int sSimulatedLatencyMs = 0;
        public static int sSimulatedJitterMs = 0;
        static Random sRandom = new Random();

        const int FUTURE_FRAME_TIMEOUT_SEC = 5; // Allow up to 5 seconds before cancelling a request
        const int HISTORY_FRAMES = 2; // Save old frames for repeated requests
        const int MAX_FILE_COUNT = 100;

        public long SessionId { get; }
        public DateTime LastRequestTime { get; set; }
        string mChallenge { get; set; } = "";

        object mLock = new object();
        object mLockZip = new object();
        bool mAuthenticated;
        long mSequence;
        Dictionary<long, FrameInfo> mHistory = new Dictionary<long, FrameInfo>();
        FrameCollector mCollector;
        FrameCompressor mAnalyzer;
        MouseAndKeyboard mEvents = new MouseAndKeyboard();
        Clip mClip = new Clip();
        ClipInfo mClipInfo = new ClipInfo();

        JsonSerializer mJsonSerializer = new JsonSerializer();
        MemoryStream mWebsocketRequestStream = new MemoryStream();
        MemoryStream mWebsocketResponseStream = new MemoryStream();
        Encoding mUtf8 = new UTF8Encoding(false); // No byte order mark

        class RemoteEvents
        {
            public RemoteEvent[] Events { get; set; }
            public RemoteDrawRequest DrawRequest { get; set; }
        }

        class RemoteEvent
        {
            public string Event { get; set; } = "";
            public long Time { get; set; }

            // Mouse
            public int Which { get; set; }
            public int Delta;
            public int X;
            public int Y;

            // Keyboard
            public int KeyCode;
            public bool KeyShift;
            public bool KeyCtrl;
            public bool KeyAlt;
        }

        class RemoteDrawRequest
        {
            public long Seq;
            public int MaxWidth;
            public int MaxHeight;
            public string Options = "";
        }

        class LoginInfo
        {
            public string Event = "";
            public string Message = "";

            public string Username = "";
            public string PasswordHash = "";
            public bool LoggedIn;
            public string ComputerName = "(unknown)";
        }

        class FrameInfo
        {
            public string Event = "Draw";
            public long Seq;
            public Stats Stats;
            public List<FrameCompressor.Frame> Frames = new List<FrameCompressor.Frame>();
            public ClipInfo Clip;
        }
        
        class ClipInfo
        {
            public bool Changed = false;
            public string Type = "";
            public string FileName = "";
            public string FileCount = "0";
        }

        class Stats
        {
            public string Size { get; set; }
            public int CopyTime { get; set; }
            public int ShrinkTime { get; set; }
            public int CompressTime { get; set; }
            public int Duplicates { get; set; }
            public int Collisions { get; set; }
        }

        public ViewtopSession(long sessionId)
        {
            SessionId = sessionId;
        }

        /// <summary>
        /// Handle a web remote view request (each request is in its own thread)
        /// </summary>
        public async Task ProcessWebRemoteViewerRequestAsync(HttpContext context)
        {
            LastRequestTime = DateTime.Now;
            var request = context.Request;
            var response = context.Response;
            var queryString = request.Query;

            var query =  queryString.Get("query");

            // Websockets!
            if (context.Request.IsWebSocketRequest)
            {
                await HandleWebSocketRequest(context);
                return;
            }

            if (query == "startsession")
            {
                // Send session id, password salt, and challenge
                await context.SendResponseAsync(GetChallenge(request.Query.Get("username")), 200);
                return;
            }

            if (query == "login")
            {
                // Authenticate user, sets mAuthenticated to true if they got in
                await context.SendResponseAsync(JsonConvert.SerializeObject(Authenticate(request.Query.Get("username"), request.Query.Get("hash"))), 200);
                return;
            }

            // --- Everything below this requires authentication ---
            if (!mAuthenticated)
            {
                await ViewtopServer.SendJsonErrorAsync(context, "User must be logged in");
                return;
            }

            // Simulate latency and jitter
            if (sSimulatedLatencyMs != 0 || sSimulatedJitterMs != 0)
            {
                int latencyMs = sRandom.Next(sSimulatedJitterMs) + sSimulatedLatencyMs;
                if (latencyMs != 0)
                    Thread.Sleep(latencyMs);
            }

            if (query == "clip")
            {
                if (!mClip.EverChanged)
                {
                    await ViewtopServer.SendJsonErrorAsync(context, "Not allowed to access clipboard until data is copied");
                    return;
                }
                await SendClipDataAsync(context);
                return;
            }

            // --- Everything below this requires a sequence number
            if (!long.TryParse(queryString.Get("seq"), out long sequence))
            {
                await ViewtopServer.SendJsonErrorAsync(context, "Query must have a sequence number called 'seq', and must it must be numeric");
                return;
            }

            if (query == "draw")
            {
                UpdateMousePositionFromDrawQuery(request);
                if (WaitForImageOrTimeoutXhr(sequence, out FrameInfo frame, context.Request.Query))
                    await context.SendResponseAsync(JsonConvert.SerializeObject(frame), 200);
                else
                    await ViewtopServer.SendJsonErrorAsync(context, "Error retrieving draw frame " + sequence);
                return;
            }

            if (query == "events")
            {
                int maxLength = 64000;
                string json = mUtf8.GetString(await context.ReadContentAsync(maxLength));
                ProcessRemoteEvents(JsonConvert.DeserializeObject<RemoteEvents>(json).Events);
                return;
            }
            await ViewtopServer.SendJsonErrorAsync(context, "ERROR: Invalid query name - " + query);
        }

        async Task HandleWebSocketRequest(HttpContext context)
        {
            var websocket = await context.AcceptWebSocketAsync("viewtop");

            // Read login (startsession was called via XHR, and now we expect username and password)
            await websocket.ReceiveAsync(mWebsocketRequestStream, CancellationToken.None);
            var login = ReadJson<LoginInfo>(mWebsocketRequestStream);

            // Authenticate user and send response, sets mAuthenticated to true if they got in
            var authenticateResponse = Authenticate(login.Username, login.PasswordHash);
            if (!mAuthenticated)
            {
                // End session if not authenticated
                await websocket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Invalid user name or password", CancellationToken.None);
                return;
            }
            await WriteJson(websocket, authenticateResponse);

            // Main loop
            while (websocket.State < WebSocketState.CloseSent)
            {                
                // Read request
                await websocket.ReceiveAsync(mWebsocketRequestStream, CancellationToken.None);
                var request = ReadJson<RemoteEvents>(mWebsocketRequestStream);

                ProcessRemoteEvents(request.Events);

                var draw = request.DrawRequest;
                if (draw != null)
                {
                    // Parse options query string TBD: Change this to JSON
                    var queryStrings = HttpContext.ParseQueryString(
                        draw.Options 
                        + "&maxwidth=" + draw.MaxWidth 
                        + "&maxheight=" + draw.MaxHeight);

                    var frame = new FrameInfo();
                    GetFrame(frame, queryStrings);
                    GetClipInfo(frame);
                    frame.Seq = draw.Seq; // TBD: Remove, but websocket JS needs this for now

                    await WriteJson(websocket, frame);
                }
            }
        }

        /// <summary>
        /// Read JSON from a stream
        /// </summary>
        T ReadJson<T>(Stream s)
        {
            using (var sr = new StreamReader(s, mUtf8, true, 1024, true))
            using (var jr = new JsonTextReader(sr))
                return mJsonSerializer.Deserialize<T>(jr);
        }

        /// <summary>
        /// Save JSON to a web socket stream, using mWebsocketResponse as a buffer
        /// </summary>
        async Task WriteJson(WebSocket websocket, object obj)
        {
            mWebsocketResponseStream.Position = 0;
            mWebsocketResponseStream.SetLength(0);
            using (var sw = new StreamWriter(mWebsocketResponseStream, mUtf8, 1024, true))
            using (var jw = new JsonTextWriter(sw))
                mJsonSerializer.Serialize(jw, obj);
            await websocket.SendAsync(new ArraySegment<byte>(mWebsocketResponseStream.GetBuffer(), 0, (int)mWebsocketResponseStream.Length), WebSocketMessageType.Text, true, CancellationToken.None);
        }


        private string GetChallenge(string userName)
        {
            mChallenge = Util.GenerateSalt();
            var user = UserFile.Load().Find(userName);
            string salt = user != null ? user.Salt : Util.GenerateSalt();
            string challenge = @"{""sid"": " + SessionId
                + @",""challenge"":""" + mChallenge
                + @""",""salt"":""" + salt + @"""}";
            return challenge;
        }

        private LoginInfo Authenticate(string username, string passwordHash)
        {
            if (username != null && passwordHash != null)
            {
                var user = UserFile.Load().Find(username);
                if (user != null)
                    mAuthenticated = user.VerifyPassword(mChallenge, passwordHash);
            }
            // Generate response
            var loginInfo = new LoginInfo();            
            loginInfo.LoggedIn = mAuthenticated;
            if (mAuthenticated)
            {
                try { loginInfo.ComputerName = Dns.GetHostName(); }
                catch { }
            }
            else
            {
                loginInfo.Event = "Close";
                loginInfo.Message = "Invalid user name or password";
            }
            return loginInfo;
        }

        void UpdateMousePositionFromDrawQuery(Http.HttpRequest request)
        {
            var queryString = request.Query;
            if (long.TryParse(queryString.Get("t"), out long time)
                && int.TryParse(queryString.Get("x"), out int x)
                && int.TryParse(queryString.Get("y"), out int y)
                && mCollector != null 
                && mCollector.Scale != 0)
            {
                mEvents.SetMousePosition(time, 1 / mCollector.Scale, x, y, false);
            }
        }

        private void ProcessRemoteEvents(RemoteEvent []events)
        {
            if (events == null)
                return;

            foreach (var e in events)
            {
                // Send mouse position, force it to move if there is a click event
                bool force = e.Event == "mousedown" || e.Event == "mouseup" || e.Event == "mousewheel";
                if (e.Event.StartsWith("mouse") && mCollector != null && mCollector.Scale != 0)
                    mEvents.SetMousePosition(e.Time, 1 / mCollector.Scale, e.X, e.Y, force);

                switch (e.Event)
                {
                    case "mousedown":
                        mEvents.MouseButton(MouseAndKeyboard.Action.Down, e.Which);
                        break;
                    case "mouseup":
                        mEvents.MouseButton(MouseAndKeyboard.Action.Up, e.Which);
                        break;
                    case "mousewheel":
                        mEvents.MouseWheel(e.Delta);
                        break;
                    case "keydown":
                        mEvents.KeyPress(MouseAndKeyboard.Action.Down, e.KeyCode, e.KeyShift, e.KeyCtrl, e.KeyAlt);
                        break;
                    case "keyup":
                        mEvents.KeyPress(MouseAndKeyboard.Action.Up, e.KeyCode, e.KeyShift, e.KeyCtrl, e.KeyAlt);
                        break;
                }
            }
        }



        /// <summary>
        /// Retrieve the requested frame via xhr (i.e. order frames and use cache)
        /// </summary>
        bool WaitForImageOrTimeoutXhr(long sequence, out FrameInfo frame, HttpQuery queryString)
        {
            // If a future frame is receivied, wait for it or timeout.
            DateTime now = DateTime.Now;
            while (true)
            {
                lock (mLock)
                {
                    // Return old frames from history
                    if (mHistory.TryGetValue(sequence, out frame))
                    {
                        return true;
                    }
                    // Break to allow the current frame to be processed
                    if (sequence <= mSequence + 1)
                        break;
                }
                // Fail if the frame isn't ready within the long poll timeout
                if ((DateTime.Now - now).TotalSeconds > FUTURE_FRAME_TIMEOUT_SEC)
                    return false;
                Thread.Sleep(10);
            }

            // Collect a new frame.  Future frames and image frames are queued above
            lock (mLock)
            {
                // Generate an error for very old frames
                if (sequence <= mSequence)
                {
                    // TBD: How much history do we need (depends on Javascript queueing and latency)?
                    Debug.Assert(false);
                    return false;
                }
                // We received a request for the next frame, which we don't have yet.
                // NOTE: Old requests were either satisified from history or timed out.
                //       Future requests were queued above.  The only way to get here 
                //       is when requesting the next frame
                Debug.Assert(sequence == mSequence + 1);
                mSequence++;

                // Generate the frame
                frame = new FrameInfo();
                GetFrame(frame, queryString);
                GetClipInfo(frame);

                // Save the frame in history and delete old frames
                mHistory[sequence] = frame;
                mHistory.Remove(sequence - HISTORY_FRAMES);
                mHistory.Remove(sequence - HISTORY_FRAMES - 1);
                Debug.Assert(mHistory.Count <= HISTORY_FRAMES);
                return true;
            }
        }

        void GetFrame(FrameInfo frame, HttpQuery queryString)
        {
            if (mCollector == null)
                mCollector = new FrameCollector();
            if (mAnalyzer == null)
                mAnalyzer = new FrameCompressor();

            // Process MaxWidth and MaxHeight
            int.TryParse(queryString.Get("maxwidth"), out int maxWidth);
            int.TryParse(queryString.Get("maxheight"), out int maxHeight);

            // Full frame analysis
            mAnalyzer.FullFrame = queryString.Get("fullframe") != "";

            // Process compression type
            string compressionType = queryString.Get("compression").ToLower();
            if (compressionType == "png")
                mAnalyzer.Compression = FrameCompressor.CompressionType.Png;
            else if (compressionType == "jpg")
                mAnalyzer.Compression = FrameCompressor.CompressionType.Jpg;
            else
                mAnalyzer.Compression = FrameCompressor.CompressionType.SmartPng;

            // Process output type
            string outputType = queryString.Get("output").ToLower();
            if (outputType == "fullframejpg")
                mAnalyzer.Output = FrameCompressor.OutputType.FullFrameJpg;
            else if (outputType == "fullframepng")
                mAnalyzer.Output = FrameCompressor.OutputType.FullFramePng;
            else if (outputType == "compressionmap")
                mAnalyzer.Output = FrameCompressor.OutputType.CompressionMap;
            else if (outputType == "hidejpg")
                mAnalyzer.Output = FrameCompressor.OutputType.HideJpg;
            else if (outputType == "hidepng")
                mAnalyzer.Output = FrameCompressor.OutputType.HidePng;
            else
                mAnalyzer.Output = FrameCompressor.OutputType.Normal;

            // Collect the frame
            var bm = mCollector.CreateFrame(maxWidth, maxHeight);
            var compressStartTime = DateTime.Now;
            var frames = mAnalyzer.Compress(bm);
            Debug.Assert(frames.Length != 0);
            bm.Dispose();
            var compressTime = DateTime.Now - compressStartTime;

            // Generate stats
            Stats stats = new Stats();
            stats.CopyTime = (int)mCollector.CopyTime.TotalMilliseconds;
            stats.ShrinkTime = (int)mCollector.ShrinkTime.TotalMilliseconds;
            stats.CompressTime = (int)compressTime.TotalMilliseconds;
            stats.Duplicates = mAnalyzer.Duplicates;
            stats.Collisions = mAnalyzer.HashCollisionsEver;
            int size = 0;
            foreach (var compressorFrame in frames)
                size += compressorFrame.Draw.Length + compressorFrame.Image.Length;
            stats.Size = "" + size / 1000 + "Kb";

            // Generate the frame buffer
            frame.Stats = stats;
            foreach (var compressorFrame in frames)
                frame.Frames.Add(compressorFrame);
        }

        private void GetClipInfo(FrameInfo frame)
        {
            // Get current clipboard info
            frame.Clip = mClipInfo;
            mClipInfo.Changed = mClip.Changed;
            if (!mClipInfo.Changed)
                return;

            // Clipboard changed, get new info
            mClip.Changed = false;
            if (mClip.ContainsText())
            {
                mClipInfo.Type = "Text";
                mClipInfo.FileName = "";
                mClipInfo.FileCount = "0";
            }
            else if (mClip.ContainsFiles())
            {
                mClipInfo.Type = "File";

                // File count
                var files = mClip.GetFiles();
                var fileCount = CountFiles(files, 0, MAX_FILE_COUNT);
                mClipInfo.FileCount = fileCount.ToString();
                if (fileCount >= MAX_FILE_COUNT)
                    mClipInfo.FileCount += "+";

                // File name
                if (files.Length == 0 || fileCount == 0)
                {
                    mClipInfo.Type = "";
                    mClipInfo.FileName = "";
                }
                else if (files.Length == 1)
                {
                    // Use file name, or zip file name if directory 
                    var attr = File.GetAttributes(files[0]);
                    if (!attr.HasFlag(FileAttributes.Directory))
                        mClipInfo.FileName = Path.GetFileName(files[0]);
                    else
                        mClipInfo.FileName = Path.GetFileNameWithoutExtension(files[0]) + ".zip";
                }
                else
                {
                    // Use zip file name of the directory the files are in
                    mClipInfo.FileName = Path.GetFileNameWithoutExtension(Path.GetDirectoryName(files[0])) + ".zip";
                }
            }
            else
            {
                mClipInfo.Type = "";
                mClipInfo.FileName = "Unknown.dat";
                mClipInfo.FileCount = "0";
            }
        }

        int CountFiles(string[] files, int count, int max)
        {
            foreach (var file in files)
                if (count < max)
                    count = CountFiles(file, count, max);
            return count;
        }

        int CountFiles(string file, int count, int max)
        {
            var attr = File.GetAttributes(file);
            if (attr.HasFlag(FileAttributes.System))
                return count;

            // Process a file
            if (!attr.HasFlag(FileAttributes.Directory))
                return count + 1;

            // Process a directory, first the sub-files then sub-directories
            count = CountFiles(Directory.GetFiles(file), count, max);
            if (count < max)
                count = CountFiles(Directory.GetDirectories(file), count, max);
            return Math.Min(count, max);
        }

        /// <summary>
        /// Send the clipboard files, multiple files get zipped
        /// </summary>
        async Task SendClipDataAsync(HttpContext context)
        {
            if (mClip.ContainsText())
            {
                await context.SendResponseAsync(mClip.GetText());
                return;
            }
            string[] files = mClip.GetFiles();
            if (files.Length == 0)
            {
                context.Response.StatusCode = 400;
                await context.SendResponseAsync(new byte[0]);
                return;
            }
            if (files.Length == 1 && !File.GetAttributes(files[0]).HasFlag(FileAttributes.Directory))
            {
                await context.SendFileAsync(files[0]);
                return;
            }
            // Create a ZIP file
            var tempFile = Path.GetTempFileName();
            try
            {
                // Lock here to prevent the browser from being aggressive
                // and trying to download multiple copies at the same time.
                // TBD: Implement caching system so repeated requests get same file.
                // TBD: Should this be run on the thread pool?
                lock (mLockZip)
                {
                    using (var tempStream = File.OpenWrite(tempFile))
                    using (var archive = new ZipArchive(tempStream, ZipArchiveMode.Create))
                        foreach (var file in files)
                            WriteZip(archive, file, Path.GetDirectoryName(file));
                }

                await context.SendFileAsync(tempFile);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        void WriteZip(ZipArchive archive, string[] files, string basePath)
        {
            foreach (var file in files)
                WriteZip(archive, file, basePath);
        }

        void WriteZip(ZipArchive archive, string file, string basePath)
        {
            var attr = File.GetAttributes(file);
            if (attr.HasFlag(FileAttributes.System))
                return;

            // Process a file
            if (!attr.HasFlag(FileAttributes.Directory))
            {
                // Remove base path name and leading \
                var entryName = file.Replace(basePath, "");
                while (entryName.StartsWith("\\"))
                    entryName = entryName.Substring(1);
                archive.CreateEntryFromFile(file, entryName);
                return;
            }
            // Process a directory, first the sub-files then sub-directories
            WriteZip(archive, Directory.GetFiles(file), basePath);
            WriteZip(archive, Directory.GetDirectories(file), basePath);
        }
        
    }
}
