// Copyright (C) 2016 by Jeremy Spiller, all rights reserved.

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using System.Threading;

namespace Gosub.Viewtop
{
    class FrameCollector
    {
        Bitmap mScreen;

        public TimeSpan CopyTime { get; set; }
        public TimeSpan ShrinkTime { get; set; }

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
                ShrinkTime = new TimeSpan();
                var screen = mScreen;
                mScreen = null;
                return screen;
            }
            // Scale the screen down to size
            double scale = Math.Min(maxWidth/(double)mScreen.Width, maxHeight/(double)mScreen.Height);
            int width = (int)(scale*mScreen.Width);
            int height = (int)(scale*mScreen.Height);
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
}
