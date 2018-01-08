// Copyright (C) 2016 by Jeremy Spiller, all rights reserved.

using System;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using Gosub.Http;
using OpenViewtopServer;

namespace Gosub.Viewtop
{
    public partial class FormMain : Form
    {
        const int HTTP_PORT = 8151;
        const int HTTPS_PORT = 8152;
        const int HTTP_PORT_DEBUG = 8153;
        const int HTTPS_PORT_DEBUG = 8154;

        const string BROADCAST_HEADER = "OVT:";

        const int PURGE_PEER_AFTER_EXIT_MS = 6000;
        const int PURGE_PEER_LOST_CONNECTION_MS = 10000;
        const int PROBLEM_PEER_TIME_MS = 3000;

        public string ControlPipe { get; set; } = "";
        public Mutex ShowOnTopMutex;

        bool mDebug { get; set; } = Debugger.IsAttached;
        bool mShowOnTopMutexLastState;

        // The tray icon doesn't get displayed if the user account
        // is still booting, so keep retrying for a while
        // TBD: Find a better way to do this
        const int TRAY_ICON_RETRIES = 30;
        const int TRAY_ICON_RETRY_TIME = 5;
        int mTrayIconRetryCount;

        int mHttpPort;
        int mHttpsPort;
        HttpServer mHttpServer;

        ViewtopServer mOvtServer = new ViewtopServer();
        Beacon mBeacon;
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
            Text = App.Name + ", version " + App.Version + (mDebug ? " - DEBUG" : "");
            labelSecureLink.Text = "Starting...";
            labelUnsecureLink.Text = "";
            notifyIcon.Icon = Icon;
            MouseAndKeyboard.GuiThreadControl = this;
            Clip.GuiThreadControl = this;
            mHttpPort = mDebug ? HTTP_PORT_DEBUG : HTTP_PORT;
            mHttpsPort = mDebug ? HTTPS_PORT_DEBUG : HTTPS_PORT;
            if (!mDebug)
                Hide();
        }

        private void FormMain_Shown(object sender, EventArgs e)
        {
            try
            {
                if (!mDebug)
                    Hide();
                CheckAppDataDirectory();
                mSettings = Settings.Load();
                textName.Text = mSettings.Name;
                LoadUserFileFirstTime();
            }
            catch (Exception ex)
            {
                Log.Write("Application start error", ex);
                MessageBox.Show(this, "Error loading application: " + ex.Message, App.Name);
                Application.Exit();
            }
            timerRunSystem.Interval = 10;
            timerRunSystem.Enabled = true;
            timerUpdateBeacon.Enabled = true;
            ExecuteNamedPipeCommand();
        }

        private void FormMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing && !mDebug)
            {
                Hide();
                e.Cancel = true;
                return;
            }
            try { mHttpServer.Stop(); } catch { }
            try { mBeacon.Stop(); } catch {  }
            try { Settings.Save(mSettings); } catch { }
        }

        /// <summary>
        /// This is needed since the form can display under other stuff when the user
        /// clicks the tray icon or clicks the executable to show this form
        /// </summary>
        void ShowOnTop()
        {
            TopMost = true;
            Show();
            BringToFront();
            TopMost = false;
        }

        // Use pipe to exit process gracefully, rather than killing from service
        async void ExecuteNamedPipeCommand()
        {
            if (ControlPipe == "")
                return;

            Log.Write("Named pipe: " + ControlPipe);
            try
            {
                var np = new NamedPipeClientStream(ControlPipe);
                await Task.Run(() => { np.Connect(); });
                Log.Write("Named pipe connected");
                var control = new StreamReader(np);
                while (true)
                {
                    var message = await control.ReadLineAsync();
                    if (message == "close")
                        Application.Exit();
                }
            }
            catch (Exception ex)
            {
                Log.Write("Named pipe exception", ex);
            }
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
                Log.Write("CheckAppDataDirectory", ex);
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
            if (mBeacon != null)
                return;

            try
            {
                mPeerInfo.ComputerName = Dns.GetHostName();
                mPeerInfo.Name = textName.Text;
                mPeerInfo.HttpsPort = mHttpsPort.ToString();
                mPeerInfo.HttpPort = mHttpPort.ToString();
                mBeacon = new Beacon();
                mBeacon.Start(BROADCAST_HEADER, mPeerInfo);
                mBeacon.PeerAdded += mBeacon_PeerAdded;
                mBeacon.PeerRemoved += mBeacon_PeerRemoved;
                mBeacon.PeerConnectionEstablishedChanged += mBeacon_PeerConnectionEstablishedChanged;
            }
            catch (Exception ex)
            {
                try { if (mBeacon != null) mBeacon.Stop(); }
                catch { }
                mBeacon = null;
                Log.Write("StartBeacon: ", ex);
                throw;
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
                Log.Write("LoadUserFile", ex);
                MessageBox.Show(this, "Error loading user files: " + ex.Message, App.Name);
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
                Log.Write("SaveUserFile", ex);
                MessageBox.Show(this, "Error loading user files: " + ex.Message, App.Name);
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
                return;

            labelSecureLink.Text = "Starting...";
            Refresh();
            string machineName = "";
            try
            {
                // Get machine name
                machineName = Dns.GetHostName();
                if (machineName.Trim() == "")
                    machineName = "localhost";
                if (mSettings.Name.Trim() == "")
                {
                    mSettings.Name = machineName;
                    textName.Text = machineName;
                }
                mOvtServer = new ViewtopServer();
                mOvtServer.LocalComputerInfo.ComputerName = machineName;
                mOvtServer.LocalComputerInfo.Name = mSettings.Name;

                // Setup HTTP server
                mHttpServer = new HttpServer();
                mHttpServer.HttpHandler += (context) => 
                {
                    if (context.Request.Target == "/ovt/log")
                        return context.SendResponseAsync(Log.GetAsString(200));
                    return mOvtServer.ProcessOpenViewtopRequestAsync(context);
                };
                mHttpServer.Start(new TcpListener(IPAddress.Any, mHttpPort));
                mOvtServer.LocalComputerInfo.HttpPort = mHttpPort.ToString();

                // Setup HTTPS connection
                mHttpServer.Start(new TcpListener(IPAddress.Any, mHttpsPort), Util.GetCertificate());
                mOvtServer.LocalComputerInfo.HttpsPort = mHttpsPort.ToString();
            }
            catch (Exception ex)
            {
                Log.Write("StartWebServer", ex);
                try { mHttpServer.Stop(); } catch { }
                mHttpServer = null;
                labelSecureLink.Text = "Error: " + ex.Message;
                throw;
            }

            string link = "https://" + machineName + ":" + mHttpsPort;
            string text = "";
            labelSecureLink.Text = text + link;
            labelSecureLink.Links.Clear();
            labelSecureLink.Links.Add(text.Length, link.Length, link);
            labelSecureLink.Enabled = true;

            link = "http://" + machineName + ":" + mHttpPort;
            text = "";
            labelUnsecureLink.Text = text + link;
            labelUnsecureLink.Links.Clear();
            labelUnsecureLink.Links.Add(text.Length, link.Length, link);
            labelUnsecureLink.Visible = true;

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
            mHttpServer = null;
            mOvtServer = new ViewtopServer();

            labelSecureLink.Text = "Web Server stopped";
            labelSecureLink.Enabled = false;
            labelUnsecureLink.Text = "Web server stopped";
            labelUnsecureLink.Visible = false;
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
            if (MessageBox.Show(this, "Are you sure you want to delete this user?", App.Name, MessageBoxButtons.YesNo) == DialogResult.No)
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

        private void timerUpdateBeacon_Tick(object sender, EventArgs e)
        {
            if (ShowOnTopMutex != null)
            {
                // Show this form on the rising edge of not being able to get
                // the mutex, which means another instance (see Program.cs)
                // is signalling this server to show itself.
                var showOnTopMutexState = ShowOnTopMutex.WaitOne(0);
                if (showOnTopMutexState)
                    ShowOnTopMutex.ReleaseMutex();
                if (!showOnTopMutexState && mShowOnTopMutexLastState)
                    ShowOnTop();
                mShowOnTopMutexLastState = showOnTopMutexState;
            }

            if (mBeacon != null)
                mBeacon.Update();
        }

        private void timerRunSystem_Tick(object sender, EventArgs e)
        {
            timerRunSystem.Interval = 1000;

            // The tray icon doesn't show if the user account is still booting
            if (mTrayIconRetryCount <= TRAY_ICON_RETRIES)
            {
                mTrayIconRetryCount++;
                if (mTrayIconRetryCount % TRAY_ICON_RETRY_TIME == 0)
                {
                    notifyIcon.Visible = false;
                    notifyIcon.Visible = true;
                }
            }

            // Retry starting the servers until it works
            try
            {
                StartBeacon();
                StartWebServer();
            }
            catch (Exception ex)
            {
                Log.Write("Error starting server", ex);
            }
            if (mBeacon == null || mHttpServer == null)
                return;

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


        private void notifyIcon_Click(object sender, EventArgs e)
        {
            ShowOnTop();
        }

    }
}
