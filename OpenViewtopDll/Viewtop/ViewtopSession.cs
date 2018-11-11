// Copyright (C) 2016 by Jeremy Spiller, all rights reserved.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Diagnostics;
using System.Collections.Specialized;
using System.IO;
using System.IO.Compression;
using Gosub.Http;
using System.Threading.Tasks;

namespace Gosub.Viewtop
{
    class ViewtopSession
    {
        const int REMOTE_COMMUNICATIONS_TIMEOUT_MS = 8000;
        const int WAIT_FOR_SCREEN_CHANGE_TIMEOUT_MS = 2000;
        const int MAX_FILE_COUNT = 100;

        public long SessionId { get; }
        public bool SessionClosed => mSessionClosed;
        object mLockScreenCopy = new object();
        object mLockZip = new object();
        bool mSessionClosed;
        long mLastScreenHash;

        double mScreenScale = 1;
        Queue<FrameCollector> mCollectors = new Queue<FrameCollector>(new FrameCollector[] { new FrameCollector(), new FrameCollector() });
        Queue<CollectRequest> mCollectRequests = new Queue<CollectRequest>();

        FrameCompressor mAnalyzer = new FrameCompressor();
        MouseAndKeyboard mEvents = new MouseAndKeyboard();
        Clip mClip = new Clip();
        ClipInfo mClipInfo = new ClipInfo();
        MemoryStream mWebsocketRequestStream = new MemoryStream();
        Options mOptions = new Options();

        class RemoteEvents
        {
            public RemoteEvent[] Events { get; set; }
            public RemoteDrawRequest DrawRequest { get; set; }
            public Options Options;
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
        }

        class Options
        {
            public int Width = 1;
            public int Height = 1;
            public FrameCompressor.CompressionType Compression = FrameCompressor.CompressionType.SmartPng;
            public FrameCompressor.OutputType Output = FrameCompressor.OutputType.Normal;
            public bool FullThrottle = false;
            public bool FullFrame = false;
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
            public RemoteDrawRequest DrawRequest;
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
        public async Task ProcessOpenViewtopRequestAsync(HttpContext context)
        {
            if (context.Request.Path == "/ovt/clip")
            {
                if (!mClip.EverChanged)
                    throw new HttpException(400, "Not allowed to access clipboard until data is copied", true);
                await SendClipDataAsync(context);
                return;
            }
            throw new HttpException(400, "Unknown url: '" + context.Request.Path + "'", true);
        }

        public async Task ProcessOpenViewtopWebSocketsAsync(WebSocket websocket)
        {
            try
            {
                await TryProcessOpenViewtopWebsocketsAsync(websocket);
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

        async Task TryProcessOpenViewtopWebsocketsAsync(WebSocket websocket)
        {
            // Main loop
            Task<WebSocketMessageType> receiveTask = null;
            while (websocket.State < WebSocketState.CloseSent)
            {
                // Start read request if needed
                if (receiveTask == null)
                    receiveTask = websocket.ReceiveAsync(mWebsocketRequestStream, CancellationToken.None);

                // Await something to do (message from remote, or a completed screen capture)
                var timeout = Task.Delay(REMOTE_COMMUNICATIONS_TIMEOUT_MS);
                if (mCollectRequests.Count != 0)
                    await Task.WhenAny(new Task[] { timeout, mCollectRequests.Peek().Task, receiveTask });
                else
                    await Task.WhenAny(new Task[] { timeout, receiveTask });

                // Fail if too long without any communications from client
                if (timeout.IsCompleted)
                    break;

                // Send frames that may have completed in the background
                while (mCollectRequests.Count != 0 && mCollectRequests.Peek().Task.IsCompleted)
                    await DequeueCollectRequestAndSendFrame(websocket);

                // Process remote events
                if (receiveTask.IsCompleted)
                {
                    if (await receiveTask == WebSocketMessageType.Close)
                        break; // Closed by remote
                    receiveTask = null;

                    var request = ViewtopServer.ReadJson<RemoteEvents>(mWebsocketRequestStream);

                    if (request.Options != null)
                    {
                        mOptions = request.Options;
                        mLastScreenHash = 0; // Ask CopyScreen blocking to end early
                    }

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
            CollectRequest collectRequest = new CollectRequest();
            collectRequest.Collector = collector;
            collectRequest.DrawRequest = request.DrawRequest;

            // Copy the screen in a blocking background thread
            collectRequest.Task = Task.Run(() =>
            {
                CopyScreen(request, collector, collectRequest);
            });
            mCollectRequests.Enqueue(collectRequest);
        }

        private async Task DequeueCollectRequestAndSendFrame(WebSocket websocket)
        {
            var collectRequest = mCollectRequests.Dequeue();
            await collectRequest.Task;

            mScreenScale = collectRequest.Collector.Scale;
            mCollectors.Enqueue(collectRequest.Collector);
            var frame = new FrameInfo();
            GetFrame(collectRequest.Collector, frame);
            GetClipInfo(frame);

            frame.Seq = collectRequest.DrawRequest.Seq; // TBD: Remove?
            await ViewtopServer.WriteJson(websocket, frame);
        }

        /// <summary>
        /// Copy the screen, block for up to 2 seconds if nothing changes.
        /// </summary>
        void CopyScreen(RemoteEvents request, FrameCollector collector, CollectRequest collectRequest)
        {
            if (mOptions.FullThrottle)
            {
                // Need the lock to wait for previous throttle (if any) to end
                mLastScreenHash = 0; // Ask CopyScreen blocking to end early
                lock (mLockScreenCopy)
                    mLastScreenHash = 0;

                collector.CopyScreen();
            }
            else
            {
                // Block until screen changes or timeout
                // NOTE: Holding this lock (even without the hash function 
                //       in the while loop) incurs a time penalty.  This  
                //       code is the big bottleneck causing a slow frame
                //       rate.  And most of the time is spent in CopyScreen.
                lock (mLockScreenCopy)
                {
                    long hash = 0;
                    var screenCopyTimeout = DateTime.Now;
                    while ((DateTime.Now - screenCopyTimeout).TotalMilliseconds < WAIT_FOR_SCREEN_CHANGE_TIMEOUT_MS)
                    {
                        collector.CopyScreen();
                        using (var bm = new Bitmap32Bits(collector.Screen, System.Drawing.Imaging.ImageLockMode.ReadOnly))
                            hash = bm.HashLong(0, 0, bm.Width, bm.Height);
                        if (hash != mLastScreenHash)
                            break;
                        Thread.Sleep(20);
                    }
                    mLastScreenHash = hash;
                }
            }
            collector.ScaleScreen(mOptions.Width, mOptions.Height);
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

        void GetFrame(FrameCollector collector, FrameInfo frame)
        {
            // Set analyzer options
            mAnalyzer.FullFrame = mOptions.FullFrame;
            mAnalyzer.Compression = mOptions.Compression;
            mAnalyzer.Output = mOptions.Output;

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
