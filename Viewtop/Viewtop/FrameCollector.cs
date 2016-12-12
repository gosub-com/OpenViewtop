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
        Bitmap mScreen;
        double mScale = 1;

        public TimeSpan CopyTime { get; set; }
        public TimeSpan ShrinkTime { get; set; }
        public double Scale { get { return mScale; }  }

        /// <summary>
        /// Create a copy of the screen with a maximum width and height.
        /// Max width and height can be 0 if not specified.
        /// </summary>
        public Bitmap CreateFrame(int maxWidth, int maxHeight)
        {
            // Create the bitmap if we need to
            var copyStartTime = DateTime.Now;
            if (mScreen == null || mScreen.Width != Screen.PrimaryScreen.Bounds.Width
                                || mScreen.Height != Screen.PrimaryScreen.Bounds.Height)
            {
                if (mScreen != null)
                    mScreen.Dispose();
                mScreen = new Bitmap(Screen.PrimaryScreen.Bounds.Width,
                                     Screen.PrimaryScreen.Bounds.Height, PixelFormat.Format32bppRgb);
            }
            // Copy screen
            using (Graphics grScreen = Graphics.FromImage(mScreen))
            {
                grScreen.CopyFromScreen(Screen.PrimaryScreen.Bounds.X,
                                Screen.PrimaryScreen.Bounds.Y,
                                0, 0, mScreen.Size,
                                CopyPixelOperation.SourceCopy);

                // Draw the mouse cursor
                bool cursorNeedsDisposing;
                var cursor = CursorInfo.GetCursor(out cursorNeedsDisposing);
                var position = Cursor.Position;
                cursor.Draw(grScreen, new Rectangle(position.X-cursor.HotSpot.X, 
                                                    position.Y-cursor.HotSpot.Y, 
                                                    cursor.Size.Width, cursor.Size.Height));
                if (cursorNeedsDisposing)
                    cursor.Dispose();
            }
            var postCopyTime = DateTime.Now;
            CopyTime = postCopyTime - copyStartTime;

            // Calculate screen size
            if (maxWidth <= 0 || maxWidth > mScreen.Width)
                maxWidth = mScreen.Width;
            if (maxHeight <= 0 || maxHeight >= mScreen.Height)
                maxHeight = mScreen.Height;
            if (maxWidth == mScreen.Width && maxHeight == mScreen.Height)
            {
                // Screen size matches, return the screen as requested
                mScale = 1;
                ShrinkTime = new TimeSpan();
                var screen = mScreen;
                mScreen = null;
                return screen;
            }
            // Scale the screen down to size
            mScale = Math.Min(maxWidth/(double)mScreen.Width, maxHeight/(double)mScreen.Height);
            int width = (int)(mScale * mScreen.Width);
            int height = (int)(mScale * mScreen.Height);
            var scaledBm = new Bitmap(width, height, PixelFormat.Format32bppRgb);
            using (Graphics grScaled = Graphics.FromImage(scaledBm))
            {
                grScaled.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Bilinear;
                grScaled.DrawImage(mScreen, new RectangleF(0, 0, scaledBm.Width, scaledBm.Height), new RectangleF(0, 0, mScreen.Width, mScreen.Height), GraphicsUnit.Pixel);
            }
            ShrinkTime = DateTime.Now - postCopyTime;
            return scaledBm;
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
