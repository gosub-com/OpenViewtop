// Copyright (C) 2016 by Jeremy Spiller, all rights reserved.

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Gosub.Webtop
{
    /// <summary>
    /// Manage a 32 bit per pixel bitmap, either locked 
    /// from a Bitmap or allocated in memory.
    /// </summary>
    unsafe sealed class Bitmap32Bits : IDisposable
    {
        int* mScan0;
        int mWidth;
        int mHeight;
        int mStrideInts;  // Int's, not bytes

        Bitmap mBitmap;
        BitmapData mBmd;

        public int Width { get { return mWidth; } }
        public int Height { get { return mHeight; } }

        /// <summary>
        /// Lock the bits from a 32 bits per pixel bitmap (dispose when done to unlock)
        /// </summary>
        public Bitmap32Bits(Bitmap bm, ImageLockMode lockMode)
        {
            mBitmap = bm;
            mBmd = bm.LockBits(new Rectangle(0, 0, bm.Width, bm.Height), lockMode, bm.PixelFormat);

            Debug.Assert(bm.Width >= 1 && bm.Height >= 1);
            Debug.Assert(mBmd.Stride > 0 && (mBmd.Stride & (sizeof(int) - 1)) == 0);
            Debug.Assert(bm.PixelFormat == PixelFormat.Format32bppRgb || bm.PixelFormat == PixelFormat.Format32bppArgb);

            mScan0 = (int*)mBmd.Scan0;
            mWidth = mBmd.Width;
            mHeight = mBmd.Height;
            mStrideInts = mBmd.Stride / sizeof(int);
        }

        /// <summary>
        /// Create an in-memory bitmap buffer
        /// </summary>
        public Bitmap32Bits(int width, int height)
        {
            Debug.Assert(width >= 1 && height >= 1);
            GC.AddMemoryPressure(width * height * sizeof(int));
            mScan0 = (int*)Marshal.AllocHGlobal(width * height * sizeof(int));
            mWidth = width;
            mHeight = height;
            mStrideInts = width;
        }

        /// <summary>
        /// Free the bitmap memory or unlock the bimap bits
        /// </summary>
        ~Bitmap32Bits()
        {
            Dispose();
        }

        public int this[int x, int y]
        {
            get
            {
                Debug.Assert(x >= 0 && x < mWidth);
                Debug.Assert(y >= 0 && y < mHeight);
                return mScan0[y*mStrideInts + x];
            }
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            if (mBitmap != null && mBmd != null)
            {
                mBitmap.UnlockBits(mBmd);
            }
            else if (mScan0 != null)
            {
                GC.RemoveMemoryPressure(mWidth * mHeight * sizeof(int));
                Marshal.FreeHGlobal((IntPtr)mScan0);
            }
            mScan0 = null;
            mBitmap = null;
            mBmd = null;
        }

        /// <summary>
        /// Copy a block from this bitmap to the other bitmap.
        /// </summary>
        public void Copy(int x, int y, int width, int height, Bitmap32Bits toBm, int toX, int toY)
        {
            Debug.Assert(x >= 0 && x+width <= mWidth);
            Debug.Assert(y >= 0 && y+height <= mHeight);
            Debug.Assert(toX >= 0 && toX+width <= toBm.Width);
            Debug.Assert(toY >= 0 && toY+height <= toBm.Height);

            // Copy pixels
            int* fromPtr = mScan0 + y*mStrideInts + x;
            int* toPtr = toBm.mScan0 + toY*toBm.mStrideInts + toX;
            while (--height >= 0)
            {
                int w = width;
                while (--w >= 0)
                    *toPtr++ = *fromPtr++;

                // Move to next line
                fromPtr += mStrideInts - width;
                toPtr += toBm.mStrideInts - width;
            }
        }

        /// <summary>
        /// Returns true if the bitmap is solid
        /// </summary>
        public bool IsSolid(int x, int y, int width, int height)
        {
            Debug.Assert(width > 0 && height > 0);
            Debug.Assert(x >= 0 && x+width <= mWidth);
            Debug.Assert(y >= 0 && y+height <= mHeight);

            // Check for solid
            int* bmPtr = mScan0 + y*mStrideInts + x;
            int color = *bmPtr;
            while (--height >= 0)
            {
                int w = width;
                while (--w >= 0)
                    if (*bmPtr++ != color)
                        return false;
                bmPtr += mStrideInts - width;
            }
            return true;
        }

        /// <summary>
        /// Fill a block
        /// </summary>
        public void Fill(int color, int x, int y, int width, int height)
        {
            Debug.Assert(x >= 0 && x+width <= mWidth);
            Debug.Assert(y >= 0 && y+height <= mHeight);

            // Copy pixels
            int* bmPtr = mScan0 + y*mStrideInts + x;
            while (--height >= 0)
            {
                int w = width;
                while (--w >= 0)
                    *bmPtr++ = 0;
                bmPtr += mStrideInts - width;
            }
        }

        /// <summary>
        /// Count number of run length encoded pixels in this block.
        /// A high number is a good candidate for PNG.
        /// </summary>
        public int RleCount(int x, int y, int width, int height)
        {
            Debug.Assert(x >= 0 && x + width <= mWidth);
            Debug.Assert(y >= 0 && y + height <= mHeight);

            // Minus 1 for edges
            width--;
            height--;

            // Count RLE repeats
            int* bmPtr = mScan0 + y*mStrideInts + x;
            int rleCount = 0;
            while (--height >= 0)
            {
                int* belowPtr = bmPtr + mStrideInts;
                int w = width;
                while (--w >= 0)
                {
                    int pixel = *bmPtr++;
                    if (pixel == *belowPtr++ || pixel == *bmPtr)
                        rleCount++;
                }
                bmPtr += mStrideInts - width;
            }
            return rleCount;
        }

        /// <summary>
        /// Return TRUE if this block contains mostly low frequency changes
        /// (i.e. Neighboring pixels are within about +/- 8, give or take)
        /// Clip right/bottom edge if necessary.
        /// </summary>
        public bool LowFrequency(int x, int y, int width, int height)
        {
            Debug.Assert(x >= 0 && x+width <= mWidth);
            Debug.Assert(y >= 0 && y+height <= mHeight);

            // Minus 1 for edges
            width--;
            height--;

            // Check for low frequency block
            int* bmPtr = mScan0 + y*mStrideInts + x;
            while (--height >= 0)
            {
                int* belowPtr = bmPtr + mStrideInts;
                int w = width;
                while (--w >= 0)
                {
                    // Look for pixel colors further apart than about 8.
                    // This is not exact because it's 1's complement, and can
                    // carry to next color, but it's fast and close enough.
                    int pixel = (*bmPtr++ >> 1) & 0x7F7F7F7F;
                    int hDiff = (((*bmPtr >> 1) & 0x7F7F7F7F) - pixel) + 0x04040404;
                    int vDiff = (((*belowPtr++ >> 1) & 0x7F7F7F7F) - pixel) + 0x04040404;
                    if (((hDiff | vDiff) & ~0x07070707) != 0)
                        return false;
                }
                // Move to next line
                bmPtr += mStrideInts - width;
            }
            return true;
        }

        /// <summary>
        /// Calculate a hash for this block
        /// </summary>
        public int HashBlock(int x, int y, int width, int height)
        {
            Debug.Assert(x >= 0 && x+width <= mWidth);
            Debug.Assert(y >= 0 && y+height <= mHeight);

            // Calculate hash
            int* bmPtr = mScan0 + y*mStrideInts + x;
            uint hash = 0x12345678;
            while (--height >= 0)
            {
                int w = width;
                while (--w >= 0)
                {
                    // Good hash function?
                    hash += (uint)*bmPtr++ + ((hash << 7) | (hash >> 25));
                }
                bmPtr += mStrideInts - width;
            }
            return (int)(hash);
        }

        /// <summary>
        /// Retuns true if these two blocks the same
        /// </summary>
        public bool IsMatch(int x1, int y1, int width, int height, Bitmap32Bits bm2, int x2, int y2)
        {
            Debug.Assert(x1 >= 0 && x1+width <= mWidth);
            Debug.Assert(y1 >= 0 && y1+height <= mHeight);
            Debug.Assert(x2 >= 0 && x2+width <= bm2.mWidth);
            Debug.Assert(y2 >= 0 && y2+height <= bm2.mHeight);

            // Check for match
            int* bmPtr1 = mScan0 + y1 * mStrideInts + x1;
            int* bmPtr2 = bm2.mScan0 + y2*bm2.mStrideInts + x2;
            while (--height >= 0)
            {
                int w = width;
                while (--w >= 0)
                    if (*bmPtr1++ != *bmPtr2++)
                        return false;

                bmPtr1 += mStrideInts - width;
                bmPtr2 += bm2.mStrideInts - width;
            }
            return true;
        }


    }
}
