// Copyright (C) 2016 by Jeremy Spiller, all rights reserved.

using System;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Diagnostics;
using System.Reflection;
using System.Security.Principal;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Gosub.Viewtop
{
    public partial class FormMain : Form
    {
        const string HTTPS_PORT = "8085";
        const string HTTP_PORT = "8086";

        FileServer mFileServer;
        ViewtopServer mOvtServer;
        FrameCollector mCollector;
        FrameCompressor mAnalyzer;

        public FormMain()
        {
            InitializeComponent();
        }

        private void FormMain_Load(object sender, EventArgs e)
        {
            labelSecureLink.Text = "Web Server stopped";
            labelSecureLink.Enabled = false;
            labelUnsecureLink.Text = "Web server stopped";
            labelUnsecureLink.Enabled = false;
            labelLocalIpAddresses.Text = "";
        }

        private void FormMain_Shown(object sender, EventArgs e)
        {
            StartWebServer();
        }

        private void checkAllowUnsecure_CheckedChanged(object sender, EventArgs e)
        {
            StopWebServer();
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

        private void labelSecureLink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (e.Link.LinkData != null)
                Process.Start(e.Link.LinkData.ToString());
        }

        private void labelUnsecureLink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (e.Link.LinkData != null)
                Process.Start(e.Link.LinkData.ToString());
        }

        public static bool IsAdministrator()
        {
            try
            {
                return (new WindowsPrincipal(WindowsIdentity.GetCurrent()))
                        .IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        void StartWebServer()
        {
            labelLocalIpAddresses.Text = "";

            if (!IsAdministrator())
            {
                MessageBox.Show(this, "Error: This application requires administrator privileges", "Viewtop");
                Application.Exit();
                return;
            }

            if (mFileServer != null)
                mFileServer.Stop();

            var httpPrefixes = new List<string>();
            httpPrefixes.Add("https://*:" + HTTPS_PORT + "/");
            if (checkAllowUnsecure.Checked)
                httpPrefixes.Add("http://*:" + HTTP_PORT + "/" );

            SetupSecurePort(HTTPS_PORT);

            try
            {
                // Setup server
                mFileServer = new FileServer(httpPrefixes.ToArray(), Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "www"));
                mOvtServer = new ViewtopServer();
                mCollector = new FrameCollector();
                mAnalyzer = new FrameCompressor();
                var ovtServer = mOvtServer; // Do not capture the field, only the local
                mFileServer.SetRequestHandler("ovt", (context) => { ovtServer.ProcessWebRemoteViewerRequest(context); });
                mFileServer.Start();
            }
            catch (Exception ex)
            {
                mFileServer = null;
                MessageBox.Show(this, "Error starting web server: " + ex.Message);
                return;
            }

            string link = "https://localhost:" + HTTPS_PORT + "/";
            string text = "Secure web server: ";
            labelSecureLink.Text = text + link;
            labelSecureLink.Links.Clear();
            labelSecureLink.Links.Add(text.Length, link.Length, link);
            labelSecureLink.Enabled = true;

            link = "http://localhost:" + HTTP_PORT + "/";
            text = "Unsecure web server: ";
            labelUnsecureLink.Text = text + link;
            labelUnsecureLink.Links.Clear();
            labelUnsecureLink.Links.Add(text.Length, link.Length, link);
            labelUnsecureLink.Enabled = checkAllowUnsecure.Checked;

            buttonStop.Enabled = true;
            buttonStart.Enabled = false;

            // Show local IP addresses
            string ipAddresses = "";
            foreach (var nif in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nif.OperationalStatus != OperationalStatus.Up)
                    continue;
                foreach (var ip in nif.GetIPProperties().UnicastAddresses)
                {
                    if (ip.Address.AddressFamily == AddressFamily.InterNetworkV6)
                        continue;
                    if (ip.Address.ToString() == "127.0.0.1")
                        continue;
                    if (ipAddresses != "")
                        ipAddresses += ",  ";
                    ipAddresses += ip.Address;
                }
            }
            if (ipAddresses == "")
                ipAddresses = "No local IP addresses found";
            else
                ipAddresses = "Local IP address: " + ipAddresses;
            labelLocalIpAddresses.Text = ipAddresses;
        }

        void StopWebServer()
        {
            if (mFileServer != null)
                mFileServer.Stop();
            mFileServer = null;
            mOvtServer = null;
            mCollector = null;
            mAnalyzer = null;

            labelSecureLink.Text = "Web Server stopped";
            labelSecureLink.Enabled = false;
            labelUnsecureLink.Text = "Web server stopped";
            labelUnsecureLink.Enabled = false;

            buttonStop.Enabled = false;
            buttonStart.Enabled = true;
        }


        void SetupSecurePort(string httpsPort)
        {
            try
            {
                // Try reading a previously created pfx
                string pfxPath = Application.CommonAppDataPath + "\\OpenViewTop.pfx";
                byte[] pfx = new byte[0];
                try { pfx = File.ReadAllBytes(pfxPath); }
                catch { }

                // Create the PFX and save it, if necessary
                const string password = "OpenViewTop";
                if (pfx.Length == 0)
                {
                    pfx = PFXGenerator.GeneratePfx("OpenViewTop", password);
                    File.WriteAllBytes(pfxPath, pfx);
                }
                // Save certificate 
                X509Certificate2 certificate = new X509Certificate2(pfx, password,
                    X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);
                X509Store store = new X509Store(StoreLocation.LocalMachine);
                store.Open(OpenFlags.ReadWrite);
                store.Add(certificate);
                store.Close();

                // Setup an invisible shell to install certificates on the SSL port
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.CreateNoWindow = true;
                psi.FileName = "netsh";
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;

                // Delete old certificates
                psi.Arguments = "http delete sslcert ipport=0.0.0.0:" + httpsPort;
                Process.Start(psi).WaitForExit();
                psi.Arguments = "http delete sslcert ipport=[::]:" + httpsPort;
                Process.Start(psi).WaitForExit();

                // Bind SSL certificate to IPV4 port
                string appId = "{" + Assembly.GetExecutingAssembly().GetType().GUID.ToString() + "}";
                psi.Arguments = "http add sslcert ipport=0.0.0.0:" + httpsPort + " certhash=" + certificate.Thumbprint + " appid=" + appId;
                var p1 = Process.Start(psi);
                p1.WaitForExit();

                // Bind SSL certificate to IPV6 port
                psi.Arguments = "http add sslcert ipport=[::]:" + httpsPort + " certhash=" + certificate.Thumbprint + " appid=" + appId;
                var p2 = Process.Start(psi);
                p2.WaitForExit();

                if (p1.ExitCode != 0)
                    throw new Exception("Could add SSL certificate to the port: " + p1.StandardOutput.ReadToEnd());
                if (p2.ExitCode != 0)
                    throw new Exception("Could add SSL certificate to the port: " + p2.StandardOutput.ReadToEnd());
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Error setting up secure link.  Your HTTPS connection may not work.  \r\n\r\n" + ex.Message, "Viewtop");
            }
        }

    }
}
