using Gosub.Viewtop;

namespace Gosub.OpenViewtopServer
{
    partial class FormGui
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormGui));
            this.timerRunSystem = new System.Windows.Forms.Timer(this.components);
            this.notifyIcon = new System.Windows.Forms.NotifyIcon(this.components);
            this.timerTrayIcon = new System.Windows.Forms.Timer(this.components);
            this.timerUpdateGui = new System.Windows.Forms.Timer(this.components);
            this.openFileDialog1 = new System.Windows.Forms.OpenFileDialog();
            this.tabMain = new Gosub.OpenViewtopServer.TablessTabControl();
            this.tabPageHome = new System.Windows.Forms.TabPage();
            this.label3 = new System.Windows.Forms.Label();
            this.checkBroadcast = new System.Windows.Forms.CheckBox();
            this.labelHttpsLink = new System.Windows.Forms.LinkLabel();
            this.labelPublicIpAddress = new System.Windows.Forms.Label();
            this.labelHttpLink = new System.Windows.Forms.LinkLabel();
            this.buttonFindRemoteComputers = new System.Windows.Forms.Button();
            this.textNickname = new System.Windows.Forms.TextBox();
            this.labelLocalIpAddress = new System.Windows.Forms.Label();
            this.gridRemote = new Gosub.Viewtop.GridWithoutAutoSelect();
            this.columnName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.columnComputer = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.columnIp = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.columnHttp = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.columnHttps = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.columnStatus = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.tabPageUers = new System.Windows.Forms.TabPage();
            this.buttonChangePassword = new System.Windows.Forms.Button();
            this.listUsers = new System.Windows.Forms.ListBox();
            this.buttonDeleteUser = new System.Windows.Forms.Button();
            this.buttonNewUser = new System.Windows.Forms.Button();
            this.tabPageConfigure = new System.Windows.Forms.TabPage();
            this.buttonLoadSignedCertificate = new System.Windows.Forms.Button();
            this.buttonRegenerateSelfSignedCertificate = new System.Windows.Forms.Button();
            this.tabPageNewsFlash = new System.Windows.Forms.TabPage();
            this.buttonAcceptNewsFlash = new System.Windows.Forms.Button();
            this.webNewsFlash = new System.Windows.Forms.WebBrowser();
            this.tabMain.SuspendLayout();
            this.tabPageHome.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gridRemote)).BeginInit();
            this.tabPageUers.SuspendLayout();
            this.tabPageConfigure.SuspendLayout();
            this.tabPageNewsFlash.SuspendLayout();
            this.SuspendLayout();
            // 
            // timerRunSystem
            // 
            this.timerRunSystem.Interval = 1000;
            this.timerRunSystem.Tick += new System.EventHandler(this.timerRunSystem_Tick);
            // 
            // notifyIcon
            // 
            this.notifyIcon.Text = "Open Viewtop";
            this.notifyIcon.Visible = true;
            this.notifyIcon.Click += new System.EventHandler(this.notifyIcon_Click);
            // 
            // timerTrayIcon
            // 
            this.timerTrayIcon.Enabled = true;
            this.timerTrayIcon.Interval = 1000;
            this.timerTrayIcon.Tick += new System.EventHandler(this.timerTrayIcon_Tick);
            // 
            // timerUpdateGui
            // 
            this.timerUpdateGui.Enabled = true;
            this.timerUpdateGui.Interval = 1000;
            this.timerUpdateGui.Tick += new System.EventHandler(this.timerUpdateGui_Tick);
            // 
            // openFileDialog1
            // 
            this.openFileDialog1.FileName = "openFileDialog1";
            // 
            // tabMain
            // 
            this.tabMain.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tabMain.Controls.Add(this.tabPageHome);
            this.tabMain.Controls.Add(this.tabPageUers);
            this.tabMain.Controls.Add(this.tabPageConfigure);
            this.tabMain.Controls.Add(this.tabPageNewsFlash);
            this.tabMain.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.tabMain.Location = new System.Drawing.Point(12, 12);
            this.tabMain.Name = "tabMain";
            this.tabMain.SelectedIndex = 0;
            this.tabMain.ShowTabs = true;
            this.tabMain.ShowTabsDesignMode = true;
            this.tabMain.Size = new System.Drawing.Size(718, 378);
            this.tabMain.TabIndex = 18;
            this.tabMain.Selected += new System.Windows.Forms.TabControlEventHandler(this.tabMain_Selected);
            // 
            // tabPageHome
            // 
            this.tabPageHome.BackColor = System.Drawing.SystemColors.Control;
            this.tabPageHome.Controls.Add(this.label3);
            this.tabPageHome.Controls.Add(this.checkBroadcast);
            this.tabPageHome.Controls.Add(this.labelHttpsLink);
            this.tabPageHome.Controls.Add(this.labelPublicIpAddress);
            this.tabPageHome.Controls.Add(this.labelHttpLink);
            this.tabPageHome.Controls.Add(this.buttonFindRemoteComputers);
            this.tabPageHome.Controls.Add(this.textNickname);
            this.tabPageHome.Controls.Add(this.labelLocalIpAddress);
            this.tabPageHome.Controls.Add(this.gridRemote);
            this.tabPageHome.Location = new System.Drawing.Point(0, 29);
            this.tabPageHome.Name = "tabPageHome";
            this.tabPageHome.Padding = new System.Windows.Forms.Padding(3);
            this.tabPageHome.Size = new System.Drawing.Size(718, 345);
            this.tabPageHome.TabIndex = 0;
            this.tabPageHome.Text = "Home";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label3.Location = new System.Drawing.Point(6, 14);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(83, 20);
            this.label3.TabIndex = 10;
            this.label3.Text = "Nickname:";
            // 
            // checkBroadcast
            // 
            this.checkBroadcast.AutoSize = true;
            this.checkBroadcast.Checked = true;
            this.checkBroadcast.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBroadcast.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.checkBroadcast.Location = new System.Drawing.Point(335, 95);
            this.checkBroadcast.Name = "checkBroadcast";
            this.checkBroadcast.Size = new System.Drawing.Size(155, 24);
            this.checkBroadcast.TabIndex = 17;
            this.checkBroadcast.Text = "Enable Broadcast";
            this.checkBroadcast.UseVisualStyleBackColor = true;
            this.checkBroadcast.CheckedChanged += new System.EventHandler(this.checkBroadcast_CheckedChanged);
            // 
            // labelHttpsLink
            // 
            this.labelHttpsLink.AutoSize = true;
            this.labelHttpsLink.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelHttpsLink.Location = new System.Drawing.Point(568, 14);
            this.labelHttpsLink.Name = "labelHttpsLink";
            this.labelHttpsLink.Size = new System.Drawing.Size(60, 20);
            this.labelHttpsLink.TabIndex = 3;
            this.labelHttpsLink.TabStop = true;
            this.labelHttpsLink.Text = "HTTPS";
            this.labelHttpsLink.TextAlign = System.Drawing.ContentAlignment.TopRight;
            this.labelHttpsLink.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.labelSecureLink_LinkClicked);
            // 
            // labelPublicIpAddress
            // 
            this.labelPublicIpAddress.AutoSize = true;
            this.labelPublicIpAddress.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelPublicIpAddress.Location = new System.Drawing.Point(6, 69);
            this.labelPublicIpAddress.Name = "labelPublicIpAddress";
            this.labelPublicIpAddress.Size = new System.Drawing.Size(213, 20);
            this.labelPublicIpAddress.TabIndex = 16;
            this.labelPublicIpAddress.Text = "Public IP address: (unknown)";
            // 
            // labelHttpLink
            // 
            this.labelHttpLink.AutoSize = true;
            this.labelHttpLink.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelHttpLink.Location = new System.Drawing.Point(568, 40);
            this.labelHttpLink.Name = "labelHttpLink";
            this.labelHttpLink.Size = new System.Drawing.Size(49, 20);
            this.labelHttpLink.TabIndex = 5;
            this.labelHttpLink.TabStop = true;
            this.labelHttpLink.Text = "HTTP";
            this.labelHttpLink.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.labelUnsecureLink_LinkClicked);
            // 
            // buttonFindRemoteComputers
            // 
            this.buttonFindRemoteComputers.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonFindRemoteComputers.Location = new System.Drawing.Point(6, 92);
            this.buttonFindRemoteComputers.Name = "buttonFindRemoteComputers";
            this.buttonFindRemoteComputers.Size = new System.Drawing.Size(323, 29);
            this.buttonFindRemoteComputers.TabIndex = 15;
            this.buttonFindRemoteComputers.Text = "Find a computer on the local network...";
            this.buttonFindRemoteComputers.UseVisualStyleBackColor = true;
            this.buttonFindRemoteComputers.Click += new System.EventHandler(this.buttonFindRemoteComputers_Click);
            // 
            // textNickname
            // 
            this.textNickname.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textNickname.Location = new System.Drawing.Point(95, 11);
            this.textNickname.Name = "textNickname";
            this.textNickname.Size = new System.Drawing.Size(234, 26);
            this.textNickname.TabIndex = 9;
            this.textNickname.TextChanged += new System.EventHandler(this.textNickname_TextChanged);
            this.textNickname.Leave += new System.EventHandler(this.textNickname_Leave);
            // 
            // labelLocalIpAddress
            // 
            this.labelLocalIpAddress.AutoSize = true;
            this.labelLocalIpAddress.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelLocalIpAddress.Location = new System.Drawing.Point(6, 40);
            this.labelLocalIpAddress.Name = "labelLocalIpAddress";
            this.labelLocalIpAddress.Size = new System.Drawing.Size(209, 20);
            this.labelLocalIpAddress.TabIndex = 12;
            this.labelLocalIpAddress.Text = "Local IP address: (unknown)";
            // 
            // gridRemote
            // 
            this.gridRemote.AllowUserToAddRows = false;
            this.gridRemote.AllowUserToDeleteRows = false;
            this.gridRemote.AllowUserToResizeRows = false;
            this.gridRemote.BackgroundColor = System.Drawing.SystemColors.Window;
            this.gridRemote.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.gridRemote.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.columnName,
            this.columnComputer,
            this.columnIp,
            this.columnHttp,
            this.columnHttps,
            this.columnStatus});
            this.gridRemote.EditMode = System.Windows.Forms.DataGridViewEditMode.EditProgrammatically;
            this.gridRemote.GridColor = System.Drawing.Color.FromArgb(((int)(((byte)(224)))), ((int)(((byte)(224)))), ((int)(((byte)(224)))));
            this.gridRemote.Location = new System.Drawing.Point(6, 127);
            this.gridRemote.Name = "gridRemote";
            this.gridRemote.RowHeadersVisible = false;
            this.gridRemote.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.gridRemote.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.gridRemote.ShowCellToolTips = false;
            this.gridRemote.Size = new System.Drawing.Size(698, 208);
            this.gridRemote.TabIndex = 11;
            this.gridRemote.CellClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.gridRemote_CellClick);
            this.gridRemote.CellMouseEnter += new System.Windows.Forms.DataGridViewCellEventHandler(this.gridRemote_CellMouseEnter);
            this.gridRemote.CellMouseLeave += new System.Windows.Forms.DataGridViewCellEventHandler(this.gridRemote_CellMouseLeave);
            this.gridRemote.SelectionChanged += new System.EventHandler(this.gridRemote_SelectionChanged);
            // 
            // columnName
            // 
            this.columnName.HeaderText = "Nickname";
            this.columnName.Name = "columnName";
            this.columnName.Width = 180;
            // 
            // columnComputer
            // 
            this.columnComputer.HeaderText = "Computer";
            this.columnComputer.Name = "columnComputer";
            this.columnComputer.Width = 120;
            // 
            // columnIp
            // 
            this.columnIp.HeaderText = "IP Address";
            this.columnIp.Name = "columnIp";
            this.columnIp.Width = 140;
            // 
            // columnHttp
            // 
            this.columnHttp.HeaderText = "HTTP";
            this.columnHttp.Name = "columnHttp";
            this.columnHttp.Width = 80;
            // 
            // columnHttps
            // 
            this.columnHttps.HeaderText = "HTTPS";
            this.columnHttps.Name = "columnHttps";
            // 
            // columnStatus
            // 
            this.columnStatus.HeaderText = "Status";
            this.columnStatus.Name = "columnStatus";
            this.columnStatus.Width = 80;
            // 
            // tabPageUers
            // 
            this.tabPageUers.BackColor = System.Drawing.SystemColors.Control;
            this.tabPageUers.Controls.Add(this.buttonChangePassword);
            this.tabPageUers.Controls.Add(this.listUsers);
            this.tabPageUers.Controls.Add(this.buttonDeleteUser);
            this.tabPageUers.Controls.Add(this.buttonNewUser);
            this.tabPageUers.Location = new System.Drawing.Point(0, 29);
            this.tabPageUers.Name = "tabPageUers";
            this.tabPageUers.Padding = new System.Windows.Forms.Padding(3);
            this.tabPageUers.Size = new System.Drawing.Size(718, 345);
            this.tabPageUers.TabIndex = 1;
            this.tabPageUers.Text = "User Names";
            // 
            // buttonChangePassword
            // 
            this.buttonChangePassword.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonChangePassword.Location = new System.Drawing.Point(156, 41);
            this.buttonChangePassword.Name = "buttonChangePassword";
            this.buttonChangePassword.Size = new System.Drawing.Size(144, 28);
            this.buttonChangePassword.TabIndex = 11;
            this.buttonChangePassword.Text = "&Change Password";
            this.buttonChangePassword.UseVisualStyleBackColor = true;
            this.buttonChangePassword.Click += new System.EventHandler(this.buttonChangePassword_Click);
            // 
            // listUsers
            // 
            this.listUsers.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.listUsers.FormattingEnabled = true;
            this.listUsers.Location = new System.Drawing.Point(6, 7);
            this.listUsers.Name = "listUsers";
            this.listUsers.Size = new System.Drawing.Size(144, 238);
            this.listUsers.TabIndex = 7;
            this.listUsers.SelectedIndexChanged += new System.EventHandler(this.listUsers_SelectedIndexChanged);
            // 
            // buttonDeleteUser
            // 
            this.buttonDeleteUser.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonDeleteUser.Location = new System.Drawing.Point(230, 7);
            this.buttonDeleteUser.Name = "buttonDeleteUser";
            this.buttonDeleteUser.Size = new System.Drawing.Size(68, 28);
            this.buttonDeleteUser.TabIndex = 10;
            this.buttonDeleteUser.Text = "&Delete";
            this.buttonDeleteUser.UseVisualStyleBackColor = true;
            this.buttonDeleteUser.Click += new System.EventHandler(this.buttonDeleteUser_Click);
            // 
            // buttonNewUser
            // 
            this.buttonNewUser.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonNewUser.Location = new System.Drawing.Point(156, 7);
            this.buttonNewUser.Name = "buttonNewUser";
            this.buttonNewUser.Size = new System.Drawing.Size(68, 28);
            this.buttonNewUser.TabIndex = 9;
            this.buttonNewUser.Text = "&New";
            this.buttonNewUser.UseVisualStyleBackColor = true;
            this.buttonNewUser.Click += new System.EventHandler(this.buttonNewUser_Click);
            // 
            // tabPageConfigure
            // 
            this.tabPageConfigure.BackColor = System.Drawing.SystemColors.Control;
            this.tabPageConfigure.Controls.Add(this.buttonLoadSignedCertificate);
            this.tabPageConfigure.Controls.Add(this.buttonRegenerateSelfSignedCertificate);
            this.tabPageConfigure.Location = new System.Drawing.Point(0, 29);
            this.tabPageConfigure.Name = "tabPageConfigure";
            this.tabPageConfigure.Size = new System.Drawing.Size(718, 345);
            this.tabPageConfigure.TabIndex = 2;
            this.tabPageConfigure.Text = "Configure";
            // 
            // buttonLoadSignedCertificate
            // 
            this.buttonLoadSignedCertificate.Location = new System.Drawing.Point(213, 24);
            this.buttonLoadSignedCertificate.Name = "buttonLoadSignedCertificate";
            this.buttonLoadSignedCertificate.Size = new System.Drawing.Size(183, 56);
            this.buttonLoadSignedCertificate.TabIndex = 1;
            this.buttonLoadSignedCertificate.Text = "Load signed SSL certificate";
            this.buttonLoadSignedCertificate.UseVisualStyleBackColor = true;
            this.buttonLoadSignedCertificate.Click += new System.EventHandler(this.buttonLoadSignedCertificate_Click);
            // 
            // buttonRegenerateSelfSignedCertificate
            // 
            this.buttonRegenerateSelfSignedCertificate.Location = new System.Drawing.Point(24, 24);
            this.buttonRegenerateSelfSignedCertificate.Name = "buttonRegenerateSelfSignedCertificate";
            this.buttonRegenerateSelfSignedCertificate.Size = new System.Drawing.Size(183, 56);
            this.buttonRegenerateSelfSignedCertificate.TabIndex = 0;
            this.buttonRegenerateSelfSignedCertificate.Text = "Regenerate self signed SSL certificate";
            this.buttonRegenerateSelfSignedCertificate.UseVisualStyleBackColor = true;
            this.buttonRegenerateSelfSignedCertificate.Click += new System.EventHandler(this.buttonRegenerateSelfSignedCertificate_Click);
            // 
            // tabPageNewsFlash
            // 
            this.tabPageNewsFlash.BackColor = System.Drawing.SystemColors.Control;
            this.tabPageNewsFlash.Controls.Add(this.buttonAcceptNewsFlash);
            this.tabPageNewsFlash.Controls.Add(this.webNewsFlash);
            this.tabPageNewsFlash.Location = new System.Drawing.Point(0, 29);
            this.tabPageNewsFlash.Name = "tabPageNewsFlash";
            this.tabPageNewsFlash.Size = new System.Drawing.Size(718, 345);
            this.tabPageNewsFlash.TabIndex = 3;
            this.tabPageNewsFlash.Text = "News";
            // 
            // buttonAcceptNewsFlash
            // 
            this.buttonAcceptNewsFlash.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonAcceptNewsFlash.Location = new System.Drawing.Point(604, 307);
            this.buttonAcceptNewsFlash.Name = "buttonAcceptNewsFlash";
            this.buttonAcceptNewsFlash.Size = new System.Drawing.Size(106, 35);
            this.buttonAcceptNewsFlash.TabIndex = 1;
            this.buttonAcceptNewsFlash.Text = "Ok";
            this.buttonAcceptNewsFlash.UseVisualStyleBackColor = true;
            this.buttonAcceptNewsFlash.Click += new System.EventHandler(this.buttonAcceptNewsFlash_Click);
            // 
            // webNewsFlash
            // 
            this.webNewsFlash.AllowWebBrowserDrop = false;
            this.webNewsFlash.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.webNewsFlash.Location = new System.Drawing.Point(3, 0);
            this.webNewsFlash.MinimumSize = new System.Drawing.Size(20, 20);
            this.webNewsFlash.Name = "webNewsFlash";
            this.webNewsFlash.Size = new System.Drawing.Size(707, 301);
            this.webNewsFlash.TabIndex = 0;
            // 
            // FormGui
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(742, 402);
            this.Controls.Add(this.tabMain);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "FormGui";
            this.Opacity = 0D;
            this.ShowInTaskbar = false;
            this.Text = "Open Viewtop";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.FormGui_FormClosing);
            this.Load += new System.EventHandler(this.FormGui_Load);
            this.Shown += new System.EventHandler(this.FormGui_Shown);
            this.tabMain.ResumeLayout(false);
            this.tabPageHome.ResumeLayout(false);
            this.tabPageHome.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gridRemote)).EndInit();
            this.tabPageUers.ResumeLayout(false);
            this.tabPageConfigure.ResumeLayout(false);
            this.tabPageNewsFlash.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.LinkLabel labelHttpsLink;
        private System.Windows.Forms.LinkLabel labelHttpLink;
        private System.Windows.Forms.ListBox listUsers;
        private System.Windows.Forms.Button buttonChangePassword;
        private System.Windows.Forms.Button buttonDeleteUser;
        private System.Windows.Forms.Button buttonNewUser;
        private System.Windows.Forms.TextBox textNickname;
        private System.Windows.Forms.Label label3;
        private GridWithoutAutoSelect gridRemote;
        private System.Windows.Forms.Timer timerRunSystem;
        private System.Windows.Forms.Label labelLocalIpAddress;
        private System.Windows.Forms.Button buttonFindRemoteComputers;
        private System.Windows.Forms.Label labelPublicIpAddress;
        private System.Windows.Forms.NotifyIcon notifyIcon;
        private System.Windows.Forms.Timer timerTrayIcon;
        private System.Windows.Forms.Timer timerUpdateGui;
        private System.Windows.Forms.CheckBox checkBroadcast;
        private TablessTabControl tabMain;
        private System.Windows.Forms.TabPage tabPageHome;
        private System.Windows.Forms.TabPage tabPageUers;
        private System.Windows.Forms.TabPage tabPageConfigure;
        private System.Windows.Forms.Button buttonRegenerateSelfSignedCertificate;
        private System.Windows.Forms.Button buttonLoadSignedCertificate;
        private System.Windows.Forms.OpenFileDialog openFileDialog1;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnName;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnComputer;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnIp;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnHttp;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnHttps;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnStatus;
        private System.Windows.Forms.TabPage tabPageNewsFlash;
        private System.Windows.Forms.WebBrowser webNewsFlash;
        private System.Windows.Forms.Button buttonAcceptNewsFlash;
    }
}

