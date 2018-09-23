// Copyright (C) 2016 by Jeremy Spiller, all rights reserved.

using System;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using Gosub.Http;
using Gosub.Viewtop;

namespace Gosub.OpenViewtopServer
{
    public partial class FormGui : Form
    {
        const int PORT_FORWARD_TO_HTTPS = 8151;
        const int PORT_HTTPS = 8152;
        const int PORT_HTTP = 8153;

        const int PORT_FORWARD_TO_HTTPS_DEBUG = 8161;
        const int PORT_HTTPS_DEBUG = 8162;
        const int PORT_HTTP_DEBUG = 8163;

        const string BROADCAST_HEADER = "OVT:";

        const int PURGE_PEER_AFTER_EXIT_MS = 6000;
        const int PURGE_PEER_LOST_CONNECTION_MS = 10000;
        const int PROBLEM_PEER_TIME_MS = 3000;

        // gridRemote columns
        const int GR_COL_NAME = 0;
        const int GR_COL_COMPUTER = 1;
        const int GR_COL_IP = 2;
        const int GR_COL_HTTP_PORT = 3;
        const int GR_COL_HTTPS_PORT = 4;
        const int GR_COL_STATUS = 5;
        const int GR_COL_COUNT = 6;

        bool mDebug { get; set; } = Debugger.IsAttached;
        bool mShowOnTopMutexLastState;

        // The tray icon doesn't get displayed if the user account
        // is still booting, so keep retrying for a while
        // TBD: Find a better way to do this
        const int TRAY_ICON_RETRIES = 30;
        const int TRAY_ICON_RETRY_TIME = 5;
        int mTrayIconRetryCount;

        bool mExiting;
        ProcessManager mProcessManager;

        int mPortForwardToHttps;
        int mPortHttp;
        int mPortHttps;
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

        public FormGui()
        {
            InitializeComponent();
        }

        private async void FormGui_Load(object sender, EventArgs e)
        {
            var closeTask = Task.Run(async () => await ProcessServerCommands());

            Text = App.Name + ", version " + App.Version + (mDebug ? " - DEBUG" : "");
            labelHttpsLink.Text = "Starting...";
            labelHttpLink.Text = "";
            MouseAndKeyboard.GuiThreadControl = this;
            Clip.GuiThreadControl = this;
            mPortForwardToHttps = mDebug ? PORT_FORWARD_TO_HTTPS_DEBUG : PORT_FORWARD_TO_HTTPS;
            mPortHttps = mDebug ? PORT_HTTPS_DEBUG : PORT_HTTPS;
            mPortHttp = mDebug ? PORT_HTTP_DEBUG : PORT_HTTP;
            if (mDebug)
            {
                Icon = Properties.Resources.OpenViewtopDebug;
                ShowInTaskbar = true;
            }
            notifyIcon.Icon = Icon;
            tabMain.BackColor = Color.Blue;

            if (await closeTask)
                ApplicationExit("@reason=Closed by service");
        }


        private void FormGui_Shown(object sender, EventArgs e)
        {
            try
            {
                // Hide form to start, the setup to show.  This is needed to prevent flickering.
                Hide();
                Opacity = 1;
                FormBorderStyle = FormBorderStyle.Fixed3D;
                if (mDebug)
                    Show();

                CheckAppDataDirectory();
                mSettings = Settings.Load();
                textNickname.Text = mSettings.Nickname;
                checkBroadcast.Checked = mSettings.EnableBeaconBroadcast;
                LoadUserFileFirstTime();
            }
            catch (Exception ex)
            {
                Log.Write("Application start error", ex);
                MessageBox.Show(this, "Error loading Open Viewtop: " + ex.Message, App.Name);
            }
            timerRunSystem.Interval = 10;
            timerRunSystem.Enabled = true;

            NetworkChange.NetworkAddressChanged += (p1, p2) => { GetPublicIpAddress(); };
            GetPublicIpAddress();
        }

        /// <summary>
        /// Process server commands.  Returns TRUE if the application should exit.
        /// </summary>
        async Task<bool> ProcessServerCommands()
        {
            if (Program.ParamControlPipe == "")
                return false;

            try
            {
                Log.Write("Starting pipe...");
                mProcessManager = new ProcessManager(Program.ParamControlPipe, false);
                Log.Write("Pipe connected");
                await mProcessManager.SendCommandAsync(ProcessManager.COMMAND_CONNECTED);
                while (!mExiting)
                {
                    var command = await mProcessManager.ReadCommandAsync();
                    if (command.StartsWith(ProcessManager.COMMAND_CLOSE))
                    {
                        await mProcessManager.SendCommandAsync(ProcessManager.COMMAND_CLOSING + "@reason=Closed by service");
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Write("ProcessServerCommands", ex);
            }
            return false;
        }

        private void FormGui_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing && !mDebug)
            {
                Hide();
                e.Cancel = true;
                return;
            }
            ApplicationExit("@reason=" + e.CloseReason);
        }

        void ApplicationExit(string reason)
        {
            if (mExiting)
                return;
            mExiting = true;
            try { mHttpServer.Stop(); } catch { }
            try { mBeacon.Stop(); } catch { }

            // Try sending the message, even though the pipe could
            // be closed, transmitting, or not even open yet
            try { mProcessManager.SendCommandAsync(ProcessManager.COMMAND_CLOSING+reason).Wait(100); } catch { }
            try { mProcessManager.Close(); } catch { }
            Application.Exit();
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
                Show();
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
                mPeerInfo.Name = textNickname.Text + (mDebug ? " - DEBUG" : "");
                mPeerInfo.HttpsPort = mPortHttps.ToString();
                mPeerInfo.HttpPort = mPortHttp.ToString();
                mBeacon = new Beacon();
                mBeacon.EnableBroadcast = mSettings.EnableBeaconBroadcast;
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
                ProcessStartAsLoggedOnUser(e.Link.LinkData.ToString());
        }

        private void labelUnsecureLink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (e.Link.LinkData != null)
                ProcessStartAsLoggedOnUser(e.Link.LinkData.ToString());
        }

        /// <summary>
        /// Don't launch using Process.Start, or else it runs as "System"
        /// </summary>
        void ProcessStartAsLoggedOnUser(string fileName)
        {
            try
            {
                // Launch from helper task running as the user, also with the user environment
                // NOTE: This fails unless it's running in the system accont
                WtsProcess.StartInSession(Wts.GetActiveConsoleSessionId(), WtsProcessType.User,
                    Application.ExecutablePath, Program.PARAM_START_BROWSER +  " \"" + fileName + "\"").Dispose();
            }
            catch
            {
                // NOTE: New versions run in as the logged on user and the above code fails.
                Process.Start(fileName);
            }
        }

        void StartWebServer()
        {
            if (mHttpServer != null)
                return;

            Log.Write("Starting web server");
            labelHttpsLink.Text = "Starting...";
            labelHttpsLink.Enabled = true;
            labelHttpsLink.Links.Clear();
            labelHttpLink.Text = "";
            labelHttpLink.Visible = true;
            labelHttpLink.Links.Clear();


            Refresh();
            string machineName = "";
            try
            {
                // Get machine name
                machineName = Dns.GetHostName();
                if (machineName.Trim() == "")
                    machineName = "localhost";
                if (mSettings.Nickname.Trim() == "")
                {
                    mSettings.Nickname = machineName;
                    textNickname.Text = machineName;
                }
                mOvtServer = new ViewtopServer();
                mOvtServer.LocalComputerInfo.ComputerName = machineName;
                mOvtServer.LocalComputerInfo.Name = mSettings.Nickname;
                mOvtServer.LocalComputerInfo.HttpsPort = mPortHttps.ToString();
                mOvtServer.LocalComputerInfo.HttpPort = mPortHttp.ToString();

                // Setup server
                mHttpServer = new HttpServer();
                mHttpServer.HttpHandler += (context) => 
                {
                    var ep = context.LocalEndPoint as IPEndPoint;
                    if (ep != null && ep.Port == mPortForwardToHttps)
                    {
                        var r = context.Response;
                        r.StatusCode = 301;
                        r.StatusMessage = "Moved Permanently";
                        r.Headers["location"] = "https://" + context.Request.HostNoPort + ":" + mPortHttps + context.Request.Path;
                        return context.SendResponseAsync("");
                    }
                    if (context.Request.Path == "/ovt/log")
                        return context.SendResponseAsync(Log.GetAsString(200));
                    return mOvtServer.ProcessOpenViewtopRequestAsync(context);
                };
                // Start servers
                mHttpServer.Start(new TcpListener(IPAddress.Any, mPortForwardToHttps));
                mHttpServer.Start(new TcpListener(IPAddress.Any, mPortHttp));

                Log.Write("HTTP web server running");

                string link = "http://" + machineName + ":" + mPortHttp;
                string text = "";
                labelHttpLink.Text = "HTTP:" + mPortHttp;
                labelHttpLink.Links.Add(text.Length, link.Length, link);

                try
                {
                    mHttpServer.Start(new TcpListener(IPAddress.Any, mPortHttps), Util.GetCertificate());

                    Log.Write("HTTPS web server running");
                    link = "https://" + machineName + ":" + mPortHttps;
                    text = "";
                    labelHttpsLink.Text = "HTTPS:" + mPortHttps;
                    labelHttpsLink.Links.Add(text.Length, link.Length, link);
                }
                catch (Exception ex)
                {
                    Log.Write("StartWebServer, could not start HTTPS: " + ex.Message);
                    labelHttpsLink.Text = "HTTPS Error";
                }
            }
            catch (Exception ex)
            {
                Log.Write("StartWebServer", ex);
                try { mHttpServer.Stop(); } catch { }
                mHttpServer = null;
                labelHttpsLink.Text = "Error: " + ex.Message;
                labelHttpLink.Text = "";
                throw;
            }
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
                        Log.Write("GetPublicIpAddress = " + match.Value);
                        labelPublicIpAddress.Text = "Public IP address: " + match.Value;
                        mOvtServer.LocalComputerInfo.PublicIp = match.Value;
                    }
                    else
                    {
                        Log.Write("GetPublicIpAddress: Could not find IP address ");
                        labelPublicIpAddress.Text = "Could not obtain public IP address";
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Write("GetPublicIpAddress: Error retrieving IP address: " + ex.Message);
                labelPublicIpAddress.Text = "Could not obtain public IP address";
            }
        }

        void StopWebServer()
        {
            if (mHttpServer != null)
                mHttpServer.Stop();
            mHttpServer = null;
            mOvtServer = new ViewtopServer();

            labelHttpsLink.Text = "Stopped";
            labelHttpsLink.Enabled = false;
            labelHttpLink.Text = "";
            labelHttpLink.Visible = false;
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

        private void textNickname_TextChanged(object sender, EventArgs e)
        {
            mPeerInfo.Name = textNickname.Text + (mDebug ? " - DEBUG" : "");
            mSettings.Nickname = textNickname.Text;
            mOvtServer.LocalComputerInfo.Name = textNickname.Text;
        }

        private void textNickname_Leave(object sender, EventArgs e)
        {
            SaveSettings();
        }

        void SaveSettings()
        {
            try
            {
                Settings.Save(mSettings);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Error saving settings: " + ex, App.Name);
            }
        }

        // Called when a new peer is detected
        void mBeacon_PeerAdded(Beacon.Peer peer)
        {
            // Ignore this compurer
            if (peer.ThisBeacon)
                return;

            // Add row to grid
            mPeers.Add(peer.Key);
            gridRemote.Rows.Add("", "", "", "", "", "");
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

        // The tray icon doesn't show if the user account is still booting.  Poke it a few times
        private void timerTrayIcon_Tick(object sender, EventArgs e)
        {
            if (mTrayIconRetryCount <= TRAY_ICON_RETRIES)
            {
                mTrayIconRetryCount++;
                if (mTrayIconRetryCount % TRAY_ICON_RETRY_TIME == 0)
                {
                    notifyIcon.Visible = false;
                    notifyIcon.Visible = true;
                }
            }
        }


        private void timerRunSystem_Tick(object sender, EventArgs e)
        {
            timerRunSystem.Interval = 100;

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

            if (mBeacon != null)
                mBeacon.Update();

            // Show this form on the rising edge of not being able to get
            // the mutex, which means another instance (see Program.cs)
            // is signalling this server to show itself.
            var showOnTopMutex = Program.ShowOnTopMutex;
            if (showOnTopMutex != null)
            {
                var showOnTopMutexState = showOnTopMutex.WaitOne(0);
                if (showOnTopMutexState)
                    showOnTopMutex.ReleaseMutex();
                if (!showOnTopMutexState && mShowOnTopMutexLastState)
                    ShowOnTop();
                mShowOnTopMutexLastState = showOnTopMutexState;
            }
        }

        private void timerUpdateGui_Tick(object sender, EventArgs e)
        {
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
                Color statusColor;
                if (info.State == Beacon.State.Exit)
                {
                    status = "Exiting...";
                    statusColor = Color.Blue;
                }
                else if (lastTimeReceived > PROBLEM_PEER_TIME_MS)
                {
                    status = "Lost connection!";
                    statusColor = Color.Red;
                }
                else if (!peer.ConnectionEstablished)
                {
                    status = "Connecting...";
                    statusColor = Color.Gray;
                }
                else if (peer.UsingNat)
                {
                    status = "Using NAT";
                    statusColor = Color.Red;
                }
                else
                {
                    status = "Ok";
                    statusColor = Color.Black;
                }

                // Add grid row
                // NOTE: Using the computer name doesn't always work because our DNS server may
                //       not have resolved it yet.  So, for now, use the IP address instead.
                var name = info.Name.Trim() == "" ? "" : " (" + info.Name + ")";
                var gridRow = gridRemote.Rows[rowIndex].Cells;
                gridRow[GR_COL_NAME].Value = info.Name;
                gridRow[GR_COL_COMPUTER].Value = info.ComputerName;
                gridRow[GR_COL_IP].Value = peer.EndPoint.Address.ToString();
                gridRow[GR_COL_HTTP_PORT].Value = new GridLink(":" + info.HttpPort, "http://" + peer.EndPoint.Address + ":" + info.HttpPort);
                gridRow[GR_COL_HTTP_PORT].Style.Font = new Font(gridRemote.Font, FontStyle.Underline);
                gridRow[GR_COL_HTTP_PORT].Style.ForeColor = Color.Blue;
                gridRow[GR_COL_HTTPS_PORT].Value = new GridLink(":" + info.HttpsPort, "https://" + peer.EndPoint.Address + ":" + info.HttpsPort);
                gridRow[GR_COL_HTTPS_PORT].Style.Font = new Font(gridRemote.Font, FontStyle.Underline);
                gridRow[GR_COL_HTTPS_PORT].Style.ForeColor = Color.Blue;
                gridRow[GR_COL_STATUS].Value = status;
                gridRow[GR_COL_STATUS].Style.ForeColor = statusColor;

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
            var link = gridRemote[e.ColumnIndex, e.RowIndex].Value as GridLink;
            if (link != null)
                ProcessStartAsLoggedOnUser(link.Link);
        }

        private void gridRemote_CellMouseEnter(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && (e.ColumnIndex == GR_COL_HTTPS_PORT || e.ColumnIndex == GR_COL_HTTP_PORT))
                gridRemote.Cursor = Cursors.Hand;
        }

        private void gridRemote_CellMouseLeave(object sender, DataGridViewCellEventArgs e)
        {
            gridRemote.Cursor = Cursors.Default;
        }
        private void gridRemote_SelectionChanged(object sender, EventArgs e)
        {
            // Disable row selection
            while (gridRemote.SelectedRows.Count != 0)
                gridRemote.SelectedRows[0].Selected = false;
        }

        private void buttonFindRemoteComputers_Click(object sender, EventArgs e)
        {
            var ip = FormInputBox.ShowDialog(this, "Enter IP address:");
            if (ip == "")
                return;
            if (!IPAddress.TryParse(ip, out IPAddress address))
            {
                MessageBox.Show(this, "Invalid IP address.  Enter four numbers from 0 to 255 separted by periods (e.g. '192.168.0.32', etc.)");
                return;
            }

            mBeacon.Ping(address);
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

        private void checkBroadcast_CheckedChanged(object sender, EventArgs e)
        {
            if (mSettings.EnableBeaconBroadcast != checkBroadcast.Checked)
            {
                mSettings.EnableBeaconBroadcast = checkBroadcast.Checked;
                if (mBeacon != null)
                    mBeacon.EnableBroadcast = mSettings.EnableBeaconBroadcast;
                SaveSettings();
            }

        }

        private void buttonRegenerateSelfSignedCertificate_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show(this, "Are you sure you want to regenerate the self-signed certificate?", App.Name, MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                try
                {
                    StopWebServer();
                    Util.RegenerateSelfSignedCertificte();
                    MessageBox.Show(this, "A new self signed certificate has been generated", App.Name);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "ERROR: " + ex.Message, App.Name);
                }
            }
        }

        private void buttonLoadSignedCertificate_Click(object sender, EventArgs e)
        {
            openFileDialog1.FileName = "";
            openFileDialog1.Title = App.Name;
            openFileDialog1.Multiselect = false;
            openFileDialog1.Filter = "PFX file (*.pfx)|*.pfx";
            openFileDialog1.ShowDialog(this);
            if (openFileDialog1.FileName == "")
                return; // User cancelled

            try
            {
                StopWebServer();
                Util.LoadCertificateFile(openFileDialog1.FileName);
                MessageBox.Show(this, "New certificate loaded succesfully", App.Name);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "ERROR: " + ex.Message, App.Name);
            }
        }
    }
}
