// Copyright (C) 2016 by Jeremy Spiller, all rights reserved.

using System;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Gosub.Http;

namespace Gosub.Viewtop
{
    public partial class FormMain : Form
    {
        const int HTTP_PORT = 8151;
        const int HTTPS_PORT = 8152;
        const string BROADCAST_HEADER = "OVT:";

        const int PURGE_PEER_AFTER_EXIT_MS = 6000;
        const int PURGE_PEER_LOST_CONNECTION_MS = 10000;
        const int PROBLEM_PEER_TIME_MS = 3000;

        HttpServer mHttpServer;
        HttpServer mHttpsServer;

        ViewtopServer mOvtServer = new ViewtopServer();
        Beacon mBeacon = new Beacon();
        PeerInfo mPeerInfo = new PeerInfo();
        Settings mSettings = new Settings();

        // This is only used to track peer indices.  TBD: The beacon could do this for us
        List<string> mPeers = new List<string>();
        List<ViewtopServer.ComputerInfo> mRemotes = new List<ViewtopServer.ComputerInfo>(); // Track with mPeers index and gridRemote

        class PeerInfo : Beacon.Info
        {
            public string ComputerName = "";
            public string Name = "";
            public string HttpsPort = "";
            public string HttpPort = "";
        }

        public FormMain()
        {
            InitializeComponent();
        }

        private void FormMain_Load(object sender, EventArgs e)
        {
            Text = App.Name + ", version " + App.Version;
            MouseAndKeyboard.MainForm = this;
            Clip.MainForm = this;
        }

        private void FormMain_Shown(object sender, EventArgs e)
        {
            try
            {
                Show();
                Refresh();

                CheckAppDataDirectory();
                mSettings = Settings.Load();
                textName.Text = mSettings.Name;
                LoadUserFileFirstTime();
                StartWebServer();
                StartBeacon();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Error loading application: " + ex.Message, App.Name);
                Application.Exit();
            }
        }

        private void FormMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            try { mHttpServer.Stop(); } catch { }
            try { mHttpsServer.Stop(); } catch { }
            try { mBeacon.Stop(); } catch {  }
            try { Settings.Save(mSettings); } catch { }
        }

        private void CheckAppDataDirectory()
        {
            try
            {
                // On Mono, the user will have to create this directory
                var appDataDir = Application.CommonAppDataPath;
                if (!Directory.Exists(appDataDir))
                    Directory.CreateDirectory(appDataDir);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Please create this directrory with appropriate pemissions.  " 
                    + "Error: " + ex.Message, App.Name);
                throw;
            }
        }

        private void LoadUserFileFirstTime()
        {
            var userFile = LoadUserFile();
            LoadUserNamesListBox(userFile);

            // Ask to create a new user if there are none
            if (userFile.Users.Count == 0)
            {
                CreateNewUser(userFile, "You must create a user name and password:");
                SaveUserFile(userFile);
                if (userFile.Users.Count == 0)
                    MessageBox.Show(this, "No one will be able to log on to the server until you create a username and password!", App.Name);
                LoadUserNamesListBox(userFile);
            }
        }

        private void StartBeacon()
        {
            try
            {
                try { mPeerInfo.ComputerName = Dns.GetHostName(); }
                catch { MessageBox.Show(this, "Error getting Dns host name.", App.Name); }
                mPeerInfo.Name = textName.Text;
                mPeerInfo.HttpsPort = HTTPS_PORT.ToString();
                mPeerInfo.HttpPort = HTTP_PORT.ToString();
                mBeacon.Start(BROADCAST_HEADER, mPeerInfo);
                mBeacon.PeerAdded += mBeacon_PeerAdded;
                mBeacon.PeerRemoved += mBeacon_PeerRemoved;
                mBeacon.PeerConnectionEstablishedChanged += mBeacon_PeerConnectionEstablishedChanged;
                timerBeacon.Enabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Error starting beacon: " + ex.Message, App.Name);
            }
            if (timerBeacon.Enabled && mBeacon.GetBroadcastAddresses().Length == 0)
            {
                MessageBox.Show(this, "Warning: No networks were detected.");
            }
        }

        UserFile LoadUserFile()
        {
            try
            {
                return UserFile.Load();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Error loading user files: " + ex.Message);
            }
            return new UserFile();
        }

        void SaveUserFile(UserFile users)
        {
            try
            {
                UserFile.Save(users);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Error loading user files: " + ex.Message);
            }
        }

        void LoadUserNamesListBox(UserFile userFile)
        {
            listUsers.Items.Clear();
            foreach (var user in userFile.Users)
                listUsers.Items.Add(user.UserName);
            UpdateGui();
        }

        void UpdateGui()
        {
            buttonDeleteUser.Enabled = listUsers.SelectedIndex >= 0;
            buttonChangePassword.Enabled = listUsers.SelectedIndex >= 0;
        }

        void CreateNewUser(UserFile userFile, string message)
        {
            var passwordForm = new FormPassword();
            passwordForm.Message = message;
            passwordForm.ShowDialog(this);
            if (!passwordForm.Accepted)
                return;

            var user = userFile.Find(passwordForm.UserName);
            if (user != null)
            {
                MessageBox.Show(this, "This user already exists", App.Name);
                return;
            }

            // Create and save new user
            user = new User();
            user.UserName = passwordForm.UserName;
            user.ResetPassword(passwordForm.Password);
            userFile.Users.Add(user);
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

        void StartWebServer()
        {
            if (mHttpServer != null)
                mHttpServer.Stop();
            if (mHttpsServer != null)
                mHttpsServer.Stop();

            // Get machine name
            string machineName = "";
            try { machineName = Dns.GetHostName(); }
            catch { }
            if (machineName.Trim() == "")
                machineName = "localhost";
            if (mSettings.Name.Trim() == "")
            {
                mSettings.Name = machineName;
                textName.Text = machineName;
            }

            labelSecureLink.Text = "Starting...";
            Refresh();


            mOvtServer = new ViewtopServer();
            mOvtServer.LocalComputerInfo.ComputerName = machineName;
            mOvtServer.LocalComputerInfo.Name = mSettings.Name;

            try
            {
                // Setup HTTP server
                var certificate = GetCertificate();
                mHttpServer = new HttpServer();
                mHttpServer.Start(new TcpListener(IPAddress.Any, HTTP_PORT),
                    (context) => { return mOvtServer.ProcessWebRemoteViewerRequest(context); });
                mOvtServer.LocalComputerInfo.HttpPort = HTTP_PORT.ToString();

                // Setup HTTPS server
                mHttpsServer = new HttpServer();
                mHttpsServer.UseSsl(certificate);
                mHttpsServer.Start(new TcpListener(IPAddress.Any, HTTPS_PORT),
                    (context) => { return mOvtServer.ProcessWebRemoteViewerRequest(context); });
                mOvtServer.LocalComputerInfo.HttpsPort = HTTPS_PORT.ToString();
            }
            catch (Exception ex)
            {
                try { mHttpServer.Stop(); } catch { }
                try { mHttpsServer.Stop(); } catch { }
                MessageBox.Show(this, "Error starting web server: " + ex.Message, App.Name);
                labelSecureLink.Text = "Error starting server";
                return;
            }

            string link = "https://" + machineName + ":" + HTTPS_PORT;
            string text = "";
            labelSecureLink.Text = text + link;
            labelSecureLink.Links.Clear();
            labelSecureLink.Links.Add(text.Length, link.Length, link);
            labelSecureLink.Enabled = true;

            link = "http://" + machineName + ":" + HTTP_PORT;
            text = "";
            labelUnsecureLink.Text = text + link;
            labelUnsecureLink.Links.Clear();
            labelUnsecureLink.Links.Add(text.Length, link.Length, link);
            labelUnsecureLink.Visible = true;

            buttonStop.Enabled = true;
            buttonStart.Enabled = false;

            GetPublicIpAddress();

        }

        async void GetPublicIpAddress()
        {
            try
            {
                using (var http = new System.Net.Http.HttpClient())
                {
                    var page = await http.GetStringAsync("http://ip4.me");
                    var regex = new System.Text.RegularExpressions.Regex(@"\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\b");
                    var match = regex.Match(page);
                    if (match.Success)
                    {
                        labelPublicIpAddress.Text = "Public IP address: " + match.Value;
                        mOvtServer.LocalComputerInfo.PublicIp = match.Value;
                    }
                }
            }
            catch
            {
                labelPublicIpAddress.Text = "Could not obtain public IP address";
            }
        }

        void StopWebServer()
        {
            if (mHttpServer != null)
                mHttpServer.Stop();
            if (mHttpsServer != null)
                mHttpsServer.Stop();
            mHttpServer = null;
            mHttpsServer = null;
            mOvtServer = new ViewtopServer();

            labelSecureLink.Text = "Web Server stopped";
            labelSecureLink.Enabled = false;
            labelUnsecureLink.Text = "Web server stopped";
            labelUnsecureLink.Visible = false;

            buttonStop.Enabled = false;
            buttonStart.Enabled = true;
        }


        X509Certificate2 GetCertificate()
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
                return new X509Certificate2(pfx, password,
                    X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Error setting up secure link.  Your HTTPS connection may not work.  \r\n\r\n" + ex.Message, App.Name);
            }
            return null;
        }

        private void listUsers_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateGui();
        }

        private void buttonNewUser_Click(object sender, EventArgs e)
        {
            var userFile = LoadUserFile();
            CreateNewUser(userFile, "Create a new user:");
            SaveUserFile(userFile);
            LoadUserNamesListBox(userFile);
        }

        private void buttonDeleteUser_Click(object sender, EventArgs e)
        {
            if (listUsers.SelectedIndex < 0)
                return;
            if (MessageBox.Show(this, "Are you sure you want to delte this user?", App.Name, MessageBoxButtons.YesNo) == DialogResult.No)
                return;
            var userFile = LoadUserFile();
            userFile.Remove((string)listUsers.Items[listUsers.SelectedIndex]);
            SaveUserFile(userFile);
            LoadUserNamesListBox(userFile);
        }

        private void buttonChangePassword_Click(object sender, EventArgs e)
        {
            if (listUsers.SelectedIndex < 0)
                return;
            var userFile = LoadUserFile();
            var user = userFile.Find((string)listUsers.Items[listUsers.SelectedIndex]);
            if (user == null)
                return;
             
            var passwordForm = new FormPassword();
            passwordForm.Message = "Enter new password for " + user.UserName + ":";
            passwordForm.UserName = user.UserName;
            passwordForm.UserNameReadOnly = true;
            passwordForm.ShowDialog(this);
            if (!passwordForm.Accepted)
                return;
            user.ResetPassword(passwordForm.Password);
            SaveUserFile(userFile);
        }

        private void timerBeacon_Tick(object sender, EventArgs e)
        {
            mBeacon.Update();
        }

        private void textName_TextChanged(object sender, EventArgs e)
        {
            mPeerInfo.Name = textName.Text;
            mSettings.Name = textName.Text;
            mOvtServer.LocalComputerInfo.Name = textName.Text;
        }

        private void textName_Leave(object sender, EventArgs e)
        {
            Settings.Save(mSettings);
        }

        // Called when a new peer is detected
        void mBeacon_PeerAdded(Beacon.Peer peer)
        {
            // Ignore this compurer
            if (peer.ThisBeacon)
                return;

            // Add row to grid
            mPeers.Add(peer.Key);
            gridRemote.Rows.Add("", "", "", "");
            mRemotes.Add(new ViewtopServer.ComputerInfo());
            RefreshPeerGrid();
        }
        
        // Called when an old peer is dropped
        void mBeacon_PeerRemoved(Beacon.Peer removedPeer)
        {
            // Remove row from grid, adjust index of peers to be in proper spot
            int index = mPeers.FindIndex((m) => (m == removedPeer.Key));
            if (index < 0)
                return;
            mPeers.RemoveAt(index);
            gridRemote.Rows.RemoveAt(index);
            mRemotes.RemoveAt(index);
        }

        private void mBeacon_PeerConnectionEstablishedChanged(Beacon.Peer peer)
        {
            RefreshPeerGrid();
        }

        private void timerUpdateRemoteGrid_Tick(object sender, EventArgs e)
        {
            RefreshPeerGrid();

            // Display local IP address
            List<string> ipAddressList = new List<string>();
            string ipAddresses = "";
            foreach (var ip in mBeacon.GetLocalAddresses())
            {
                if (ipAddresses != "")
                    ipAddresses += ", ";
                ipAddresses += ip.ToString();
                ipAddressList.Add(ip.ToString());
            }
            labelLocalIpAddress.Text = "Local IP addresses: " + (ipAddresses == "" ? "(unknown)" : ipAddresses);
            mOvtServer.LocalComputerInfo.LocalIp = ipAddressList.Count == 0 ? "" : ipAddressList[0];
            mOvtServer.LocalComputerInfo.LocalIps = ipAddressList.ToArray();
        }

        private void RefreshPeerGrid()
        {
            // Update list of remote computers
            var now = DateTime.Now;
            mBeacon.PurgePeers(PURGE_PEER_AFTER_EXIT_MS, PURGE_PEER_LOST_CONNECTION_MS);
            var peers = mBeacon.GetPeers();

            foreach (var peer in peers)
            {
                // Ignore any peers not in the grid
                int rowIndex = mPeers.FindIndex((m) => (m == peer.Key));
                if (rowIndex < 0)
                    continue;

                // Get peer status and color
                var info = (PeerInfo)peer.Info;
                var lastTimeReceived = (now - peer.TimeReceived).TotalMilliseconds;
                string status;
                Color color;
                if (info.State == Beacon.State.Exit)
                {
                    status = "Exiting...";
                    color = Color.Blue;
                }
                else if (lastTimeReceived > PROBLEM_PEER_TIME_MS)
                {
                    status = "Lost connection!";
                    color = Color.Red;
                }
                else if (!peer.ConnectionEstablished)
                {
                    status = "Connecting...";
                    color = Color.Gray;
                }
                else if (peer.UsingNat)
                {
                    status = "Using NAT";
                    color = Color.Red;
                }
                else
                {
                    status = "Ok";
                    color = Color.Black;
                }

                // Add grid row
                // NOTE: Using the computer name doesn't always work because our DNS server may
                //       not have resolved it yet.  So, for now, use the IP address instead.
                string linkComputerName = "https://" + peer.EndPoint.Address + ":" + info.HttpsPort;
                string linkIpAddress = "https://" + peer.EndPoint.Address + ":" + info.HttpsPort;
                var name = info.Name.Trim() == "" ? "" : " (" + info.Name + ")";
                gridRemote[0, rowIndex].Value = new GridLink(info.Name, linkComputerName);
                gridRemote[1, rowIndex].Value = new GridLink(info.ComputerName, linkComputerName);
                gridRemote[2, rowIndex].Value = new GridLink(peer.EndPoint.Address.ToString(), linkIpAddress);
                gridRemote[3, rowIndex].Value = new GridLink(status, linkIpAddress);
                gridRemote[0, rowIndex].Style.ForeColor = color;
                gridRemote[1, rowIndex].Style.ForeColor = color;
                gridRemote[2, rowIndex].Style.ForeColor = color;
                gridRemote[3, rowIndex].Style.ForeColor = color;
                var remote = mRemotes[rowIndex];
                remote.Name = info.Name;
                remote.ComputerName = info.ComputerName;
                remote.LocalIp = peer.EndPoint.Address.ToString();
                remote.HttpPort = info.HttpPort;
                remote.HttpsPort = info.HttpsPort;
                remote.Status = status;
            }
            mOvtServer.RemoteComputerInfo = mRemotes.ToArray();
        }

        private void gridRemote_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
                return;
            Process.Start(((GridLink)gridRemote[e.ColumnIndex, e.RowIndex].Value).Link);
            while (gridRemote.SelectedRows.Count != 0)
                gridRemote.SelectedRows[0].Selected = false;
        }

        private void buttonRefreshRemoteComputers_Click(object sender, EventArgs e)
        {
            mBeacon.Stop();
            mBeacon = new Beacon();
            mPeers.Clear();
            gridRemote.Rows.Clear();
            StartBeacon();
            GetPublicIpAddress();
        }

        /// <summary>
        /// Helper class for the grid
        /// </summary>
        class GridLink
        {
            public string Text = "";
            public string Link = "";
            public GridLink(string text, string link)
            {
                Text = text;
                Link = link;
            }
            public override string ToString()
            {
                return Text;
            }
        }

    }
}
