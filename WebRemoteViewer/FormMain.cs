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
        WrvServer mWrvServer;
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
                mWrvServer = new WrvServer();
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
            FrameAnalyzer.NoCompression = checkNoCompression.Checked;
            FrameAnalyzer.SuppressBackgroundCompare = checkSuppressBackgroundCompare.Checked;
            FrameAnalyzer.SmartPng = checkSmartPng.Checked;
        }

    }
}
