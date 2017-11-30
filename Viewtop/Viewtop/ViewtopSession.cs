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
using System.Drawing;

namespace Gosub.Viewtop
{
    class ViewtopSession
    {
        const int MAX_FILE_COUNT = 100;

        public long SessionId { get; }
        public bool SessionClosed => mSessionClosed;
        string mChallenge { get; set; } = "";
        object mLock = new object();
        object mLockZip = new object();
        bool mAuthenticated;
        bool mSessionClosed;

        double mScreenScale = 1;
        Queue<FrameCollector> mCollectors = new Queue<FrameCollector>(new FrameCollector[] { new FrameCollector(), new FrameCollector() });
        Queue<CollectRequest> mCollectRequests = new Queue<CollectRequest>();

        FrameCompressor mAnalyzer = new FrameCompressor();
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

        /// <summary>
        /// Copying the screen is slow, so collect requests are double buffered and processed in a background task
        /// </summary>
        struct CollectRequest
        {
            public FrameCollector Collector;
            public long Seq;
            public string DrawOptions;
            public Task Task;
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
            var request = context.Request;
            var response = context.Response;
            var queryString = request.Query;

            var query =  queryString.Get("query");
            if (query == "startsession")
            {
                // Send session id, password salt, and challenge
                await context.SendResponseAsync(GetChallenge(request.Query.Get("username")), 200);
                return;
            }

            if (context.Request.IsWebSocketRequest)
            {
                await HandleWebSocketRequest(context.AcceptWebSocket("viewtop"));
                return;
            }

            // --- Everything below this requires authentication ---
            if (!mAuthenticated)
                throw new HttpException(400, "User must be logged in", true);

            if (query == "clip")
            {
                if (!mClip.EverChanged)
                    throw new HttpException(400, "Not allowed to access clipboard until data is copied", true);
                await SendClipDataAsync(context);
                return;
            }
            throw new HttpException(400, "ERROR: Invalid query name - " + query, true);
        }

        public async Task HandleWebSocketRequest(WebSocket websocket)
        {
            try
            {
                await TryHandleWebSocketRequest(websocket);
                if (websocket.State == WebSocketState.Open || websocket.State == WebSocketState.CloseReceived)
                    await websocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "End of session", CancellationToken.None);
            }
            catch (Exception ex)
            {
                if (websocket.State == WebSocketState.Open || websocket.State == WebSocketState.CloseReceived)
                    await websocket.CloseAsync(WebSocketCloseStatus.InternalServerError, ex.Message, CancellationToken.None);
                throw;
            }
            finally
            {
                // Close session
                mAuthenticated = false;
                mSessionClosed = true;
                while (mCollectors.Count != 0)
                    mCollectors.Dequeue().Dispose();
                while (mCollectRequests.Count != 0)
                {
                    var request = mCollectRequests.Dequeue();
                    await request.Task;
                    request.Collector.Dispose();
                }
            }
        }

        async Task TryHandleWebSocketRequest(WebSocket websocket)
        {
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
            Task<WebSocketMessageType> receiveTask = null;
            while (websocket.State < WebSocketState.CloseSent)
            {
                // Start read request if needed
                if (receiveTask == null)
                    receiveTask = websocket.ReceiveAsync(mWebsocketRequestStream, CancellationToken.None);

                // Await something to do (message from remote, or a completed screen capture)
                if (mCollectRequests.Count != 0)
                    await Task.WhenAny(new Task[] { mCollectRequests.Peek().Task, receiveTask });
                else
                    await receiveTask;

                // Send frames that may have completed in the background
                while (mCollectRequests.Count != 0 && mCollectRequests.Peek().Task.IsCompleted)
                    await DequeueCollectRequestAndSendFrame(websocket);

                // Process remote events
                if (receiveTask.IsCompleted)
                {
                    if (await receiveTask == WebSocketMessageType.Close)
                        break; // Closed by remote
                    receiveTask = null;

                    var request = ReadJson<RemoteEvents>(mWebsocketRequestStream);
                    ProcessRemoteEvents(request.Events);
                    if (request.DrawRequest != null)
                        await EnqueueCollectRequestAsync(websocket, request);
                }
            }
        }

        private async Task EnqueueCollectRequestAsync(WebSocket websocket, RemoteEvents request)
        {
            // Ensure there is a collector.  Wait for one if necessary
            if (mCollectors.Count == 0)
                await DequeueCollectRequestAndSendFrame(websocket);

            var collector = mCollectors.Dequeue();
            CollectRequest collectRequest;
            collectRequest.Collector = collector;
            collectRequest.Seq = request.DrawRequest.Seq;
            collectRequest.DrawOptions = request.DrawRequest.Options;
            collectRequest.Task = Task.Run(() =>
            {
                collector.CopyScreen(request.DrawRequest.MaxWidth, request.DrawRequest.MaxHeight);
            });
            mCollectRequests.Enqueue(collectRequest);
        }

        private async Task DequeueCollectRequestAndSendFrame(WebSocket websocket)
        {
            var drawRequest = mCollectRequests.Dequeue();
            await drawRequest.Task;

            mScreenScale = drawRequest.Collector.Scale;
            mCollectors.Enqueue(drawRequest.Collector);
            var frame = new FrameInfo();
            GetFrame(drawRequest.Collector, HttpContext.ParseQueryString(drawRequest.DrawOptions), frame);
            GetClipInfo(frame);

            frame.Seq = drawRequest.Seq; // TBD: Remove?
            await WriteJson(websocket, frame);
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

        private void ProcessRemoteEvents(RemoteEvent []events)
        {
            if (events == null)
                return;

            foreach (var e in events)
            {
                // Send mouse position, force it to move if there is a click event
                bool force = e.Event == "mousedown" || e.Event == "mouseup" || e.Event == "mousewheel";
                if (e.Event.StartsWith("mouse"))
                    mEvents.SetMousePosition(e.Time, 1 / mScreenScale, e.X, e.Y, force);

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


        void GetFrame(FrameCollector collector, HttpQuery queryString, FrameInfo frame)
        {
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

            // Compress the frame
            var compressStartTime = DateTime.Now;
            var frames = mAnalyzer.Compress(collector.Screen);
            Debug.Assert(frames.Length != 0);
            var compressTime = DateTime.Now - compressStartTime;

            // Generate stats
            Stats stats = new Stats();
            stats.CopyTime = (int)collector.CopyTime.TotalMilliseconds;
            stats.ShrinkTime = (int)collector.ShrinkTime.TotalMilliseconds;
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
