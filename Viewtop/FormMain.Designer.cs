namespace Gosub.Viewtop
{
    partial class FormMain
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
            this.buttonStart = new System.Windows.Forms.Button();
            this.buttonStop = new System.Windows.Forms.Button();
            this.labelSecureLink = new System.Windows.Forms.LinkLabel();
            this.labelUnsecureLink = new System.Windows.Forms.LinkLabel();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.comboJitter = new System.Windows.Forms.ComboBox();
            this.comboLatency = new System.Windows.Forms.ComboBox();
            this.listUsers = new System.Windows.Forms.ListBox();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.buttonChangePassword = new System.Windows.Forms.Button();
            this.buttonDeleteUser = new System.Windows.Forms.Button();
            this.buttonNewUser = new System.Windows.Forms.Button();
            this.timerBeacon = new System.Windows.Forms.Timer(this.components);
            this.textName = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.timerUpdateRemoteGrid = new System.Windows.Forms.Timer(this.components);
            this.labelLocalIpAddress = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.gridRemote = new Gosub.Viewtop.GridWithoutAutoSelect();
            this.columnName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.columnIp = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.columnStatus = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.groupBox1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gridRemote)).BeginInit();
            this.SuspendLayout();
            // 
            // buttonStart
            // 
            this.buttonStart.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonStart.Location = new System.Drawing.Point(12, 12);
            this.buttonStart.Name = "buttonStart";
            this.buttonStart.Size = new System.Drawing.Size(75, 28);
            this.buttonStart.TabIndex = 0;
            this.buttonStart.Text = "Start";
            this.buttonStart.UseVisualStyleBackColor = true;
            this.buttonStart.Click += new System.EventHandler(this.buttonStart_Click);
            // 
            // buttonStop
            // 
            this.buttonStop.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonStop.Location = new System.Drawing.Point(93, 12);
            this.buttonStop.Name = "buttonStop";
            this.buttonStop.Size = new System.Drawing.Size(75, 28);
            this.buttonStop.TabIndex = 1;
            this.buttonStop.Text = "Stop";
            this.buttonStop.UseVisualStyleBackColor = true;
            this.buttonStop.Click += new System.EventHandler(this.buttonStop_Click);
            // 
            // labelSecureLink
            // 
            this.labelSecureLink.AutoSize = true;
            this.labelSecureLink.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelSecureLink.Location = new System.Drawing.Point(174, 16);
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
            this.labelUnsecureLink.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelUnsecureLink.Location = new System.Drawing.Point(175, 42);
            this.labelUnsecureLink.Name = "labelUnsecureLink";
            this.labelUnsecureLink.Size = new System.Drawing.Size(95, 13);
            this.labelUnsecureLink.TabIndex = 5;
            this.labelUnsecureLink.TabStop = true;
            this.labelUnsecureLink.Text = "labelUnsecureLink";
            this.labelUnsecureLink.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.labelUnsecureLink_LinkClicked);
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.label2);
            this.groupBox1.Controls.Add(this.label1);
            this.groupBox1.Controls.Add(this.comboJitter);
            this.groupBox1.Controls.Add(this.comboLatency);
            this.groupBox1.Location = new System.Drawing.Point(12, 220);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(156, 74);
            this.groupBox1.TabIndex = 6;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Simulated Network Delay:";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(33, 49);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(32, 13);
            this.label2.TabIndex = 9;
            this.label2.Text = "Jitter:";
            this.label2.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(17, 22);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(48, 13);
            this.label1.TabIndex = 7;
            this.label1.Text = "Latency:";
            this.label1.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // comboJitter
            // 
            this.comboJitter.FormattingEnabled = true;
            this.comboJitter.Location = new System.Drawing.Point(71, 46);
            this.comboJitter.Name = "comboJitter";
            this.comboJitter.Size = new System.Drawing.Size(64, 21);
            this.comboJitter.TabIndex = 8;
            this.comboJitter.SelectedIndexChanged += new System.EventHandler(this.comboJitter_SelectedIndexChanged);
            // 
            // comboLatency
            // 
            this.comboLatency.FormattingEnabled = true;
            this.comboLatency.Location = new System.Drawing.Point(71, 19);
            this.comboLatency.Name = "comboLatency";
            this.comboLatency.Size = new System.Drawing.Size(64, 21);
            this.comboLatency.TabIndex = 7;
            this.comboLatency.SelectedIndexChanged += new System.EventHandler(this.comboLatency_SelectedIndexChanged);
            // 
            // listUsers
            // 
            this.listUsers.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.listUsers.FormattingEnabled = true;
            this.listUsers.Location = new System.Drawing.Point(6, 25);
            this.listUsers.Name = "listUsers";
            this.listUsers.Size = new System.Drawing.Size(144, 69);
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
            this.groupBox2.Location = new System.Drawing.Point(12, 46);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(156, 168);
            this.groupBox2.TabIndex = 8;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "User Names:";
            // 
            // buttonChangePassword
            // 
            this.buttonChangePassword.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonChangePassword.Location = new System.Drawing.Point(6, 134);
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
            this.buttonDeleteUser.Location = new System.Drawing.Point(82, 100);
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
            this.buttonNewUser.Location = new System.Drawing.Point(6, 100);
            this.buttonNewUser.Name = "buttonNewUser";
            this.buttonNewUser.Size = new System.Drawing.Size(68, 28);
            this.buttonNewUser.TabIndex = 9;
            this.buttonNewUser.Text = "&New";
            this.buttonNewUser.UseVisualStyleBackColor = true;
            this.buttonNewUser.Click += new System.EventHandler(this.buttonNewUser_Click);
            // 
            // timerBeacon
            // 
            this.timerBeacon.Tick += new System.EventHandler(this.timerBeacon_Tick);
            // 
            // textName
            // 
            this.textName.Location = new System.Drawing.Point(481, 59);
            this.textName.Name = "textName";
            this.textName.Size = new System.Drawing.Size(112, 20);
            this.textName.TabIndex = 9;
            this.textName.TextChanged += new System.EventHandler(this.textName_TextChanged);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(412, 62);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(63, 13);
            this.label3.TabIndex = 10;
            this.label3.Text = "Nick Name:";
            // 
            // timerUpdateRemoteGrid
            // 
            this.timerUpdateRemoteGrid.Enabled = true;
            this.timerUpdateRemoteGrid.Interval = 1000;
            this.timerUpdateRemoteGrid.Tick += new System.EventHandler(this.timerUpdateRemoteGrid_Tick);
            // 
            // labelLocalIpAddress
            // 
            this.labelLocalIpAddress.AutoSize = true;
            this.labelLocalIpAddress.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelLocalIpAddress.Location = new System.Drawing.Point(175, 62);
            this.labelLocalIpAddress.Name = "labelLocalIpAddress";
            this.labelLocalIpAddress.Size = new System.Drawing.Size(142, 13);
            this.labelLocalIpAddress.TabIndex = 12;
            this.labelLocalIpAddress.Text = "Local IP address: (unknown)";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label4.Location = new System.Drawing.Point(174, 99);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(152, 20);
            this.label4.TabIndex = 13;
            this.label4.Text = "Remote Computers:";
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
            this.columnIp,
            this.columnStatus});
            this.gridRemote.EditMode = System.Windows.Forms.DataGridViewEditMode.EditProgrammatically;
            this.gridRemote.GridColor = System.Drawing.Color.FromArgb(((int)(((byte)(224)))), ((int)(((byte)(224)))), ((int)(((byte)(224)))));
            this.gridRemote.Location = new System.Drawing.Point(178, 122);
            this.gridRemote.Name = "gridRemote";
            this.gridRemote.RowHeadersVisible = false;
            this.gridRemote.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.gridRemote.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.gridRemote.Size = new System.Drawing.Size(415, 172);
            this.gridRemote.TabIndex = 11;
            this.gridRemote.CellContentClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.gridRemote_CellContentClick);
            // 
            // columnName
            // 
            this.columnName.HeaderText = "Name";
            this.columnName.Name = "columnName";
            this.columnName.Width = 180;
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
            // 
            // FormMain
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(607, 309);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.labelLocalIpAddress);
            this.Controls.Add(this.gridRemote);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.textName);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.labelUnsecureLink);
            this.Controls.Add(this.labelSecureLink);
            this.Controls.Add(this.buttonStop);
            this.Controls.Add(this.buttonStart);
            this.Name = "FormMain";
            this.Text = "Open Viewtop";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.FormMain_FormClosing);
            this.Load += new System.EventHandler(this.FormMain_Load);
            this.Shown += new System.EventHandler(this.FormMain_Shown);
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.groupBox2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.gridRemote)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button buttonStart;
        private System.Windows.Forms.Button buttonStop;
        private System.Windows.Forms.LinkLabel labelSecureLink;
        private System.Windows.Forms.LinkLabel labelUnsecureLink;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.ComboBox comboJitter;
        private System.Windows.Forms.ComboBox comboLatency;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ListBox listUsers;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.Button buttonChangePassword;
        private System.Windows.Forms.Button buttonDeleteUser;
        private System.Windows.Forms.Button buttonNewUser;
        private System.Windows.Forms.Timer timerBeacon;
        private System.Windows.Forms.TextBox textName;
        private System.Windows.Forms.Label label3;
        private GridWithoutAutoSelect gridRemote;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnName;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnIp;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnStatus;
        private System.Windows.Forms.Timer timerUpdateRemoteGrid;
        private System.Windows.Forms.Label labelLocalIpAddress;
        private System.Windows.Forms.Label label4;
    }
}

