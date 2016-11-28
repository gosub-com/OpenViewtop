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
            this.buttonStart = new System.Windows.Forms.Button();
            this.buttonStop = new System.Windows.Forms.Button();
            this.labelSecureLink = new System.Windows.Forms.LinkLabel();
            this.checkAllowUnsecure = new System.Windows.Forms.CheckBox();
            this.labelUnsecureLink = new System.Windows.Forms.LinkLabel();
            this.labelLocalIpAddresses = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // buttonStart
            // 
            this.buttonStart.Location = new System.Drawing.Point(12, 12);
            this.buttonStart.Name = "buttonStart";
            this.buttonStart.Size = new System.Drawing.Size(75, 23);
            this.buttonStart.TabIndex = 0;
            this.buttonStart.Text = "Start";
            this.buttonStart.UseVisualStyleBackColor = true;
            this.buttonStart.Click += new System.EventHandler(this.buttonStart_Click);
            // 
            // buttonStop
            // 
            this.buttonStop.Location = new System.Drawing.Point(93, 12);
            this.buttonStop.Name = "buttonStop";
            this.buttonStop.Size = new System.Drawing.Size(75, 23);
            this.buttonStop.TabIndex = 1;
            this.buttonStop.Text = "Stop";
            this.buttonStop.UseVisualStyleBackColor = true;
            this.buttonStop.Click += new System.EventHandler(this.buttonStop_Click);
            // 
            // labelSecureLink
            // 
            this.labelSecureLink.AutoSize = true;
            this.labelSecureLink.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelSecureLink.Location = new System.Drawing.Point(12, 94);
            this.labelSecureLink.Name = "labelSecureLink";
            this.labelSecureLink.Size = new System.Drawing.Size(109, 17);
            this.labelSecureLink.TabIndex = 3;
            this.labelSecureLink.TabStop = true;
            this.labelSecureLink.Text = "labelSecureLink";
            this.labelSecureLink.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.labelSecureLink_LinkClicked);
            // 
            // checkAllowUnsecure
            // 
            this.checkAllowUnsecure.AutoSize = true;
            this.checkAllowUnsecure.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.checkAllowUnsecure.Location = new System.Drawing.Point(12, 70);
            this.checkAllowUnsecure.Name = "checkAllowUnsecure";
            this.checkAllowUnsecure.Size = new System.Drawing.Size(170, 21);
            this.checkAllowUnsecure.TabIndex = 4;
            this.checkAllowUnsecure.Text = "Allow unsecure access";
            this.checkAllowUnsecure.UseVisualStyleBackColor = true;
            this.checkAllowUnsecure.CheckedChanged += new System.EventHandler(this.checkAllowUnsecure_CheckedChanged);
            // 
            // labelUnsecureLink
            // 
            this.labelUnsecureLink.AutoSize = true;
            this.labelUnsecureLink.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelUnsecureLink.Location = new System.Drawing.Point(12, 120);
            this.labelUnsecureLink.Name = "labelUnsecureLink";
            this.labelUnsecureLink.Size = new System.Drawing.Size(125, 17);
            this.labelUnsecureLink.TabIndex = 5;
            this.labelUnsecureLink.TabStop = true;
            this.labelUnsecureLink.Text = "labelUnsecureLink";
            this.labelUnsecureLink.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.labelUnsecureLink_LinkClicked);
            // 
            // labelLocalIpAddresses
            // 
            this.labelLocalIpAddresses.AutoSize = true;
            this.labelLocalIpAddresses.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelLocalIpAddresses.Location = new System.Drawing.Point(12, 38);
            this.labelLocalIpAddresses.Name = "labelLocalIpAddresses";
            this.labelLocalIpAddresses.Size = new System.Drawing.Size(170, 20);
            this.labelLocalIpAddresses.TabIndex = 6;
            this.labelLocalIpAddresses.Text = "labelLocalIpAddresses";
            // 
            // FormMain
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(580, 279);
            this.Controls.Add(this.labelLocalIpAddresses);
            this.Controls.Add(this.labelUnsecureLink);
            this.Controls.Add(this.checkAllowUnsecure);
            this.Controls.Add(this.labelSecureLink);
            this.Controls.Add(this.buttonStop);
            this.Controls.Add(this.buttonStart);
            this.Name = "FormMain";
            this.Text = "Connect";
            this.Load += new System.EventHandler(this.FormMain_Load);
            this.Shown += new System.EventHandler(this.FormMain_Shown);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button buttonStart;
        private System.Windows.Forms.Button buttonStop;
        private System.Windows.Forms.LinkLabel labelSecureLink;
        private System.Windows.Forms.CheckBox checkAllowUnsecure;
        private System.Windows.Forms.LinkLabel labelUnsecureLink;
        private System.Windows.Forms.Label labelLocalIpAddresses;
    }
}

