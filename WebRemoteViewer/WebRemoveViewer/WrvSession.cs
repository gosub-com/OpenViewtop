// Copyright (C) 2016 by Jeremy Spiller, all rights reserved.

using System;
using System.Collections.Generic;
using System.Net;
using Newtonsoft.Json;
using System.Threading;
using System.Drawing;
using System.Diagnostics;

namespace Gosub.WebRemoteViewer
{
    class WrvSession
    {
        const int FUTURE_FRAME_TIMEOUT_SEC = 5; // Allow up to 5 seconds before cancelling a request
        const int HISTORY_FRAMES = 2; // Save old frames for repeated requests

        public int SessionId { get; }
        public DateTime LastRequestTime { get; set; }

        object mLock = new object();
        long mSequence;
        Dictionary<long, FrameInfo> mHistory = new Dictionary<long, FrameInfo>();
        FrameCollector mCollector;
        FrameAnalyzer mAnalyzer;

        class FrameInfo
        {
            public long Sequence;
            public string Draw;
            public byte[] Image;
            public Stats Stats;
        }
        
        class Stats
        {
            public int CollectTime { get; set; }
            public int collectSpan { get; set; }
            public int ScoreTime { get; set; }
            public int CreateTime { get; set; }
            public int CompressTime { get; set; }
            public int DuplicateBlocks { get; set; }
            public int HashCollisionsEver { get; set; }
        }

        public WrvSession(int sessionId)
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

            long sequence;
            if (sequenceStr == null || !long.TryParse(sequenceStr, out sequence))
            {
                FileServer.SendError(response, "Query 'seq' must be numeric", 400);
                return;
            }

            // Get the next image or timeout
            if (query == "image")
            {
                string draw;
                byte[] image;
                if (WaitForImageOrTimeout(sequence, out draw, out image))
                    FileServer.SendResponse(response, image, 200);
                else
                    FileServer.SendError(response, "Error retrieving image frame " + sequence, 400);
                return;
            }
            if (query == "draw")
            {
                string draw;
                byte[] image;
                if (WaitForImageOrTimeout(sequence, out draw, out image))
                    FileServer.SendResponse(response, draw, 200);
                else
                    FileServer.SendError(response, "Error retrieving draw frame " + sequence, 400);
                return;
            }
            FileServer.SendError(response, "ERROR: Invalid query type", 400);
        }


        /// <summary>
        /// Retrieve the requested frame
        /// </summary>
        bool WaitForImageOrTimeout(long sequence, out string draw, out byte[] image)
        {
            draw = null;
            image = null;

            // If a future frame is receivied, wait for it or timeout
            // NOTE: Javascript doesn't queue future frames, but it will eventually
            DateTime now = DateTime.Now;
            while (true)
            {
                // Exit if we have the frame
                lock (mLock)
                    if (sequence <= mSequence + 1)
                        break;

                // Fail if the frame isn't ready within the timeout
                if ((DateTime.Now - now).TotalSeconds > FUTURE_FRAME_TIMEOUT_SEC)
                    return false;
                Thread.Sleep(10);
            }

            // Collect new frame, or return a previously collected frame from history.
            // NOTE: Future frames are queued above
            lock (mLock)
            {
                // Return old frames from history
                FrameInfo frameInfo;
                if (mHistory.TryGetValue(sequence, out frameInfo))
                {
                    draw = frameInfo.Draw;
                    image = frameInfo.Image;
                    return true;
                }

                // Generate an error for very old frames
                if (sequence <= mSequence)
                {
                    // TBD: How much history do we need (depends on Javascript queueing and latency)?
                    Debug.Assert(false); 
                    return false;
                }

                // Clean out history
                mHistory.Remove(sequence - HISTORY_FRAMES);
                Debug.Assert(mHistory.Count <= HISTORY_FRAMES);

                // We received a request for the next frame, which we don't have yet.
                // NOTE: Old requests were either satisified from history or timed out.
                //       Future requests were queued above.  The only way to get here 
                //       is when requesting the next frame
                Debug.Assert(sequence == mSequence + 1);
                mSequence = sequence;

                if (mCollector == null)
                    mCollector = new FrameCollector();
                if (mAnalyzer == null)
                    mAnalyzer = new FrameAnalyzer();

                // Collect the frame
                var collectStart = DateTime.Now;
                var bm = mCollector.CreateFrame();
                var collectSpan = DateTime.Now - collectStart;

                // Analyze the frame
                mAnalyzer.AnalyzeFrame(bm, out image, out draw);
                bm.Dispose();

                Stats stats = new Stats();
                stats.CollectTime = (int)collectSpan.TotalMilliseconds;
                stats.ScoreTime= (int)mAnalyzer.ScoreTime.TotalMilliseconds;
                stats.CreateTime = (int)mAnalyzer.CreateTime.TotalMilliseconds;
                stats.CompressTime = (int)mAnalyzer.CompressTime.TotalMilliseconds;
                stats.DuplicateBlocks = mAnalyzer.DuplicateBlocks;
                stats.HashCollisionsEver = mAnalyzer.HashCollisionsEver;

                // Save frame in history for repeated requests
                mHistory[sequence] = new FrameInfo() { Sequence = sequence, Draw = draw, Image = image, Stats = stats };
                return true;
            }
        }


    }
}
