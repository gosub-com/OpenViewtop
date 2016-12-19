// Copyright (C) 2016 by Jeremy Spiller, all rights reserved.

using System;
using System.Collections.Generic;
using System.Net;
using Newtonsoft.Json;
using System.Threading;
using System.Diagnostics;
using System.Collections.Specialized;
using System.Windows.Forms;
using System.Drawing;
using System.Text;

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

        public long SessionId { get; }
        public DateTime LastRequestTime { get; set; }
        string mChallenge { get; set; } = "";

        object mLock = new object();
        bool mAuthenticated;
        long mSequence;
        Dictionary<long, FrameInfo> mHistory = new Dictionary<long, FrameInfo>();
        FrameCollector mCollector;
        FrameCompressor mAnalyzer;
        Events mEvents = new Events();

        class MouseOrKeyEvent
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
            public int KeyChar;
            public bool KeyShift;
            public bool KeyCtrl;
            public bool KeyAlt;
        }

        class RemoteEvents
        {
            public List<MouseOrKeyEvent> Events { get; set; } = new List<MouseOrKeyEvent>();
        }

        class FrameInfo
        {
            // NOTE: Do not change names - they are converted to JSON and sent to client
            public Stats Stats;
            public List<FrameCompressor.Frame> Frames = new List<FrameCompressor.Frame>();
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
        public void ProcessWebRemoteViewerRequest(HttpListenerContext context)
        {
            LastRequestTime = DateTime.Now;
            var request = context.Request;
            var response = context.Response;
            string query = request.QueryString["query"];

            if (query == "startsession")
            {
                // Send session id, password salt, and challenge
                mChallenge = Util.GenerateSalt();
                var user = UserFile.Load().Find(request.QueryString["username"]);
                string salt = user != null ? user.Salt : Util.GenerateSalt();
                FileServer.SendResponse(response,
                    @"{""sid"": " + SessionId
                    + @",""challenge"":""" + mChallenge
                    + @""",""salt"":""" + salt + @"""}", 200);
                return;
            }

            if (query == "login")
            {
                // Authenticate user
                string username = request.QueryString["username"];
                var passwordHash = request.QueryString["hash"];
                if (username != null && passwordHash != null)
                {
                    var user = UserFile.Load().Find(username);
                    if (user != null)
                        mAuthenticated = user.VerifyPassword(mChallenge, passwordHash);
                }
                FileServer.SendResponse(response, @"{""pass"": " + mAuthenticated.ToString().ToLower() + "}", 200);
                return;
            }

            // --- Everything below this requires authentication ---
            if (!mAuthenticated)
            {
                FileServer.SendError(response, "User must log in", 401);
                return;
            }

            // --- Everything below this requires a sequence number
            string sequenceStr = request.QueryString["seq"];
            long sequence;
            if (sequenceStr == null || !long.TryParse(sequenceStr, out sequence))
            {
                FileServer.SendError(response, "Query 'seq' must be numeric", 400);
                return;
            }

            // Simulate latency and jitter
            if (sSimulatedLatencyMs != 0 || sSimulatedJitterMs != 0)
            {
                int latencyMs = sRandom.Next(sSimulatedJitterMs) + sSimulatedLatencyMs;
                if (latencyMs != 0)
                    Thread.Sleep(latencyMs);
            }

            if (query == "draw")
            {
                UpdateMousePositionFromDrawQuery(request);
                FrameInfo frame;
                if (WaitForImageOrTimeout(sequence, out frame, request.QueryString))
                    FileServer.SendResponse(response, JsonConvert.SerializeObject(frame), 200);
                else
                    FileServer.SendError(response, "Error retrieving draw frame " + sequence, 400);
                return;
            }

            if (query == "events")
            {
                int maxLength = 64000;
                byte[] buffer = FileServer.GetRequest(request, maxLength);
                string json = UTF8Encoding.UTF8.GetString(buffer);
                var events = JsonConvert.DeserializeObject<RemoteEvents>(json);

                foreach (var e in events.Events)
                {
                    switch (e.Event)
                    {
                        case "mousemove":
                            if (mCollector != null && mCollector.Scale != 0)
                                mEvents.SetMousePosition(e.Time, 1/mCollector.Scale, e.X, e.Y, true);
                            break;
                        case "mousedown":
                        case "mouseup":
                            if (mCollector != null && mCollector.Scale != 0)
                                mEvents.SetMousePosition(e.Time, 1 / mCollector.Scale, e.X, e.Y, true);
                            mEvents.MouseButton(e.Event == "mousedown" ? Events.Action.Down : Events.Action.Up, e.Which);
                            break;
                        case "mousewheel":
                            if (mCollector != null && mCollector.Scale != 0)
                                mEvents.SetMousePosition(e.Time, 1 / mCollector.Scale, e.X, e.Y, true);
                            mEvents.MouseWheel(e.Delta);
                            break;
                        case "keypress":
                            mEvents.KeyPress(e.KeyCode, e.KeyChar, e.KeyShift, e.KeyCtrl, e.KeyAlt);
                            break;

                    }
                }
                FileServer.SendResponse(response, "", 200);
                return;
            }
            FileServer.SendError(response, "ERROR: Invalid query type", 400);
        }

        void UpdateMousePositionFromDrawQuery(HttpListenerRequest request)
        {
            long time;
            int x;
            int y;
            string ts = request.QueryString["t"];
            string xs = request.QueryString["x"];
            string ys = request.QueryString["y"];
            if (ts == null || !long.TryParse(ts, out time)
                || xs == null || !int.TryParse(xs, out x)
                || ys == null || !int.TryParse(ys, out y)
                || mCollector == null || mCollector.Scale == 0)
                return;
            mEvents.SetMousePosition(time, 1/mCollector.Scale, x, y, false);
        }

        /// <summary>
        /// Retrieve the requested frame
        /// </summary>
        bool WaitForImageOrTimeout(long sequence, out FrameInfo frame, NameValueCollection drawOptions)
        {
            // If a future frame is receivied, wait for it or timeout.
            // NOTE: Javascript doesn't queue future frames, but it will eventually
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
                    if (drawOptions != null && sequence <= mSequence + 1)
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
                // Frames are blocked above until we have the data
                Debug.Assert(drawOptions != null);

                // Generate an error for very old frames
                if (sequence <= mSequence)
                {
                    // TBD: How much history do we need (depends on Javascript queueing and latency)?
                    Debug.Assert(false); 
                    return false;
                }

                // Clean out history
                mHistory.Remove(sequence - HISTORY_FRAMES);
                mHistory.Remove(sequence - HISTORY_FRAMES-1);
                Debug.Assert(mHistory.Count <= HISTORY_FRAMES);

                // We received a request for the next frame, which we don't have yet.
                // NOTE: Old requests were either satisified from history or timed out.
                //       Future requests were queued above.  The only way to get here 
                //       is when requesting the next frame
                Debug.Assert(sequence == mSequence + 1);

                if (mCollector == null)
                    mCollector = new FrameCollector();
                if (mAnalyzer == null)
                    mAnalyzer = new FrameCompressor();

                // Process MaxWidth and MaxHeight
                int maxWidth = 0;
                int maxHeight = 0;
                int.TryParse(drawOptions["MaxWidth"], out maxWidth);
                int.TryParse(drawOptions["MaxHeight"], out maxHeight);

                // Full frame analysis
                mAnalyzer.FullFrame = drawOptions["fullframe"] != null;

                // Process compression type
                string compressionType = drawOptions["compression"];
                compressionType = compressionType == null ? "" : compressionType.ToLower();
                if (compressionType == "png")
                    mAnalyzer.Compression = FrameCompressor.CompressionType.Png;
                else if (compressionType == "jpg")
                    mAnalyzer.Compression = FrameCompressor.CompressionType.Jpg;
                else
                    mAnalyzer.Compression = FrameCompressor.CompressionType.SmartPng;

                // Process output type
                string outputType = drawOptions["output"];
                outputType = outputType == null ? "" : outputType.ToLower();
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
                stats.Size = "" + size/1000 + "Kb";

                // Generate the frame buffer
                frame = new FrameInfo();
                frame.Stats = stats;
                foreach (var compressorFrame in frames)
                    frame.Frames.Add(compressorFrame);
                mHistory[sequence] = frame;
                mSequence++;
                return true;
            }
        }


    }
}
