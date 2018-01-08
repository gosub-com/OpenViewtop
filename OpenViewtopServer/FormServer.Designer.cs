namespace Gosub.Viewtop
{
    partial class FormServer
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormServer));
            this.labelSecureLink = new System.Windows.Forms.LinkLabel();
            this.labelUnsecureLink = new System.Windows.Forms.LinkLabel();
            this.listUsers = new System.Windows.Forms.ListBox();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.buttonChangePassword = new System.Windows.Forms.Button();
            this.buttonDeleteUser = new System.Windows.Forms.Button();
            this.buttonNewUser = new System.Windows.Forms.Button();
            this.textName = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.timerRunSystem = new System.Windows.Forms.Timer(this.components);
            this.labelLocalIpAddress = new System.Windows.Forms.Label();
            this.buttonRefreshRemoteComputers = new System.Windows.Forms.Button();
            this.labelPublicIpAddress = new System.Windows.Forms.Label();
            this.notifyIcon = new System.Windows.Forms.NotifyIcon(this.components);
            this.timerUpdateBeacon = new System.Windows.Forms.Timer(this.components);
            this.gridRemote = new Gosub.Viewtop.GridWithoutAutoSelect();
            this.columnName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.columnComputer = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.columnIp = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.columnStatus = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.groupBox2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gridRemote)).BeginInit();
            this.SuspendLayout();
            // 
            // labelSecureLink
            // 
            this.labelSecureLink.AutoSize = true;
            this.labelSecureLink.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelSecureLink.Location = new System.Drawing.Point(174, 45);
            this.labelSecureLink.Name = "labelSecureLink";
            this.labelSecureLink.Size = new System.Drawing.Size(122, 20);
            this.labelSecureLink.TabIndex = 3;
            this.labelSecureLink.TabStop = true;
            this.labelSecureLink.Text = "labelSecureLink";
            this.labelSecureLink.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.labelSecureLink_LinkClicked);
            // 
            // labelUnsecureLink
            // 
            this.labelUnsecureLink.AutoSize = true;
            this.labelUnsecureLink.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelUnsecureLink.Location = new System.Drawing.Point(173, 71);
            this.labelUnsecureLink.Name = "labelUnsecureLink";
            this.labelUnsecureLink.Size = new System.Drawing.Size(140, 20);
            this.labelUnsecureLink.TabIndex = 5;
            this.labelUnsecureLink.TabStop = true;
            this.labelUnsecureLink.Text = "labelUnsecureLink";
            this.labelUnsecureLink.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.labelUnsecureLink_LinkClicked);
            // 
            // listUsers
            // 
            this.listUsers.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.listUsers.FormattingEnabled = true;
            this.listUsers.Location = new System.Drawing.Point(6, 25);
            this.listUsers.Name = "listUsers";
            this.listUsers.Size = new System.Drawing.Size(144, 238);
            this.listUsers.TabIndex = 7;
            this.listUsers.SelectedIndexChanged += new System.EventHandler(this.listUsers_SelectedIndexChanged);
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.buttonChangePassword);
            this.groupBox2.Controls.Add(this.buttonDeleteUser);
            this.groupBox2.Controls.Add(this.buttonNewUser);
            this.groupBox2.Controls.Add(this.listUsers);
            this.groupBox2.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.groupBox2.Location = new System.Drawing.Point(12, 12);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(156, 340);
            this.groupBox2.TabIndex = 8;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "User Names:";
            // 
            // buttonChangePassword
            // 
            this.buttonChangePassword.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonChangePassword.Location = new System.Drawing.Point(6, 306);
            this.buttonChangePassword.Name = "buttonChangePassword";
            this.buttonChangePassword.Size = new System.Drawing.Size(144, 28);
            this.buttonChangePassword.TabIndex = 11;
            this.buttonChangePassword.Text = "&Change Password";
            this.buttonChangePassword.UseVisualStyleBackColor = true;
            this.buttonChangePassword.Click += new System.EventHandler(this.buttonChangePassword_Click);
            // 
            // buttonDeleteUser
            // 
            this.buttonDeleteUser.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonDeleteUser.Location = new System.Drawing.Point(82, 272);
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
            this.buttonNewUser.Location = new System.Drawing.Point(6, 272);
            this.buttonNewUser.Name = "buttonNewUser";
            this.buttonNewUser.Size = new System.Drawing.Size(68, 28);
            this.buttonNewUser.TabIndex = 9;
            this.buttonNewUser.Text = "&New";
            this.buttonNewUser.UseVisualStyleBackColor = true;
            this.buttonNewUser.Click += new System.EventHandler(this.buttonNewUser_Click);
            // 
            // textName
            // 
            this.textName.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textName.Location = new System.Drawing.Point(235, 12);
            this.textName.Name = "textName";
            this.textName.Size = new System.Drawing.Size(453, 26);
            this.textName.TabIndex = 9;
            this.textName.TextChanged += new System.EventHandler(this.textName_TextChanged);
            this.textName.Leave += new System.EventHandler(this.textName_Leave);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label3.Location = new System.Drawing.Point(174, 15);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(55, 20);
            this.label3.TabIndex = 10;
            this.label3.Text = "Name:";
            // 
            // timerRunSystem
            // 
            this.timerRunSystem.Interval = 1000;
            this.timerRunSystem.Tick += new System.EventHandler(this.timerRunSystem_Tick);
            // 
            // labelLocalIpAddress
            // 
            this.labelLocalIpAddress.AutoSize = true;
            this.labelLocalIpAddress.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelLocalIpAddress.Location = new System.Drawing.Point(174, 100);
            this.labelLocalIpAddress.Name = "labelLocalIpAddress";
            this.labelLocalIpAddress.Size = new System.Drawing.Size(209, 20);
            this.labelLocalIpAddress.TabIndex = 12;
            this.labelLocalIpAddress.Text = "Local IP address: (unknown)";
            // 
            // buttonRefreshRemoteComputers
            // 
            this.buttonRefreshRemoteComputers.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonRefreshRemoteComputers.Location = new System.Drawing.Point(526, 126);
            this.buttonRefreshRemoteComputers.Name = "buttonRefreshRemoteComputers";
            this.buttonRefreshRemoteComputers.Size = new System.Drawing.Size(162, 29);
            this.buttonRefreshRemoteComputers.TabIndex = 15;
            this.buttonRefreshRemoteComputers.Text = "Refresh";
            this.buttonRefreshRemoteComputers.UseVisualStyleBackColor = true;
            this.buttonRefreshRemoteComputers.Click += new System.EventHandler(this.buttonRefreshRemoteComputers_Click);
            // 
            // labelPublicIpAddress
            // 
            this.labelPublicIpAddress.AutoSize = true;
            this.labelPublicIpAddress.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelPublicIpAddress.Location = new System.Drawing.Point(174, 129);
            this.labelPublicIpAddress.Name = "labelPublicIpAddress";
            this.labelPublicIpAddress.Size = new System.Drawing.Size(213, 20);
            this.labelPublicIpAddress.TabIndex = 16;
            this.labelPublicIpAddress.Text = "Public IP address: (unknown)";
            // 
            // notifyIcon
            // 
            this.notifyIcon.Text = "Open Viewtop";
            this.notifyIcon.Visible = true;
            this.notifyIcon.Click += new System.EventHandler(this.notifyIcon_Click);
            // 
            // timerUpdateBeacon
            // 
            this.timerUpdateBeacon.Tick += new System.EventHandler(this.timerUpdateBeacon_Tick);
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
            this.columnStatus});
            this.gridRemote.EditMode = System.Windows.Forms.DataGridViewEditMode.EditProgrammatically;
            this.gridRemote.GridColor = System.Drawing.Color.FromArgb(((int)(((byte)(224)))), ((int)(((byte)(224)))), ((int)(((byte)(224)))));
            this.gridRemote.Location = new System.Drawing.Point(178, 167);
            this.gridRemote.Name = "gridRemote";
            this.gridRemote.RowHeadersVisible = false;
            this.gridRemote.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.gridRemote.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.gridRemote.Size = new System.Drawing.Size(510, 185);
            this.gridRemote.TabIndex = 11;
            this.gridRemote.CellClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.gridRemote_CellClick);
            // 
            // columnName
            // 
            this.columnName.HeaderText = "Name";
            this.columnName.Name = "columnName";
            this.columnName.Width = 200;
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
            // 
            // columnStatus
            // 
            this.columnStatus.HeaderText = "Status";
            this.columnStatus.Name = "columnStatus";
            this.columnStatus.Width = 80;
            // 
            // FormMain
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(702, 362);
            this.Controls.Add(this.labelPublicIpAddress);
            this.Controls.Add(this.buttonRefreshRemoteComputers);
            this.Controls.Add(this.labelLocalIpAddress);
            this.Controls.Add(this.gridRemote);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.textName);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.labelUnsecureLink);
            this.Controls.Add(this.labelSecureLink);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "FormMain";
            this.Text = "Open Viewtop";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.FormMain_FormClosing);
            this.Load += new System.EventHandler(this.FormMain_Load);
            this.Shown += new System.EventHandler(this.FormMain_Shown);
            this.groupBox2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.gridRemote)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.LinkLabel labelSecureLink;
        private System.Windows.Forms.LinkLabel labelUnsecureLink;
        private System.Windows.Forms.ListBox listUsers;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.Button buttonChangePassword;
        private System.Windows.Forms.Button buttonDeleteUser;
        private System.Windows.Forms.Button buttonNewUser;
        private System.Windows.Forms.TextBox textName;
        private System.Windows.Forms.Label label3;
        private GridWithoutAutoSelect gridRemote;
        private System.Windows.Forms.Timer timerRunSystem;
        private System.Windows.Forms.Label labelLocalIpAddress;
        private System.Windows.Forms.Button buttonRefreshRemoteComputers;
        private System.Windows.Forms.Label labelPublicIpAddress;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnName;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnComputer;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnIp;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnStatus;
        private System.Windows.Forms.NotifyIcon notifyIcon;
        private System.Windows.Forms.Timer timerUpdateBeacon;
    }
}

