namespace GSPlatformClient
{
    partial class MainWindow
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
            this.panel1 = new System.Windows.Forms.Panel();
            this.serverPanel1 = new GSPlatformClient.ServerPanel();
            this.timer1 = new System.Windows.Forms.Timer(this.components);
            this.panel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // panel1
            // 
            this.panel1.AutoScroll = true;
            this.panel1.Controls.Add(this.serverPanel1);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel1.Location = new System.Drawing.Point(0, 0);
            this.panel1.MinimumSize = new System.Drawing.Size(500, 0);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(640, 463);
            this.panel1.TabIndex = 0;
            // 
            // serverPanel1
            // 
            this.serverPanel1.AutoSize = true;
            this.serverPanel1.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.serverPanel1.BackColor = System.Drawing.Color.Gray;
            this.serverPanel1.CanAddRoom = true;
            this.serverPanel1.CanRegister = true;
            this.serverPanel1.ConnectionFailed = false;
            this.serverPanel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.serverPanel1.Font = new System.Drawing.Font("DengXian", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.serverPanel1.Location = new System.Drawing.Point(0, 0);
            this.serverPanel1.Margin = new System.Windows.Forms.Padding(0);
            this.serverPanel1.MinimumSize = new System.Drawing.Size(500, 0);
            this.serverPanel1.Name = "serverPanel1";
            this.serverPanel1.OnlineUsers = null;
            this.serverPanel1.Padding = new System.Windows.Forms.Padding(0, 0, 0, 1);
            this.serverPanel1.RegisterText = "输入邀请码";
            this.serverPanel1.ServerDelay = 0;
            this.serverPanel1.ServerName = "测试服务器";
            this.serverPanel1.ServerNotRegistered = false;
            this.serverPanel1.Size = new System.Drawing.Size(640, 343);
            this.serverPanel1.TabIndex = 0;
            // 
            // timer1
            // 
            this.timer1.Interval = 10000;
            this.timer1.Tick += new System.EventHandler(this.timer1_Tick);
            // 
            // MainWindow
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.Window;
            this.ClientSize = new System.Drawing.Size(640, 463);
            this.Controls.Add(this.panel1);
            this.MinimumSize = new System.Drawing.Size(530, 330);
            this.Name = "MainWindow";
            this.Text = "连接到服务器";
            this.Shown += new System.EventHandler(this.MainWindow_Shown);
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel panel1;
        private ServerPanel serverPanel1;
        private System.Windows.Forms.Timer timer1;
    }
}