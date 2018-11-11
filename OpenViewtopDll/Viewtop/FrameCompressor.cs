// Copyright (C) 2016 by Jeremy Spiller, all rights reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Gosub.Viewtop
{
    /// <summary>
    /// Analyze a frame.  Look for un-changed sections, solid colors, duplicates,
    /// and optionally look for PNG compressed blocks.
    /// </summary>
    unsafe class FrameCompressor
    {
        Bitmap32Bits mBackground;

        int mBlockSize = 16; // Should be a power of two, should be >= 16

        // Each block is scored for PNG compressibility. 
        // Text on a shrunken screen is usually > 50% compressible, 
        // so use that as the cutoff before switching to JPG.
        float mPngCompressionThreshold = 0.50f;

        int mFrameIndex;
        int mHashCollisions;
        int mDuplicates;
        int mBlocksX;
        bool mForceDisableDelta;

        bool mFullFrameAnalysis;
        OutputType mOutput;
        CompressionType mCompression;

        public int Duplicates {  get { return mDuplicates; } }
        public int HashCollisionsEver {  get { return mHashCollisions; } }

        // Convert block index to X and Y coordinates
        int ItoX(int i) { return (i % mBlocksX) * mBlockSize; }
        int ItoY(int i) { return (i / mBlocksX) * mBlockSize; }

        public enum CompressionType
        {
            Jpg,
            Png,
            SmartPng
        }

        public enum OutputType
        {
            Normal,
            FullFrameJpg,
            FullFramePng,
            CompressionMap,
            HideJpg,
            HidePng
        }

        internal struct Block
        {
            public short Index;
            public Score Score;
            public Block(int index, Score score) { Index = (short)index;  Score = score; }
            public override string ToString() { return "" + Index + "=" + Score; }
        }

        internal enum Score : short
        {
            None = 0,
            Solid = 1,
            Duplicate = 2,
            Png = 4,
            Jpg = 8,            
            ScoreMask = 0xF0,
            ScoreShift = 4,
            LowFrequency = 0x100

        }

        /// <summary>
        /// Force full frame analysis without differential compression
        /// </summary>
        public bool FullFrame
        {
            get { return mFullFrameAnalysis; }
            set { mFullFrameAnalysis = value; }
        }

        public OutputType Output
        {
            get { return mOutput; }
            set
            {
                if (value != mOutput && value == OutputType.Normal)
                    mForceDisableDelta = true;
                mOutput = value;
            }
        }

        public CompressionType Compression
        {
            get { return mCompression; }
            set
            {
                if (value != mCompression)
                    mForceDisableDelta = true;
                mCompression = value;
            }
        }

        public class Frame
        {
            // NOTE: Do not change names - they are converted to JSON and sent to client
            public string Draw;
            public string Image;

            public Frame(string draw, string image)
            {
                Draw = draw;
                Image = image;
            }

            public Frame(string draw, MemoryStream image, ImageFormat format)
            {
                Image = (format == ImageFormat.Png ? "data:image/png;base64," : "data:image/jpeg;base64,")
                    + Convert.ToBase64String(image.GetBuffer(), 0, (int)image.Length);
                Draw = draw;
            }

        }

        /// <summary>
        /// Analyze a bitmap, and keep a copy as the new background.  If the bitmap is not
        /// the same size as the previous one, a new comparison is made.  You must
        /// dispose the bitmap when done with it.  Returns 1 or 2 frames (PNG/JPG)
        /// depending on settings
        /// </summary>
        public Frame []Compress(Bitmap frame)
        {
            // Generate full frame JPG or PNG (debugging output)
            if (mOutput == OutputType.FullFrameJpg || mOutput == OutputType.FullFramePng)
            {
                var f = GenerateFullFrame((Bitmap)frame, mOutput == OutputType.FullFrameJpg ? ImageFormat.Jpeg : ImageFormat.Png);
                return new Frame[] { f };
            }

            // Force full frame analysis if requested
            bool disableDelta = mForceDisableDelta 
                                    || mFullFrameAnalysis
                                    || Output == OutputType.HideJpg 
                                    || Output == OutputType.HidePng;
            mForceDisableDelta = false;
            
            // Optionally create background if it doesn't exist, or the size changed
            if (mBackground == null || mBackground.Width != frame.Width || mBackground.Height != frame.Height)
            {
                // New background, do not compare with previous
                disableDelta = true;
                if (mBackground != null)
                    mBackground.Dispose();
                mBackground = new Bitmap32Bits(frame.Width, frame.Height);
            }

            // Number of blocks visible (even if partially visible)
            mBlocksX = (frame.Width + mBlockSize - 1) / mBlockSize;

            // Score bitmap
            mFrameIndex++;
            var frameBits = new Bitmap32Bits(frame, ImageLockMode.ReadOnly);
            List<Block> blocks;
            Dictionary<int, int> duplicates;
            int jpgCount;
            int pngCount;
            ScoreFrame(frameBits, disableDelta, out blocks, out duplicates, out jpgCount, out pngCount);

            // Build the bitmaps (JPG and PNG)
            Frame[] frames;
            if (jpgCount == 0 || pngCount == 0)
            {
                // Build one frame (both PNG and JPG)
                frames = new Frame[]
                    { BuildBitmap(frameBits, blocks, 
                                  duplicates, Score.Solid | Score.Duplicate | Score.Jpg | Score.Png, 
                                  pngCount == 0 ? ImageFormat.Jpeg : ImageFormat.Png) };
            }
            else
            {
                // Build two frames (PNG first, and JPG second)
                frames = new Frame[]
                    { BuildBitmap(frameBits, blocks, duplicates, Score.Solid | Score.Duplicate | Score.Png, ImageFormat.Png),
                      BuildBitmap(frameBits, blocks, duplicates, Score.Duplicate | Score.Jpg, ImageFormat.Jpeg)};
            }
            frameBits.Dispose();

            // Optionally create compression maps and other debug output
            if (mOutput == OutputType.CompressionMap)
            {
                CopyDuplicateAttributesForDebug(blocks, duplicates);
                var map = GenerateCompressionMap(frame.Width, frame.Height, blocks);
                var f = GenerateFullFrame(map, ImageFormat.Png);
                map.Dispose();
                return new Frame[] { f };
            }
            else if (mOutput == OutputType.HideJpg || mOutput == OutputType.HidePng)
            {
                CopyDuplicateAttributesForDebug(blocks, duplicates);
                var map = GenerateHideMap(frame, blocks, mOutput == OutputType.HideJpg ? Score.Jpg : Score.Png);
                var f = GenerateFullFrame(map, ImageFormat.Jpeg);
                map.Dispose();
                return new Frame[] { f };
            }
            return frames;
        }

        /// <summary>
        /// Score a frame, creating a list of blocks
        /// </summary>
        private void ScoreFrame(Bitmap32Bits frame, 
                                bool disableDelta, 
                                out List<Block> blocks, 
                                out Dictionary<int, int> duplicates,
                                out int jpgCount,
                                out int pngCount)
        {
            blocks = new List<Block>();
            duplicates = new Dictionary<int, int>();
            jpgCount = 0;
            pngCount = 0;

            var sourceIndex = -1;
            var duplicateHashToIndex = new Dictionary<int, int>();
            for (int y = 0; y < frame.Height; y += mBlockSize)
            {
                for (int x = 0; x < frame.Width; x += mBlockSize)
                {
                    sourceIndex++;
                    Debug.Assert(ItoX(sourceIndex) == x);
                    Debug.Assert(ItoY(sourceIndex) == y);

                    // Skip clear blocks
                    int width = Math.Min(mBlockSize, frame.Width - x);
                    int height = Math.Min(mBlockSize, frame.Height - y);
                    if (!disableDelta && frame.IsMatch(x, y, width, height, mBackground, x, y))
                    {
                        continue; // Nothing changed, skip it
                    }
                    frame.Copy(x, y, width, height, mBackground, x, y);

                    // Check for solid blocks
                    if (frame.IsSolid(x, y, width, height))
                    {
                        blocks.Add(new Block(sourceIndex, Score.Solid));
                        continue;
                    }
                    // Check for duplicates
                    int duplicateIndex;
                    if (IsDuplicate(frame, duplicateHashToIndex, sourceIndex, out duplicateIndex))
                    {
                        blocks.Add(new Block(sourceIndex, Score.Duplicate));
                        duplicates[sourceIndex] = duplicateIndex;
                        continue;
                    }
                    // Compression: SmartPNG, JPG, or PNG
                    bool usePng = mCompression == CompressionType.Png;
                    var score = Score.None;
                    if (mCompression == CompressionType.SmartPng && width >= 4 && height >= 4)
                    {
                        // SmartPng - Check to see if the block would compress well with PNG
                        var rleCount = frame.RleCount(x, y, width, height);
                        var rleTotal = (height-1) * (width-1);
                        var rleCompression = rleCount / (float)rleTotal;
                        score = (Score)((int)(rleCompression * 9.99) << (int)Score.ScoreShift) & Score.ScoreMask;
                        if (rleCompression > mPngCompressionThreshold)
                        {
                            if (!frame.LowFrequency(x, y, width, height))
                                usePng = true; // Good compression, not low frequency
                            else
                                score |= Score.LowFrequency;
                        }
                    }
                    blocks.Add(new Block(sourceIndex, score | (usePng ? Score.Png : Score.Jpg)));
                    if (usePng)
                        pngCount++;
                    else
                        jpgCount++;
                }
            }
            mDuplicates = duplicates.Count;
        }


        private bool IsDuplicate(Bitmap32Bits bm, 
                                 Dictionary<int, int> duplicateHashToIndex, 
                                 int sourceIndex, 
                                 out int duplicateIndex)
        {
            int x = ItoX(sourceIndex);
            int y = ItoY(sourceIndex);
            int width = Math.Min(mBlockSize, bm.Width - x);
            int height = Math.Min(mBlockSize, bm.Height - y);

            int hash = bm.HashInt(x, y, width, height);

            if (duplicateHashToIndex.ContainsKey(hash))
            {
                // Possibly a match, but needs verification just in case there is a hash collison
                duplicateIndex = duplicateHashToIndex[hash];
                if (bm.IsMatch(x, y, width, height, bm, ItoX(duplicateIndex), ItoY(duplicateIndex)))
                {
                    return true;
                }
                // Hash collision.  This is very rare.
                mHashCollisions++;
                duplicateIndex = 0;
                return false;
            }
            // First time this block has been seen
            duplicateHashToIndex[hash] = sourceIndex;
            duplicateIndex = 0;
            return false;
        }

        /// <summary>
        /// Build a bitmap with any of the given score bits set
        /// </summary>
        Frame BuildBitmap(Bitmap32Bits frame, 
                         List<Block> blocks, 
                         Dictionary<int, int> duplicates, 
                         Score score,
                         ImageFormat format)
        {
            // Count blocks to write to target
            int numTargetBlocks = 0;
            foreach (var block in blocks)
                if ( (block.Score & score & (Score.Jpg | Score.Png)) != Score.None )
                    numTargetBlocks++;

            // Generate the bitmap
            int size = (int)Math.Sqrt(numTargetBlocks) + 1;
            var bmTarget = new Bitmap(size * mBlockSize, size * mBlockSize, PixelFormat.Format32bppArgb);
            var bmdTarget = new Bitmap32Bits(bmTarget, ImageLockMode.WriteOnly);
            var blockWriter = new BlockWriter(frame, bmdTarget, mBlockSize, duplicates);
            foreach (var block in blocks)
                if ((block.Score & score) != Score.None)
                    blockWriter.Write(block);
            bmdTarget.Dispose();

            // Compress frame to stream
            var targetStream = new MemoryStream();
            bmTarget.Save(targetStream, format);
            bmTarget.Dispose();
            return new Frame(blockWriter.GetDrawString(), targetStream, format);
        }

        /// <summary>
        /// Slow function for debugging
        /// </summary>
        void CopyDuplicateAttributesForDebug(List<Block> blocks, Dictionary<int, int> duplicates)
        {
            for (int i = 0; i < blocks.Count; i++)
            {
                int di;
                Block block = blocks[i];
                if (block.Score.HasFlag(Score.Duplicate) && duplicates.TryGetValue(block.Index, out di))
                    for (int j = 0; j < i; j++)
                        if (blocks[j].Index == di)
                            blocks[i] = new Block(block.Index, blocks[j].Score | Score.Duplicate);
            }
        }

        /// <summary>
        /// Generate a full frame from the bitmap
        /// </summary>
        private static Frame GenerateFullFrame(Bitmap frame, ImageFormat format)
        {
            var ms = new MemoryStream();
            frame.Save(ms, format);
            return new Frame("!", ms, format);
        }

        private Bitmap GenerateCompressionMap(int width, int height, List<Block> blocks)
        {
            // Draw output frame
            Bitmap map = new Bitmap(width, height, PixelFormat.Format32bppRgb);
            using (var mapGr = Graphics.FromImage(map))
                foreach (var block in blocks)
                {
                    Score score = block.Score;
                    int x = ItoX(block.Index);
                    int y = ItoY(block.Index);
                    if (score.HasFlag(Score.Solid))
                    { 
                        mapGr.FillRectangle(Brushes.White, new Rectangle(x, y, mBlockSize, mBlockSize));
                    }
                    else if (score.HasFlag(Score.Png))
                    { 
                        mapGr.FillRectangle(Brushes.Gray, new Rectangle(x, y, mBlockSize, mBlockSize));
                    }
                    else if (score.HasFlag(Score.Jpg))
                    { 
                        mapGr.FillRectangle(Brushes.Pink, new Rectangle(x, y, mBlockSize, mBlockSize));
                    }

                    if (score.HasFlag(Score.Duplicate))
                    {
                        mapGr.DrawRectangle(Pens.Blue, new Rectangle(x + 4, y + 4, mBlockSize - 8, mBlockSize - 8));
                    }
                    else if (score.HasFlag(Score.Jpg) || score.HasFlag(Score.Png))
                    {
                        mapGr.DrawString(score.HasFlag(Score.LowFrequency) ? "L"
                            : "" + ((int)(score & Score.ScoreMask) >> (int)Score.ScoreShift),
                                            SystemFonts.DefaultFont, Brushes.Black, x+4, y+2);
                    }
                }
            return map;
        }

        private Bitmap GenerateHideMap(Bitmap frame, List<Block> blocks, Score hide)
        {
            Bitmap map = new Bitmap(frame.Width, frame.Height, PixelFormat.Format32bppRgb);
            using (var mapGr = Graphics.FromImage(map))
            {
                mapGr.DrawImage(frame, 0, 0);
                foreach (var block in blocks)
                    if (block.Score.HasFlag(hide))
                    {
                        int x = ItoX(block.Index);
                        int y = ItoY(block.Index);
                        mapGr.FillRectangle(Brushes.Black, x, y, mBlockSize, mBlockSize);
                    }
            }
            return map;
        }

    }
}
