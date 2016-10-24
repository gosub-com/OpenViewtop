namespace Gosub.WebRemoteViewer
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
            this.labelLinkToWebSite = new System.Windows.Forms.LinkLabel();
            this.timerRefresh = new System.Windows.Forms.Timer(this.components);
            this.labelStats = new System.Windows.Forms.Label();
            this.checkSmartPng = new System.Windows.Forms.CheckBox();
            this.checkSuppressBackgroundCompare = new System.Windows.Forms.CheckBox();
            this.checkNoCompression = new System.Windows.Forms.CheckBox();
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
            // labelLinkToWebSite
            // 
            this.labelLinkToWebSite.AutoSize = true;
            this.labelLinkToWebSite.Location = new System.Drawing.Point(222, 17);
            this.labelLinkToWebSite.Name = "labelLinkToWebSite";
            this.labelLinkToWebSite.Size = new System.Drawing.Size(103, 13);
            this.labelLinkToWebSite.TabIndex = 3;
            this.labelLinkToWebSite.TabStop = true;
            this.labelLinkToWebSite.Text = "labelLinkToWebSite";
            this.labelLinkToWebSite.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.labelLinkToWebSite_LinkClicked);
            // 
            // timerRefresh
            // 
            this.timerRefresh.Enabled = true;
            this.timerRefresh.Interval = 1000;
            this.timerRefresh.Tick += new System.EventHandler(this.timerRefresh_Tick);
            // 
            // labelStats
            // 
            this.labelStats.AutoSize = true;
            this.labelStats.Location = new System.Drawing.Point(222, 42);
            this.labelStats.Name = "labelStats";
            this.labelStats.Size = new System.Drawing.Size(53, 13);
            this.labelStats.TabIndex = 4;
            this.labelStats.Text = "labelStats";
            // 
            // checkSmartPng
            // 
            this.checkSmartPng.AutoSize = true;
            this.checkSmartPng.Location = new System.Drawing.Point(12, 87);
            this.checkSmartPng.Name = "checkSmartPng";
            this.checkSmartPng.Size = new System.Drawing.Size(79, 17);
            this.checkSmartPng.TabIndex = 5;
            this.checkSmartPng.Text = "Smart PNG";
            this.checkSmartPng.UseVisualStyleBackColor = true;
            // 
            // checkSuppressBackgroundCompare
            // 
            this.checkSuppressBackgroundCompare.AutoSize = true;
            this.checkSuppressBackgroundCompare.Location = new System.Drawing.Point(12, 64);
            this.checkSuppressBackgroundCompare.Name = "checkSuppressBackgroundCompare";
            this.checkSuppressBackgroundCompare.Size = new System.Drawing.Size(174, 17);
            this.checkSuppressBackgroundCompare.TabIndex = 6;
            this.checkSuppressBackgroundCompare.Text = "Suppress background compare";
            this.checkSuppressBackgroundCompare.UseVisualStyleBackColor = true;
            // 
            // checkNoCompression
            // 
            this.checkNoCompression.AutoSize = true;
            this.checkNoCompression.Location = new System.Drawing.Point(12, 41);
            this.checkNoCompression.Name = "checkNoCompression";
            this.checkNoCompression.Size = new System.Drawing.Size(102, 17);
            this.checkNoCompression.TabIndex = 7;
            this.checkNoCompression.Text = "No compression";
            this.checkNoCompression.UseVisualStyleBackColor = true;
            // 
            // FormMain
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(580, 279);
            this.Controls.Add(this.checkNoCompression);
            this.Controls.Add(this.checkSuppressBackgroundCompare);
            this.Controls.Add(this.checkSmartPng);
            this.Controls.Add(this.labelStats);
            this.Controls.Add(this.labelLinkToWebSite);
            this.Controls.Add(this.buttonStop);
            this.Controls.Add(this.buttonStart);
            this.Name = "FormMain";
            this.Text = "Connect";
            this.Load += new System.EventHandler(this.FormMain_Load);
            this.Shown += new System.EventHandler(this.Jrfb_Shown);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button buttonStart;
        private System.Windows.Forms.Button buttonStop;
        private System.Windows.Forms.LinkLabel labelLinkToWebSite;
        private System.Windows.Forms.Timer timerRefresh;
        private System.Windows.Forms.Label labelStats;
        private System.Windows.Forms.CheckBox checkSmartPng;
        private System.Windows.Forms.CheckBox checkSuppressBackgroundCompare;
        private System.Windows.Forms.CheckBox checkNoCompression;
    }
}

