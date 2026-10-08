namespace WindowsFormsApp1
{
	partial class EmployeeChatForm
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
			this.pnlTop = new System.Windows.Forms.Panel();
			this.lblSubtitle = new System.Windows.Forms.Label();
			this.lblTitle = new System.Windows.Forms.Label();
			this.pnlComposer = new System.Windows.Forms.Panel();
			this.btnSend = new System.Windows.Forms.Button();
			this.txtMessage = new System.Windows.Forms.TextBox();
			this.tabMain = new System.Windows.Forms.TabControl();
			this.tabChat = new System.Windows.Forms.TabPage();
			this.flowMessages = new System.Windows.Forms.FlowLayoutPanel();
			this.tabApi = new System.Windows.Forms.TabPage();
			this.txtApiLog = new System.Windows.Forms.TextBox();
			this.pnlTop.SuspendLayout();
			this.pnlComposer.SuspendLayout();
			this.tabMain.SuspendLayout();
			this.tabChat.SuspendLayout();
			this.tabApi.SuspendLayout();
			this.SuspendLayout();
			// 
			// pnlTop
			// 
			this.pnlTop.BackColor = System.Drawing.Color.White;
			this.pnlTop.Controls.Add(this.lblSubtitle);
			this.pnlTop.Controls.Add(this.lblTitle);
			this.pnlTop.Dock = System.Windows.Forms.DockStyle.Top;
			this.pnlTop.Location = new System.Drawing.Point(0, 0);
			this.pnlTop.Name = "pnlTop";
			this.pnlTop.Size = new System.Drawing.Size(984, 84);
			this.pnlTop.TabIndex = 0;
			// 
			// lblSubtitle
			// 
			this.lblSubtitle.AutoSize = true;
			this.lblSubtitle.Font = new System.Drawing.Font("Segoe UI", 10F);
			this.lblSubtitle.ForeColor = System.Drawing.Color.DimGray;
			this.lblSubtitle.Location = new System.Drawing.Point(26, 48);
			this.lblSubtitle.Name = "lblSubtitle";
			this.lblSubtitle.Size = new System.Drawing.Size(121, 23);
			this.lblSubtitle.TabIndex = 1;
			this.lblSubtitle.Text = "Kullanıcı bilgisi";
			// 
			// lblTitle
			// 
			this.lblTitle.AutoSize = true;
			this.lblTitle.Font = new System.Drawing.Font("Segoe UI", 16F, System.Drawing.FontStyle.Bold);
			this.lblTitle.Location = new System.Drawing.Point(22, 10);
			this.lblTitle.Name = "lblTitle";
			this.lblTitle.Size = new System.Drawing.Size(236, 37);
			this.lblTitle.TabIndex = 0;
			this.lblTitle.Text = "LiftUp AI Mentor";
			// 
			// pnlComposer
			// 
			this.pnlComposer.BackColor = System.Drawing.Color.White;
			this.pnlComposer.Controls.Add(this.btnSend);
			this.pnlComposer.Controls.Add(this.txtMessage);
			this.pnlComposer.Dock = System.Windows.Forms.DockStyle.Bottom;
			this.pnlComposer.Location = new System.Drawing.Point(0, 608);
			this.pnlComposer.Name = "pnlComposer";
			this.pnlComposer.Size = new System.Drawing.Size(984, 73);
			this.pnlComposer.TabIndex = 1;
			// 
			// btnSend
			// 
			this.btnSend.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.btnSend.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(82)))), ((int)(((byte)(82)))));
			this.btnSend.FlatAppearance.BorderSize = 0;
			this.btnSend.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
			this.btnSend.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
			this.btnSend.ForeColor = System.Drawing.Color.White;
			this.btnSend.Location = new System.Drawing.Point(829, 17);
			this.btnSend.Name = "btnSend";
			this.btnSend.Size = new System.Drawing.Size(143, 40);
			this.btnSend.TabIndex = 1;
			this.btnSend.Text = "Gönder";
			this.btnSend.UseVisualStyleBackColor = false;
			this.btnSend.Click += new System.EventHandler(this.btnSend_Click);
			// 
			// txtMessage
			// 
			this.txtMessage.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.txtMessage.Font = new System.Drawing.Font("Segoe UI", 10F);
			this.txtMessage.Location = new System.Drawing.Point(20, 22);
			this.txtMessage.Name = "txtMessage";
			this.txtMessage.Size = new System.Drawing.Size(801, 30);
			this.txtMessage.TabIndex = 0;
			this.txtMessage.KeyDown += new System.Windows.Forms.KeyEventHandler(this.txtMessage_KeyDown);
			// 
			// tabMain
			// 
			this.tabMain.Controls.Add(this.tabChat);
			this.tabMain.Controls.Add(this.tabApi);
			this.tabMain.Dock = System.Windows.Forms.DockStyle.Fill;
			this.tabMain.Location = new System.Drawing.Point(0, 84);
			this.tabMain.Name = "tabMain";
			this.tabMain.SelectedIndex = 0;
			this.tabMain.Size = new System.Drawing.Size(984, 524);
			this.tabMain.TabIndex = 2;
			// 
			// tabChat
			// 
			this.tabChat.Controls.Add(this.flowMessages);
			this.tabChat.Location = new System.Drawing.Point(4, 25);
			this.tabChat.Name = "tabChat";
			this.tabChat.Padding = new System.Windows.Forms.Padding(3);
			this.tabChat.Size = new System.Drawing.Size(976, 495);
			this.tabChat.TabIndex = 0;
			this.tabChat.Text = "Sohbet";
			this.tabChat.UseVisualStyleBackColor = true;
			// 
			// flowMessages
			// 
			this.flowMessages.AutoScroll = true;
			this.flowMessages.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(248)))), ((int)(((byte)(249)))), ((int)(((byte)(251)))));
			this.flowMessages.Dock = System.Windows.Forms.DockStyle.Fill;
			this.flowMessages.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
			this.flowMessages.Location = new System.Drawing.Point(3, 3);
			this.flowMessages.Name = "flowMessages";
			this.flowMessages.Padding = new System.Windows.Forms.Padding(12);
			this.flowMessages.Size = new System.Drawing.Size(970, 489);
			this.flowMessages.TabIndex = 0;
			this.flowMessages.WrapContents = false;
			this.flowMessages.Paint += new System.Windows.Forms.PaintEventHandler(this.flowMessages_Paint);
			// 
			// tabApi
			// 
			this.tabApi.Controls.Add(this.txtApiLog);
			this.tabApi.Location = new System.Drawing.Point(4, 25);
			this.tabApi.Name = "tabApi";
			this.tabApi.Padding = new System.Windows.Forms.Padding(3);
			this.tabApi.Size = new System.Drawing.Size(976, 495);
			this.tabApi.TabIndex = 1;
			this.tabApi.Text = "API Log";
			this.tabApi.UseVisualStyleBackColor = true;
			// 
			// txtApiLog
			// 
			this.txtApiLog.Dock = System.Windows.Forms.DockStyle.Fill;
			this.txtApiLog.Font = new System.Drawing.Font("Consolas", 9.5F);
			this.txtApiLog.Location = new System.Drawing.Point(3, 3);
			this.txtApiLog.Multiline = true;
			this.txtApiLog.Name = "txtApiLog";
			this.txtApiLog.ReadOnly = true;
			this.txtApiLog.ScrollBars = System.Windows.Forms.ScrollBars.Both;
			this.txtApiLog.Size = new System.Drawing.Size(970, 489);
			this.txtApiLog.TabIndex = 0;
			this.txtApiLog.WordWrap = false;
			// 
			// EmployeeChatForm
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(984, 681);
			this.Controls.Add(this.tabMain);
			this.Controls.Add(this.pnlComposer);
			this.Controls.Add(this.pnlTop);
			this.Name = "EmployeeChatForm";
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
			this.Text = "LiftUp 360 - AI Mentor Sohbet";
			this.pnlTop.ResumeLayout(false);
			this.pnlTop.PerformLayout();
			this.pnlComposer.ResumeLayout(false);
			this.pnlComposer.PerformLayout();
			this.tabMain.ResumeLayout(false);
			this.tabChat.ResumeLayout(false);
			this.tabApi.ResumeLayout(false);
			this.tabApi.PerformLayout();
			this.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.Panel pnlTop;
		private System.Windows.Forms.Label lblSubtitle;
		private System.Windows.Forms.Label lblTitle;
		private System.Windows.Forms.Panel pnlComposer;
		private System.Windows.Forms.Button btnSend;
		private System.Windows.Forms.TextBox txtMessage;
		private System.Windows.Forms.TabControl tabMain;
		private System.Windows.Forms.TabPage tabChat;
		private System.Windows.Forms.FlowLayoutPanel flowMessages;
		private System.Windows.Forms.TabPage tabApi;
		private System.Windows.Forms.TextBox txtApiLog;
	}
}
