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
            this.labelLinkToWebSite = new System.Windows.Forms.LinkLabel();
            this.labelStats = new System.Windows.Forms.Label();
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
            // labelStats
            // 
            this.labelStats.AutoSize = true;
            this.labelStats.Location = new System.Drawing.Point(222, 42);
            this.labelStats.Name = "labelStats";
            this.labelStats.Size = new System.Drawing.Size(53, 13);
            this.labelStats.TabIndex = 4;
            this.labelStats.Text = "labelStats";
            // 
            // FormMain
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(580, 279);
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
        private System.Windows.Forms.Label labelStats;
    }
}

