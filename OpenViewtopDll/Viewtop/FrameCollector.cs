﻿// Copyright (C) 2016 by Jeremy Spiller, all rights reserved.

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using static Gosub.Viewtop.NativeMethods;

namespace Gosub.Viewtop
{
    class FrameCollector : IDisposable
    {
        static Font sErrorFont = new Font("Arial", 32);
        bool mIsScaledScreen;
        Bitmap mFullScreen;
        Bitmap mScaledScreen;
        double mScale = 1;

        public TimeSpan CopyTime { get; set; }
        public TimeSpan ShrinkTime { get; set; }
        public double Scale { get { return mScale; }  }
        public Bitmap Screen => mIsScaledScreen ? mScaledScreen : mFullScreen;

        public void Dispose()
        {
            if (mFullScreen != null)
                mFullScreen.Dispose();
            mFullScreen = null;
            if (mScaledScreen != null)
                mScaledScreen.Dispose();
            mScaledScreen = null;
        }

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
                try
                {
                    grScreen.CopyFromScreen(System.Windows.Forms.Screen.PrimaryScreen.Bounds.X,
                                            System.Windows.Forms.Screen.PrimaryScreen.Bounds.Y,
                                            0, 0, mFullScreen.Size,
                                            CopyPixelOperation.SourceCopy);
                }
                catch (Exception ex)
                {
                    // Can't copy screen when showing UAC or login
                    grScreen.Clear(Color.Black);
                    grScreen.DrawString("Can't copy the screen: " + ex.Message, sErrorFont, Brushes.Red, new PointF(0, 0));
                }

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
                cursorInfo.StructSize = Marshal.SizeOf(typeof(CURSORINFO));
                GetCursorInfo(out cursorInfo);
                if (cursorInfo.Cursof != IntPtr.Zero)
                {
                    var cursor = new Cursor(cursorInfo.Cursof);

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

    }
}