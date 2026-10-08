using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows.Forms;
using WindowsFormsApp1.AI;

namespace WindowsFormsApp1
{
	public partial class EmployeeChatForm : Form
	{
		private readonly Color _themeBlue = Color.FromArgb(41, 128, 185);
		private readonly Color _themeAccent = Color.FromArgb(255, 82, 82);
		private readonly Color _themeBg = Color.FromArgb(240, 242, 245);

		private readonly string _userCode;
		private readonly GeminiChatService _chatService;
		private readonly List<string> _conversationHistory = new List<string>();

		public EmployeeChatForm(string userCode, string fullName, string department, string apiKey)
		{
			InitializeComponent();

			_userCode = (userCode ?? string.Empty).Trim();
			_chatService = new GeminiChatService(apiKey);

			ApplyTheme();
			lblSubtitle.Text = string.Format("{0} | {1}", string.IsNullOrWhiteSpace(fullName) ? "Çalışan" : fullName, string.IsNullOrWhiteSpace(department) ? "Departman" : department);
			this.Resize += EmployeeChatForm_Resize;
			ApplyResponsiveTypography();
			AppendApiLog("Sohbet açıldı.");
			AddAssistantMessage("Merhaba! Gelişim alanlarınıza göre size kısa ve uygulanabilir challenge önerileri sunabilirim. Hazırsanız başlayalım.");
		}

		private async void btnSend_Click(object sender, EventArgs e)
		{
			await SendCurrentMessageAsync();
		}

		private async void txtMessage_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Enter && !e.Shift)
			{
				e.SuppressKeyPress = true;
				await SendCurrentMessageAsync();
			}
		}

		private async Task SendCurrentMessageAsync()
		{
			string message = txtMessage.Text.Trim();
			if (string.IsNullOrWhiteSpace(message))
			{
				return;
			}

			txtMessage.Clear();
			AddUserMessage(message);
			SetBusy(true);

			try
			{
				AppendApiLog("Kullanıcı mesajı alındı. Uzunluk: " + message.Length);
				_conversationHistory.Add("Kullanıcı: " + message);
				string response = await _chatService.SendMessageAsync(_userCode, message, _conversationHistory, AppendApiLog);
				if (string.IsNullOrWhiteSpace(response))
				{
					response = "Şu anda bir yanıt üretemedim, lütfen tekrar deneyin.";
				}

				AddAssistantMessage(response);
				_conversationHistory.Add("Asistan: " + response);
			}
			catch (Exception ex)
			{
				AppendApiLog("Hata: " + ex.Message);
				AddAssistantMessage("Bağlantı sırasında bir sorun oluştu: " + ex.Message);
			}
			finally
			{
				SetBusy(false);
			}
		}

		private void SetBusy(bool busy)
		{
			btnSend.Enabled = !busy;
			btnSend.Text = busy ? "Gönderiliyor..." : "Gönder";
			txtMessage.Enabled = !busy;
		}

		private void AddUserMessage(string text)
		{
			AddMessageBubble("Siz", text, true);
		}

		private void AddAssistantMessage(string text)
		{
			AddMessageBubble("AI Mentor", text, false);
		}

		private void AddMessageBubble(string sender, string text, bool isUser)
		{
			var holder = new Panel
			{
				Width = flowMessages.ClientSize.Width - 25,
				Height = 1,
				Margin = new Padding(3, 6, 3, 6)
			};

			var bubble = new RoundedPanel
			{
				BackColor = isUser ? _themeBlue : Color.White,
				MaximumSize = new Size((int)(holder.Width * 0.8), 0),
				AutoSize = true,
				Padding = new Padding(16, 14, 16, 14),
				Anchor = isUser ? AnchorStyles.Top | AnchorStyles.Right : AnchorStyles.Top | AnchorStyles.Left,
				CornerRadius = 10,
				BorderColor = isUser ? _themeBlue : Color.FromArgb(185, 210, 230),
				BorderWidth = isUser ? 1 : 2
			};

			var lbl = new Label
			{
				AutoSize = true,
				MaximumSize = new Size(bubble.MaximumSize.Width - 32, 0),
				Text = sender + Environment.NewLine + text,
				Font = BuildMessageFont(),
				ForeColor = isUser ? Color.White : Color.FromArgb(45, 52, 54)
			};

			bubble.Controls.Add(lbl);
			holder.Height = bubble.PreferredSize.Height + 4;
			bubble.Location = isUser
				? new Point(Math.Max(0, holder.Width - bubble.PreferredSize.Width - 2), 2)
				: new Point(2, 2);

			holder.Controls.Add(bubble);
			flowMessages.Controls.Add(holder);
			flowMessages.ScrollControlIntoView(holder);
		}

		private void ApplyTheme()
		{
			BackColor = _themeBg;
			flowMessages.BackColor = _themeBg;
			tabMain.BackColor = _themeBg;
			btnSend.BackColor = _themeAccent;
			btnSend.FlatAppearance.MouseOverBackColor = Color.FromArgb(226, 74, 74);
			btnSend.FlatAppearance.MouseDownBackColor = Color.FromArgb(200, 60, 60);
		}

		private Font BuildMessageFont()
		{
			return new Font("Segoe UI", 11f, FontStyle.Regular);
		}

		private void EmployeeChatForm_Resize(object sender, EventArgs e)
		{
			ApplyResponsiveTypography();
			ReflowMessageBubbles();
		}

		private void ApplyResponsiveTypography()
		{
			lblTitle.Font = new Font("Segoe UI", 18f, FontStyle.Bold);
			lblSubtitle.Font = new Font("Segoe UI", 10f, FontStyle.Regular);
			txtMessage.Font = new Font("Segoe UI", 11f, FontStyle.Regular);
			btnSend.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
		}

		private void ReflowMessageBubbles()
		{
			flowMessages.SuspendLayout();

			foreach (Control c in flowMessages.Controls)
			{
				var holder = c as Panel;
				if (holder == null || holder.Controls.Count == 0)
				{
					continue;
				}

				holder.Width = flowMessages.ClientSize.Width - 25;
				var bubble = holder.Controls[0] as RoundedPanel;
				if (bubble == null || bubble.Controls.Count == 0)
				{
					continue;
				}

				bubble.MaximumSize = new Size((int)(holder.Width * 0.8), 0);
				
				var lbl = bubble.Controls[0] as Label;
				if (lbl != null)
				{
					lbl.MaximumSize = new Size(bubble.MaximumSize.Width - 32, 0);
				}

				// Force layout calculation
				lbl?.PerformLayout();
				bubble.PerformLayout();

				bool isUser = bubble.BackColor.ToArgb() == _themeBlue.ToArgb();
				holder.Height = bubble.PreferredSize.Height + 10; // Added extra padding to prevent clipping
				
				bubble.Location = isUser
					? new Point(Math.Max(0, holder.Width - bubble.Width - 2), 2)
					: new Point(2, 2);
			}
			
			flowMessages.ResumeLayout(true);
		}

		private void AppendApiLog(string message)
		{
			if (string.IsNullOrWhiteSpace(message))
			{
				return;
			}

			if (txtApiLog.InvokeRequired)
			{
				txtApiLog.BeginInvoke(new Action<string>(AppendApiLog), message);
				return;
			}

			string line = string.Format(
				CultureInfo.InvariantCulture,
				"[{0}] {1}{2}",
				DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
				message,
				Environment.NewLine);

			txtApiLog.AppendText(line);
		}

		private sealed class RoundedPanel : Panel
		{
			public int CornerRadius { get; set; } = 10;
			public Color BorderColor { get; set; } = Color.Transparent;
			public int BorderWidth { get; set; } = 1;

			protected override void OnPaint(PaintEventArgs e)
			{
				base.OnPaint(e);
				e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

				using (GraphicsPath path = BuildPath(new Rectangle(0, 0, Width - 1, Height - 1), CornerRadius))
				{
					this.Region = new Region(path);
					using (var pen = new Pen(BorderColor, BorderWidth))
					{
						e.Graphics.DrawPath(pen, path);
					}
				}
			}

			private static GraphicsPath BuildPath(Rectangle rect, int radius)
			{
				int d = radius * 2;
				var path = new GraphicsPath();
				path.AddArc(rect.X, rect.Y, d, d, 180, 90);
				path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
				path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
				path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
				path.CloseFigure();
				return path;
			}
		}

        private void flowMessages_Paint(object sender, PaintEventArgs e)
        {

        }
    }
}
