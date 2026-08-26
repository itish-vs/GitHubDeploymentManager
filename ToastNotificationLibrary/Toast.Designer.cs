namespace ToastNotification
{
    partial class Toast
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Toast));
            pictureboxSuccess = new PictureBox();
            pictureboxInfo = new PictureBox();
            pictureboxError = new PictureBox();
            panelToast = new Panel();
            pictureboxClose = new PictureBox();
            labelToastMessageText = new Label();
            labelToastHeaderText = new Label();
            panelAccent = new Panel();
            ((System.ComponentModel.ISupportInitialize)pictureboxSuccess).BeginInit();
            ((System.ComponentModel.ISupportInitialize)pictureboxInfo).BeginInit();
            ((System.ComponentModel.ISupportInitialize)pictureboxError).BeginInit();
            panelToast.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)pictureboxClose).BeginInit();
            SuspendLayout();
            // 
            // pictureboxSuccess
            // 
            pictureboxSuccess.Image = (Image)resources.GetObject("pictureboxSuccess.Image");
            pictureboxSuccess.Location = new Point(8, 32);
            pictureboxSuccess.Margin = new Padding(2);
            pictureboxSuccess.Name = "pictureboxSuccess";
            pictureboxSuccess.Size = new Size(28, 28);
            pictureboxSuccess.SizeMode = PictureBoxSizeMode.Zoom;
            pictureboxSuccess.TabIndex = 1;
            pictureboxSuccess.TabStop = false;
            // 
            // pictureboxInfo
            // 
            pictureboxInfo.Image = (Image)resources.GetObject("pictureboxInfo.Image");
            pictureboxInfo.Location = new Point(8, 32);
            pictureboxInfo.Margin = new Padding(2);
            pictureboxInfo.Name = "pictureboxInfo";
            pictureboxInfo.Size = new Size(28, 28);
            pictureboxInfo.SizeMode = PictureBoxSizeMode.Zoom;
            pictureboxInfo.TabIndex = 2;
            pictureboxInfo.TabStop = false;
            // 
            // pictureboxError
            // 
            pictureboxError.Image = (Image)resources.GetObject("pictureboxError.Image");
            pictureboxError.Location = new Point(8, 32);
            pictureboxError.Margin = new Padding(2);
            pictureboxError.Name = "pictureboxError";
            pictureboxError.Size = new Size(28, 28);
            pictureboxError.SizeMode = PictureBoxSizeMode.Zoom;
            pictureboxError.TabIndex = 3;
            pictureboxError.TabStop = false;
            // 
            // panelToast
            // 
            panelToast.BackColor = Color.FromArgb(255, 255, 255);
            panelToast.Controls.Add(pictureboxClose);
            panelToast.Controls.Add(labelToastMessageText);
            panelToast.Controls.Add(labelToastHeaderText);
            panelToast.Controls.Add(pictureboxError);
            panelToast.Controls.Add(pictureboxSuccess);
            panelToast.Controls.Add(pictureboxInfo);
            panelToast.Location = new Point(6, 2);
            panelToast.Margin = new Padding(2);
            panelToast.Name = "panelToast";
            panelToast.Size = new Size(372, 92);
            panelToast.TabIndex = 5;
            // 
            // pictureboxClose
            // 
            pictureboxClose.Image = (Image)resources.GetObject("pictureboxClose.Image");
            pictureboxClose.Location = new Point(346, 6);
            pictureboxClose.Margin = new Padding(2);
            pictureboxClose.Name = "pictureboxClose";
            pictureboxClose.Size = new Size(20, 20);
            pictureboxClose.SizeMode = PictureBoxSizeMode.Zoom;
            pictureboxClose.TabIndex = 6;
            pictureboxClose.TabStop = false;
            pictureboxClose.Click += pictureboxClose_Click;
            // 
            // labelToastMessageText
            // 
            labelToastMessageText.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            labelToastMessageText.ForeColor = Color.FromArgb(71, 85, 105);
            labelToastMessageText.Location = new Point(44, 34);
            labelToastMessageText.Margin = new Padding(2, 0, 2, 0);
            labelToastMessageText.Name = "labelToastMessageText";
            labelToastMessageText.Size = new Size(296, 50);
            labelToastMessageText.TabIndex = 5;
            labelToastMessageText.Text = "label2";
            // 
            // labelToastHeaderText
            // 
            labelToastHeaderText.AutoSize = true;
            labelToastHeaderText.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold, GraphicsUnit.Point);
            labelToastHeaderText.ForeColor = Color.FromArgb(15, 23, 42);
            labelToastHeaderText.Location = new Point(44, 10);
            labelToastHeaderText.Margin = new Padding(2, 0, 2, 0);
            labelToastHeaderText.Name = "labelToastHeaderText";
            labelToastHeaderText.Size = new Size(60, 18);
            labelToastHeaderText.TabIndex = 4;
            labelToastHeaderText.Text = "label1";
            // 
            // panelAccent
            // 
            panelAccent.Location = new Point(0, 0);
            panelAccent.Margin = new Padding(2);
            panelAccent.Name = "panelAccent";
            panelAccent.Size = new Size(6, 96);
            panelAccent.TabIndex = 6;
            // 
            // Toast
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(223, 229, 240);
            ClientSize = new Size(380, 96);
            Controls.Add(panelAccent);
            Controls.Add(panelToast);
            FormBorderStyle = FormBorderStyle.None;
            Name = "Toast";
            Text = "Toast";
            Load += Toast_Load;
            ((System.ComponentModel.ISupportInitialize)pictureboxSuccess).EndInit();
            ((System.ComponentModel.ISupportInitialize)pictureboxInfo).EndInit();
            ((System.ComponentModel.ISupportInitialize)pictureboxError).EndInit();
            panelToast.ResumeLayout(false);
            panelToast.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)pictureboxClose).EndInit();
            ResumeLayout(false);
        }

        #endregion
        private PictureBox pictureboxSuccess;
        private PictureBox pictureboxInfo;
        private PictureBox pictureboxError;
        private Panel panelToast;
        private Panel panelAccent;
        private Label labelToastMessageText;
        private Label labelToastHeaderText;
        private PictureBox pictureboxClose;
    }
}