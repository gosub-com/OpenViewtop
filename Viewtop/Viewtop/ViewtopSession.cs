// Copyright (C) 2016 by Jeremy Spiller, all rights reserved.

using System;
using System.Collections.Generic;
using System.Net;
using Newtonsoft.Json;
using System.Threading;
using System.Diagnostics;
using System.Collections.Specialized;

namespace Gosub.Viewtop
{
    class ViewtopSession
    {
        const int FUTURE_FRAME_TIMEOUT_SEC = 5; // Allow up to 5 seconds before cancelling a request
        const int HISTORY_FRAMES = 2; // Save old frames for repeated requests

        public int SessionId { get; }
        public DateTime LastRequestTime { get; set; }
        public string Challenge { get; } = DateTime.Now.Ticks.ToString();

        object mLock = new object();
        bool mAuthenticated;
        long mSequence;
        Dictionary<long, FrameInfo> mHistory = new Dictionary<long, FrameInfo>();
        FrameCollector mCollector;
        FrameCompressor mAnalyzer;

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

        public ViewtopSession(int sessionId)
        {
            SessionId = sessionId;
        }

        /// <summary>
        /// Handle a web remote view request (each request is in its own thread)
        /// </summary>
        public void ProcessWebRemoteViewerRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            string sequenceStr = request.QueryString["seq"];
            string query = request.QueryString["query"];

            if (query == "login")
            {
                // TBD: Authenticate by reading password file
                string username = request.QueryString["username"];
                string password = request.QueryString["password"];
                if (username != null && password != null)
                    mAuthenticated = username.ToLower() == "jms" && password == "12345";
                FileServer.SendResponse(response, @"{""pass"": " + mAuthenticated.ToString().ToLower() + "}", 200);
                return;
            }

            // --- Everything below this requires authentication ---
            if (!mAuthenticated)
            {
                FileServer.SendError(response, "Query 'seq' must be numeric", 401);
                return;
            }

            // --- Everything below this requires a sequence number
            long sequence;
            if (sequenceStr == null || !long.TryParse(sequenceStr, out sequence))
            {
                FileServer.SendError(response, "Query 'seq' must be numeric", 400);
                return;
            }

            if (query == "draw")
            {
                FrameInfo frame;
                if (WaitForImageOrTimeout(sequence, out frame, request.QueryString))
                    FileServer.SendResponse(response, JsonConvert.SerializeObject(frame), 200);
                else
                    FileServer.SendError(response, "Error retrieving draw frame " + sequence, 400);
                return;
            }
            FileServer.SendError(response, "ERROR: Invalid query type", 400);
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
