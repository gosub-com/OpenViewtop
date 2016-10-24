// Copyright (C) 2016 by Jeremy Spiller, all rights reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Gosub.WebRemoteViewer
{
    /// <summary>
    /// Analyze a frame.  Look for un-changed sections, solid colors, duplicates,
    /// and optionally look for PNG compressed blocks.
    /// </summary>
    unsafe class FrameAnalyzer
    {

        Bitmap32Bits mBackground;

        int mBlockSize = 16; // Should be a power of two, should be >= 16
        float mPngCompressionThreshold = 0.7f;

        TimeSpan mScoreTime;
        TimeSpan mCreateTime;
        TimeSpan mCompressTime;
        int mFrameIndex;
        int mHashCollisions;
        int mDuplicateBlocks;

        public bool NoCompression { get; set; }
        public bool SuppressBackgroundCompare { get; set; }
        public bool SmartPng { get; set; } = true;

        public TimeSpan ScoreTime { get { return mScoreTime; } }
        public TimeSpan CreateTime { get { return mCreateTime; } }
        public TimeSpan CompressTime { get { return mCompressTime; } }
        public int DuplicateBlocks {  get { return mDuplicateBlocks; } }
        public int HashCollisionsEver {  get { return mHashCollisions; } }

        struct Block
        {
            public short Index;
            public short X;
            public short Y;
            public Score Score;
            public Block(int index, int x, int y, Score score) { Index = (short)index;  X = (short)x;  Y = (short)y;  Score = score; }
        }

        enum Score : short
        {
            None = 0,
            Clear = 1,
            Solid = 2,
            ClearAndSolid = 3,
            Png = 4,
            Jpg = 8,
            LowFrequency = 16,
            Partial = 32,
            Duplicate = 64
        }

        class FrameInfo
        {
            public Stream Image;
            public string Draw;
            public FrameInfo(string draw, Stream image)
            {
                Draw = draw;
                Image = image;
            }
        }

        /// <summary>
        /// Analyze a bitmap, and keep a copy as the new background.  If the bitmap is not
        /// the same size as the previous one, a new comparison is made.  You must
        /// dispose the bitmap when done with it.
        /// NOTE: Throws away the PNG for now (deal with it later)
        /// </summary>
        public void AnalyzeFrame(Bitmap bm, out Stream stream, out string draw)
        {
            if (NoCompression)
            {
                mScoreTime = new TimeSpan();
                mCreateTime = new TimeSpan();
                var tc = DateTime.Now;
                stream = new MemoryStream();
                draw = "No Compression";
                bm.Save(stream, ImageFormat.Jpeg);
                mCompressTime = DateTime.Now - tc;
                return;
            }

            // Score bitmap
            var t1 = DateTime.Now;
            mFrameIndex++;
            var bits = new Bitmap32Bits(bm, ImageLockMode.ReadOnly);
            List<Block> blocks = ScoreFrame(bits);

            // Create bitmaps
            var t2 = DateTime.Now;
            Bitmap png, jpg;
            BuildBitmap(bits, blocks, Score.Png, out png, out draw);
            BuildBitmap(bits, blocks, Score.Solid | Score.Duplicate | Score.Jpg, out jpg, out draw);
            bits.Dispose();


            // Compress bitmap
            var t3 = DateTime.Now;
            var jpgStream = new MemoryStream();
            jpg.Save(jpgStream, ImageFormat.Jpeg);
            jpgStream.Position = 0;


            mScoreTime = t2 - t1;
            mCreateTime = t3 - t2;
            mCompressTime = DateTime.Now - t3;

            //SaveDebugBitmaps(bm, blocks, png, jpg);

            png.Dispose();
            jpg.Dispose();

            // Since the draw string isn't valid yet, chop it
            if (draw.Length >= 50)
                draw = draw.Substring(0, 50) + "...";

            draw = "Frame " + mFrameIndex + ": " + draw;
            stream = jpgStream;
        }

        /// <summary>
        /// Score a frame, creating a list of blocks
        /// </summary>
        private List<Block> ScoreFrame(Bitmap32Bits bmd)
        {
            // Optionally create background if it doesn't exist, or the size changed
            bool supressBackgroundCompare = SuppressBackgroundCompare;
            if (mBackground == null || mBackground.Width != bmd.Width || mBackground.Height != bmd.Height)
            {
                // New background, do not compare with previous
                supressBackgroundCompare = true;
                if (mBackground != null)
                    mBackground.Dispose();
                mBackground = new Bitmap32Bits(bmd.Width, bmd.Height);
            }

            var bi = -1; // Block index
            var blocks = new List<Block>();
            var duplicates = new Dictionary<int, List<Block>>();
            mDuplicateBlocks = 0;
            for (int y = 0; y < bmd.Height; y += mBlockSize)
            {
                for (int x = 0; x < bmd.Width; x += mBlockSize)
                {
                    // Skip clear blocks
                    bi++;
                    int width = Math.Min(mBlockSize, bmd.Width - x);
                    int height = Math.Min(mBlockSize, bmd.Height - y);
                    if (!supressBackgroundCompare && bmd.IsMatch(x, y, width, height, mBackground, x, y))
                    {
                        continue; // Nothing changed, skip it
                    }
                    bmd.Copy(x, y, width, height, mBackground, x, y);

                    // Check for solid blocks
                    if (bmd.IsSolid(x, y, width, height))
                    {
                        blocks.Add(new Block(bi, x, y, Score.Solid));
                        continue;
                    }
                    // Check for duplicates
                    if (IsDuplicate(bmd, duplicates, bi, x, y, width, height))
                    {
                        blocks.Add(new Block(bi, x, y, Score.Duplicate));
                        mDuplicateBlocks++;
                        continue;
                    }
                    // Check for PNG or JPG
                    if (SmartPng)
                    {
                        var rleCount = bmd.RleCount(x, y, width, height);
                        var rleTotal = height * width;
                        var rleCompression = rleCount / (float)rleTotal;
                        if (rleCompression > mPngCompressionThreshold && !bmd.LowFrequency(x, y, width, height))
                        {
                            blocks.Add(new Block(bi, x, y, Score.Png));
                            continue;

                        }
                    }
                    // Use Jpeg
                    blocks.Add(new Block(bi, x, y, Score.Jpg));
                }
            }

            return blocks;
        }

        private void SaveDebugBitmaps(Bitmap bm, List<Block> blocks, Bitmap png, Bitmap jpg)
        {
            png.Save("c:\\zzresources\\png.png", ImageFormat.Png);
            jpg.Save("c:\\zzresources\\jpg.jpg", ImageFormat.Jpeg);
            jpg.Save("c:\\zzresources\\jpg-p.png", ImageFormat.Png);
            bm.Save("c:\\zzresources\\Desktop.png", ImageFormat.Png);
            bm.Save("c:\\zzresources\\Desktop.jpg", ImageFormat.Jpeg);

            // Clear out JPG portions
            using (var bmd = new Bitmap32Bits(bm, ImageLockMode.ReadWrite))
            {
                foreach (var block in blocks)
                    if (block.Score.HasFlag(Score.Jpg))
                        bmd.Fill(0, block.X, block.Y, Math.Min(mBlockSize, bmd.Width-block.X), Math.Min(mBlockSize, bmd.Height-block.Y));
                bmd.Dispose();
                bm.Save("C:\\zzresources\\DesktopClear.png", ImageFormat.Png);
            }

            // Draw output frame
            var gr = Graphics.FromImage(bm);
            foreach (var block in blocks)
            {
                Score score = block.Score;
                int x = block.X;
                int y = block.Y;
                if (score == Score.Solid)
                    gr.FillRectangle(Brushes.White, new Rectangle(x, y, mBlockSize, mBlockSize));
                else if (score == Score.Png)
                    gr.FillRectangle(Brushes.Gray, new Rectangle(x, y, mBlockSize, mBlockSize));
                else if (score == Score.Jpg)
                    gr.FillRectangle(Brushes.Pink, new Rectangle(x, y, mBlockSize, mBlockSize));
                else if (score == Score.Duplicate)
                    gr.FillRectangle(Brushes.LightGreen, new Rectangle(x, y, mBlockSize, mBlockSize));
                else
                    Debug.Assert(false);
            }
            bm.Save("c:\\zzresources\\Score.jpg");

            gr.Dispose();
        }

        private bool IsDuplicate(Bitmap32Bits bmd, Dictionary<int, List<Block>> dups, int bi, int x, int y, int width, int height)
        {
            int hash = bmd.HashBlock(x, y, width, height);

            if (dups.ContainsKey(hash))
            {
                // Possibly a match, but needs verification just in case there is a hash collison
                var list = dups[hash];
                foreach (var testBlock in list)
                    if (bmd.IsMatch(x, y, width, height, bmd, testBlock.X, testBlock.Y))
                        return true;

                // Hash collision - rare occurence
                dups[hash].Add(new Block(bi, x, y, Score.None));
                mHashCollisions++;
            }
            else
            {
                // First time this block has been seen
                dups[hash] = new List<Block>() { new Block(bi, x, y, Score.None) };
            }
            return false;
        }


        /// <summary>
        /// Build a bitmap with any of the given score bits set
        /// </summary>
        void BuildBitmap(Bitmap32Bits bmdSource, List<Block> blocks, Score score, out Bitmap image, out string draw)
        {
            // Count blocks to write to target
            int numTargetBlocks = 0;
            foreach (var block in blocks)
                if (block.Score.HasFlag(score & (Score.Jpg | Score.Png) ))
                    numTargetBlocks++;

            // Generate the bitmap
            int size = (int)Math.Sqrt(numTargetBlocks) + 1;
            var bmTarget = new Bitmap(size * mBlockSize, size * mBlockSize, PixelFormat.Format32bppArgb);
            var bmdTarget = new Bitmap32Bits(bmTarget, ImageLockMode.WriteOnly);
            var compressor = new BitmapBlockCompressor(bmdSource, bmdTarget, mBlockSize);
            foreach (var block in blocks)
                if ((block.Score & score) != Score.None)
                    compressor.Write(block);

            bmdTarget.Dispose();

            image = bmTarget;
            draw = compressor.GetDrawString();
        }

        /// <summary>
        /// Compress a bitmap, copying source blocks to target blocks when necessary.
        /// TBD: The generated draw string is not working yet
        /// </summary>
        class BitmapBlockCompressor
        {
            StringBuilder mSb = new StringBuilder();
            Bitmap32Bits mSource;
            Bitmap32Bits mTarget;
            int mBlockSize;

            int mSourceIndex;
            int mTargetX;
            int mTargetY;

            int mSolidColor;
            int mSolidsInARow;
            int mDuplicatesInARow;
            int mCopiesInARow;

            public BitmapBlockCompressor(Bitmap32Bits source, Bitmap32Bits target, int blockSize)
            {
                mSource = source;
                mTarget = target;
                mBlockSize = blockSize;
            }

            public void Write(Block block)
            {
                if (block.Index != mSourceIndex)
                {
                    // Skip blocks
                    Flush();
                    mSb.Append('s');
                    mSb.Append(block.Index - mSourceIndex);
                    mSourceIndex = block.Index;
                }
                if (block.Score.HasFlag(Score.Solid) && mSource[block.X, block.Y] != mSolidColor)
                {
                    // Set solid color
                    Flush();
                    mSolidColor = mSource[block.X, block.Y];
                    mSb.Append('c');
                    mSb.Append(mSolidColor);
                }
                if (block.Score.HasFlag(Score.Solid))
                {
                    // Cache solid color
                    if (mSolidsInARow == 0)
                        Flush();
                    mSolidsInARow++;
                    mSourceIndex++;
                    return;
                }
                if (block.Score.HasFlag(Score.Duplicate))
                {
                    // Cache duplicate block
                    // TBD: This doesn't work yet
                    // Need to duplicate from location, with repeats
                    if (mDuplicatesInARow == 0)
                        Flush();
                    mDuplicatesInARow++;
                    mSourceIndex++;
                    return;
                }
                if ((block.Score & (Score.Jpg | Score.Png)) != Score.None)
                {
                    // Cache copy to target
                    if (mCopiesInARow == 0)
                        Flush();
                    mCopiesInARow++;
                    mSourceIndex++;

                    // Copy to output, then move to next
                    mSource.Copy(block.X, block.Y, Math.Min(mBlockSize, mSource.Width - block.X),
                                                   Math.Min(mBlockSize, mSource.Height - block.Y), mTarget, mTargetX, mTargetY);

                    mTargetX += mBlockSize;
                    if (mTargetX + mBlockSize > mTarget.Width)
                    {
                        mTargetY += mBlockSize;
                        mTargetX = 0;
                    }
                    return;
                }
                // All block types should be accounted  for above
                Debug.Assert(false);
            }

            void Flush()
            {
                if (mSolidsInARow != 0)
                {
                    // Append solid color
                    mSb.Append('S');
                    mSb.Append(mSolidsInARow);
                    mSolidsInARow = 0;
                }
                if (mDuplicatesInARow != 0)
                {
                    mSb.Append('D');
                    mSb.Append(mDuplicatesInARow);
                    mSb.Append('?'); // TBD: Get duplicates working
                    mDuplicatesInARow = 0;
                }
                if (mCopiesInARow != 0)
                {
                    // Append solid color
                    mSb.Append('C');
                    mSb.Append(mCopiesInARow);
                    mCopiesInARow = 0;
                }
            }

            public string GetDrawString()
            {
                Flush();
                return mSb.ToString();
            }


        }




    }
}
