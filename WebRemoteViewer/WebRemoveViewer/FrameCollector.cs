// Copyright (C) 2016 by Jeremy Spiller, all rights reserved.

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace Gosub.WebRemoteViewer
{
    class FrameCollector
    {

        public Bitmap CreateFrame()
        {
            DateTime start = DateTime.Now;
            var bm = new Bitmap(Screen.PrimaryScreen.Bounds.Width,
                                Screen.PrimaryScreen.Bounds.Height, PixelFormat.Format32bppArgb);
            using (Graphics gr = Graphics.FromImage(bm))
            {
                gr.CopyFromScreen(Screen.PrimaryScreen.Bounds.X,
                                    Screen.PrimaryScreen.Bounds.Y,
                                    0, 0,
                                    bm.Size,
                                    CopyPixelOperation.SourceCopy);
            }
            return bm;
        }
    }
}
