// Copyright (C) 2016 by Jeremy Spiller, all rights reserved.

using System;
using System.Windows.Forms;

namespace Gosub.Webtop
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new FormMain());
        }
    }
}
