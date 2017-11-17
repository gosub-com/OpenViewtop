// Copyright (C) 2016 by Jeremy Spiller, all rights reserved.

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace Gosub.Viewtop
{
    class FrameCollector
    {
        bool mIsScaledScreen;
        Bitmap mFullScreen;
        Bitmap mScaledScreen;
        double mScale = 1;

        public TimeSpan CopyTime { get; set; }
        public TimeSpan ShrinkTime { get; set; }
        public double Scale { get { return mScale; }  }
        public Bitmap Screen => mIsScaledScreen ? mScaledScreen : mFullScreen;

        /// <summary>
        /// Create a copy of the screen with a maximum width and height.
        /// Max width and height can be 0 if not specified.
        /// </summary>
        public void CopyScreen(int maxWidth, int maxHeight)
        {
            // Create the bitmap if we need to
            var copyStartTime = DateTime.Now;
            if (mFullScreen == null || mFullScreen.Width != System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width
                                    || mFullScreen.Height != System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height)
            {
                if (mFullScreen != null)
                    mFullScreen.Dispose();
                mFullScreen = new Bitmap(System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width,
                                     System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height, PixelFormat.Format32bppRgb);
            }
            // Copy screen
            using (Graphics grScreen = Graphics.FromImage(mFullScreen))
            {
                grScreen.CopyFromScreen(System.Windows.Forms.Screen.PrimaryScreen.Bounds.X,
                                        System.Windows.Forms.Screen.PrimaryScreen.Bounds.Y,
                                        0, 0, mFullScreen.Size,
                                        CopyPixelOperation.SourceCopy);

                // Draw the mouse cursor
                var cursor = CursorInfo.GetCursor(out bool cursorNeedsDisposing);
                var position = Cursor.Position;
                cursor.Draw(grScreen, new Rectangle(position.X-cursor.HotSpot.X, 
                                                    position.Y-cursor.HotSpot.Y, 
                                                    cursor.Size.Width, cursor.Size.Height));
                if (cursorNeedsDisposing)
                    cursor.Dispose();
            }
            var postCopyTime = DateTime.Now;
            CopyTime = postCopyTime - copyStartTime;


            // Calculate scaled screen size
            if (maxWidth <= 0 || maxWidth > mFullScreen.Width)
                maxWidth = mFullScreen.Width;
            if (maxHeight <= 0 || maxHeight >= mFullScreen.Height)
                maxHeight = mFullScreen.Height;
            if (maxWidth == mFullScreen.Width && maxHeight == mFullScreen.Height)
            {
                // Screen size matches, return the screen as requested
                mScale = 1;
                ShrinkTime = new TimeSpan();
                mIsScaledScreen = false;
                return;
            }
            // Scale the screen down to size
            mScale = Math.Min(maxWidth/(double)mFullScreen.Width, maxHeight/(double)mFullScreen.Height);
            int width = (int)(mScale * mFullScreen.Width);
            int height = (int)(mScale * mFullScreen.Height);
            if (mScaledScreen == null || mScaledScreen.Width != width || mScaledScreen.Height != height)
            {
                if (mScaledScreen != null)
                    mScaledScreen.Dispose();
                mScaledScreen = new Bitmap(width, height, PixelFormat.Format32bppRgb);
            }
            using (Graphics grScaled = Graphics.FromImage(mScaledScreen))
            {
                grScaled.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Bilinear;
                grScaled.DrawImage(mFullScreen, new RectangleF(0, 0, mScaledScreen.Width, mScaledScreen.Height), 
                                    new RectangleF(0, 0, mFullScreen.Width, mFullScreen.Height), GraphicsUnit.Pixel);
            }
            ShrinkTime = DateTime.Now - postCopyTime;
            mIsScaledScreen = true;
        }
    }

    class CursorInfo
    {
        // Get the cursor shown on the screen
        // NOTE: Cursor.Current does not work, which is why this class is necessary
        public static Cursor GetCursor(out bool cursorNeedsDisposing)
        {
            try
            {
                CURSORINFO cursorInfo;
                cursorInfo.cbSize = Marshal.SizeOf(typeof(CURSORINFO));
                GetCursorInfo(out cursorInfo);
                if (cursorInfo.hCursor != IntPtr.Zero)
                {
                    var cursor = new Cursor(cursorInfo.hCursor);

                    // NOTE: Since the IBeam uses XOR, it is invisible when drawn by C#
                    //       We should either create our own IBeam icon, or else use GDI
                    //       to draw it
                    if (cursor == Cursors.IBeam)
                    {
                        cursor.Dispose();
                        cursorNeedsDisposing = false;
                        return Cursors.Default;
                    }
                    cursorNeedsDisposing = true;
                    return cursor;
                }
            }
            catch
            {
            }
            cursorNeedsDisposing = false;
            return Cursors.Default;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct CURSORINFO
        {
            public int cbSize;
            public int flags;
            public IntPtr hCursor;
            public POINT ptScreenPos;
        }

        [DllImport("user32.dll")]
        static extern bool GetCursorInfo(out CURSORINFO pci);
    }
}
