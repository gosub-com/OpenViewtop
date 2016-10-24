// Copyright (C) 2016 by Jeremy Spiller, all rights reserved.

using System;
using System.Drawing;
using System.Windows.Forms;
using System.IO;

namespace Gosub.WebRemoteViewer
{
    public partial class FormMain : Form
    {
        FileServer mFileServer;
        WrvHandler mWrvServer;
        FrameCollector mCollector;
        FrameAnalyzer mAnalyzer;

        string[] prefixes = { "http://*:8089/", "https://*:8443/" };

        public FormMain()
        {
            InitializeComponent();
        }

        private void FormMain_Load(object sender, EventArgs e)
        {
            labelLinkToWebSite.Text = "Starting Web Server";
            labelLinkToWebSite.Enabled = false;
            labelStats.Text = "";
        }

        private void Jrfb_Shown(object sender, EventArgs e)
        {
            StartWebServer();
        }

        private void buttonStart_Click(object sender, EventArgs e)
        {
            StartWebServer();
        }

        private void buttonStop_Click(object sender, EventArgs e)
        {
            StopWebServer();
        }

        private void labelLinkToWebSite_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (e.Link.LinkData != null)
                System.Diagnostics.Process.Start(e.Link.LinkData.ToString());
        }

        void StartWebServer()
        {
            if (mFileServer != null)
                mFileServer.Stop();
            try
            {
                mFileServer = new FileServer("http://localhost:8080/", Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "www"));
                mWrvServer = new WrvHandler();
                mCollector = new FrameCollector();
                mAnalyzer = new FrameAnalyzer();
                var wrvServer = mWrvServer; // Do not capture the field, only the local
                mFileServer.SetRequestHandler("wrv", (context) => { wrvServer.ProcessWebRemoteViewerRequest(context); } );
                mFileServer.Start();
            }
            catch (Exception ex)
            {
                mFileServer = null;
                MessageBox.Show(this, "Error starting web server: " + ex.Message);
                return;
            }

            string link = "http://localhost:8080";
            string text = "Web server running: ";
            labelLinkToWebSite.Text = text + link;
            labelLinkToWebSite.Links.Clear();
            labelLinkToWebSite.Links.Add(text.Length, link.Length, link);
            labelLinkToWebSite.Enabled = true;
            buttonStop.Enabled = true;
            buttonStart.Enabled = false;
        }

        void StopWebServer()
        {
            if (mFileServer != null)
                mFileServer.Stop();
            mFileServer = null;
            mWrvServer = null;
            mCollector = null;
            mAnalyzer = null;

            labelLinkToWebSite.Text = "Web server stopped";
            buttonStop.Enabled = false;
            buttonStart.Enabled = true;
        }

        private void timerRefresh_Tick(object sender, EventArgs e)
        {
            if (mFileServer == null)
            {
                labelStats.Text = "";
                return;
            }

            DateTime t1 = DateTime.Now;
            Bitmap bm = mCollector.CreateFrame();

            DateTime t2 = DateTime.Now;
            mAnalyzer.NoCompression = checkNoCompression.Checked;
            mAnalyzer.SuppressBackgroundCompare = checkSuppressBackgroundCompare.Checked;
            mAnalyzer.SmartPng = checkSmartPng.Checked;

            Stream image;
            string draw;
            mAnalyzer.AnalyzeFrame(bm, out image, out draw);
            bm.Dispose();

            DateTime t3 = DateTime.Now;

            mWrvServer.SetDraw(image, draw);

            labelStats.Text = "Collect time: " + (t2 - t1).Milliseconds + "\r\n"
                              + "Analyze time: " + (t3 - t2).Milliseconds + "\r\n"
                              + "Score time: " + mAnalyzer.ScoreTime.Milliseconds + "\r\n"
                              + "Create time: " + mAnalyzer.CreateTime.Milliseconds + "\r\n"
                              + "Compress time: " + mAnalyzer.CompressTime.Milliseconds + "\r\n"
                              + "Duplicates: " + mAnalyzer.DuplicateBlocks + "\r\n"
                              + "Hash collisions: " + mAnalyzer.HashCollisionsEver;
        }

    }
}
