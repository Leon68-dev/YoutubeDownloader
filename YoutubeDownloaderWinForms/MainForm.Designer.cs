namespace YoutubeDownloaderWinForms
{
    partial class MainForm
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
            btnBrowseDirectory = new Button();
            txtOutputDirectory = new TextBox();
            txtVideoUrl = new TextBox();
            btnDownload = new Button();
            lbxQualities = new ListBox();
            progressBar = new ProgressBar();
            txtLog = new TextBox();
            btnGetInfo = new Button();
            btnCancel = new Button();
            SuspendLayout();
            // 
            // btnBrowseDirectory
            // 
            btnBrowseDirectory.Location = new Point(645, 9);
            btnBrowseDirectory.Name = "btnBrowseDirectory";
            btnBrowseDirectory.Size = new Size(94, 29);
            btnBrowseDirectory.TabIndex = 0;
            btnBrowseDirectory.Text = "Browse";
            btnBrowseDirectory.UseVisualStyleBackColor = true;
            btnBrowseDirectory.Click += btnBrowseDirectory_Click;
            // 
            // txtOutputDirectory
            // 
            txtOutputDirectory.Location = new Point(12, 11);
            txtOutputDirectory.Name = "txtOutputDirectory";
            txtOutputDirectory.Size = new Size(627, 27);
            txtOutputDirectory.TabIndex = 1;
            // 
            // txtVideoUrl
            // 
            txtVideoUrl.Location = new Point(12, 44);
            txtVideoUrl.Name = "txtVideoUrl";
            txtVideoUrl.Size = new Size(727, 27);
            txtVideoUrl.TabIndex = 3;
            // 
            // btnDownload
            // 
            btnDownload.Location = new Point(152, 86);
            btnDownload.Name = "btnDownload";
            btnDownload.Size = new Size(94, 29);
            btnDownload.TabIndex = 2;
            btnDownload.Text = "Download";
            btnDownload.UseVisualStyleBackColor = true;
            btnDownload.Click += btnDownload_Click;
            // 
            // lbxQualities
            // 
            lbxQualities.FormattingEnabled = true;
            lbxQualities.Location = new Point(12, 121);
            lbxQualities.Name = "lbxQualities";
            lbxQualities.Size = new Size(727, 124);
            lbxQualities.TabIndex = 4;
            // 
            // progressBar
            // 
            progressBar.Location = new Point(12, 251);
            progressBar.Name = "progressBar";
            progressBar.Size = new Size(727, 29);
            progressBar.TabIndex = 5;
            // 
            // txtLog
            // 
            txtLog.Location = new Point(12, 286);
            txtLog.Multiline = true;
            txtLog.Name = "txtLog";
            txtLog.Size = new Size(727, 263);
            txtLog.TabIndex = 6;
            // 
            // btnGetInfo
            // 
            btnGetInfo.Location = new Point(12, 86);
            btnGetInfo.Name = "btnGetInfo";
            btnGetInfo.Size = new Size(94, 29);
            btnGetInfo.TabIndex = 7;
            btnGetInfo.Text = "Get Info";
            btnGetInfo.UseVisualStyleBackColor = true;
            btnGetInfo.Click += btnGetInfo_Click;
            // 
            // btnCancel
            // 
            btnCancel.Location = new Point(252, 86);
            btnCancel.Name = "btnCancel";
            btnCancel.Size = new Size(94, 29);
            btnCancel.TabIndex = 8;
            btnCancel.Text = "Cancel";
            btnCancel.UseVisualStyleBackColor = true;
            btnCancel.Click += btnCancel_Click;
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(751, 559);
            Controls.Add(btnCancel);
            Controls.Add(btnGetInfo);
            Controls.Add(txtLog);
            Controls.Add(progressBar);
            Controls.Add(lbxQualities);
            Controls.Add(txtVideoUrl);
            Controls.Add(btnDownload);
            Controls.Add(txtOutputDirectory);
            Controls.Add(btnBrowseDirectory);
            Name = "MainForm";
            Text = "Form1";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Button btnBrowseDirectory;
        private TextBox txtOutputDirectory;
        private TextBox txtVideoUrl;
        private Button btnDownload;
        private ListBox lbxQualities;
        private ProgressBar progressBar;
        private TextBox txtLog;
        private Button btnGetInfo;
        private Button btnCancel;
    }
}