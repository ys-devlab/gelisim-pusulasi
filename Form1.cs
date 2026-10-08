using System;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;
using WindowsFormsApp1.AI;

namespace WindowsFormsApp1
{
	public partial class Form1 : Form
	{
		// --- UI Elemanları ---
		private Panel   pnlLeft;
		private TextBox txtSicilNo;
		private TextBox txtPassword;
		private Button  btnLogin;
		private Button  btnClose;
		private Button  btnTogglePass;
		private Button  btnForgotPass;
		private Panel   pnlUnderline;
		private Label   lblCapsLock;
		private bool    _passVisible = false;

		// --- Pencere Sürükleme DLL Kodları ---
		[DllImport("user32.dll", EntryPoint = "ReleaseCapture")]
		private extern static void ReleaseCapture();
		[DllImport("user32.dll", EntryPoint = "SendMessage")]
		private extern static void SendMessage(System.IntPtr hwnd, int wmsg, int wparam, int lparam);

		public Form1()
		{
			InitializeComponent();
			SetupFinalDesign();
		}

		private void Form1_Load(object sender, EventArgs e)
		{
			if (txtSicilNo != null)
			{
				this.ActiveControl = txtSicilNo;
				txtSicilNo.Focus();
			}
		}

		// --- ŞİFRE HASH ---
		private static string HashPassword(string password)
		{
			using (var sha = SHA256.Create())
			{
				byte[] bytes = Encoding.UTF8.GetBytes(password);
				byte[] hash  = sha.ComputeHash(bytes);
				return Convert.ToBase64String(hash);
			}
		}

		// --- GİRİŞ BUTONU ---
		private void BtnLogin_Click(object sender, EventArgs e)
		{
			string sicilNo  = txtSicilNo.Text.Trim().ToUpper();
			string password = txtPassword?.Text ?? "";

			if (string.IsNullOrEmpty(sicilNo))
			{
				ShakeControl(txtSicilNo);
				MessageBox.Show("Lütfen sicil numaranızı giriniz.", "Eksik Bilgi",
					MessageBoxButtons.OK, MessageBoxIcon.Warning);
				return;
			}
			if (string.IsNullOrEmpty(password))
			{
				ShakeControl(txtPassword);
				MessageBox.Show("Lütfen şifrenizi giriniz.", "Eksik Bilgi",
					MessageBoxButtons.OK, MessageBoxIcon.Warning);
				return;
			}

			try
			{
				string query = @"
                    SELECT e.PersonelCode, e.FullName, e.Title,
                           ISNULL(et.Birim, '') AS DepartmentName,
                           e.DepartmentID, e.PasswordHash, e.Email,
                           ISNULL(d.IsHR, 0) AS IsHR
                    FROM Employees e
                    LEFT JOIN Employees_Tablo et ON et.PersonelCode = e.PersonelCode
                    LEFT JOIN Departments d ON d.DepartmentID = e.DepartmentID
                    WHERE e.PersonelCode = @code";

				SqlParameter[] p = { new SqlParameter("@code", sicilNo) };
				DataTable dt = SqlHelper.GetDataTable(query, p);

				if (dt.Rows.Count == 0)
				{
					ShakeControl(txtSicilNo);
					MessageBox.Show("Sicil numarası sistemde bulunamadı.", "Giriş Başarısız",
						MessageBoxButtons.OK, MessageBoxIcon.Warning);
					return;
				}

				DataRow row       = dt.Rows[0];
				string storedHash = row["PasswordHash"] != DBNull.Value ? row["PasswordHash"].ToString() : "";
				string enteredHash = HashPassword(password);

				if (storedHash != enteredHash)
				{
					ShakeControl(txtPassword);
					txtPassword.Clear();
					MessageBox.Show("Şifre hatalı. Lütfen tekrar deneyiniz.", "Giriş Başarısız",
						MessageBoxButtons.OK, MessageBoxIcon.Warning);
					txtPassword.Focus();
					return;
				}

				// Giriş başarılı
				string dept  = row["DepartmentName"] != DBNull.Value ? row["DepartmentName"].ToString() : "";
				string code  = row["PersonelCode"].ToString();
				string name  = row["FullName"].ToString();
				string title = row["Title"].ToString();

				// İK yetkisi normalize edilmiş Departments.IsHR bayrağından gelir
				// (eski kırılgan metin eşleştirmesi kaldırıldı). P001 sistem yöneticisi
				// özel durumu korunur.
				bool isHRDept = row["IsHR"] != DBNull.Value && Convert.ToBoolean(row["IsHR"]);
				bool isHR = isHRDept || code == "P001";

				var dashboard = new DashboardForm(code, name, title, dept, isHR);
				dashboard.Show();
				this.Hide();
			}
			catch (Exception ex)
			{
				MessageBox.Show("Bağlantı hatası: " + ex.Message, "Hata",
					MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		// --- ŞİFREMİ UNUTTUM ---
		private void BtnForgotPass_Click(object sender, EventArgs e)
		{
			string sicilNo = txtSicilNo.Text.Trim().ToUpper();

			if (string.IsNullOrEmpty(sicilNo))
			{
				MessageBox.Show("Lütfen önce sicil numaranızı giriniz.", "Bilgi",
					MessageBoxButtons.OK, MessageBoxIcon.Information);
				txtSicilNo.Focus();
				return;
			}

			try
			{
				string query = "SELECT FullName, Email FROM Employees WHERE PersonelCode=@code";
				var ps = new SqlParameter[] { new SqlParameter("@code", sicilNo) };
				DataTable dt = SqlHelper.GetDataTable(query, ps);

				if (dt.Rows.Count == 0)
				{
					MessageBox.Show("Sicil numarası sistemde bulunamadı.", "Bilgi",
						MessageBoxButtons.OK, MessageBoxIcon.Warning);
					return;
				}

				string fullName = dt.Rows[0]["FullName"].ToString();
				string email    = dt.Rows[0]["Email"].ToString();

				// Mail gönder (arka planda, hata olursa sessiz)
				System.Threading.Tasks.Task.Run(() =>
					EmailHelper.SendPasswordResetEmail(email, fullName));

				MessageBox.Show(
					"Şifre sıfırlama talebiniz alındı.\n\n" +
					"Sistemde kayıtlı olan mail adresinize yönerge gönderilmiştir.\n\n" +
					"Mail kutunuzu kontrol ediniz.",
					"Mail Gönderildi",
					MessageBoxButtons.OK,
					MessageBoxIcon.Information);
			}
			catch (Exception ex)
			{
				MessageBox.Show("İşlem sırasında hata oluştu: " + ex.Message, "Hata",
					MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		// --- KONTROL SALLAMA ANİMASYONU (hatalı giriş geri bildirimi) ---
		private async void ShakeControl(Control ctrl)
		{
			if (ctrl == null) return;
			var orig = ctrl.Left;
			int[] offsets = { -6, 6, -5, 5, -4, 4, -2, 2, 0 };
			foreach (int o in offsets)
			{
				ctrl.Left = orig + o;
				await System.Threading.Tasks.Task.Delay(30);
			}
			ctrl.Left = orig;
		}

		// ── TUSAŞ Renk Paleti ─────────────────────────────────────────
		// Sol panel #0E1A32 → logo mavisi ve kırmızısı her ikisi de kontrast sağlar
		private static readonly Color T_Navy     = Color.FromArgb( 14,  26,  50);  // Çok koyu lacivert-siyah
		private static readonly Color T_NavyDark = Color.FromArgb(  8,  16,  34);  // Hover/active
		private static readonly Color T_Red      = Color.FromArgb(227,   6,  19);  // TUSAŞ kırmızısı
		private static readonly Color T_BgLight  = Color.FromArgb(238, 243, 250);  // Arka plan
		private static readonly Color T_TextDark = Color.FromArgb( 15,  32,  65);
		private static readonly Color T_TextSub  = Color.FromArgb(100, 116, 139);
		private static readonly Color T_Border   = Color.FromArgb(213, 220, 232);

		// --- TASARIM KODLARI – TUSAŞ TEMA ---
		private void SetupFinalDesign()
		{
			this.Controls.Clear();
			this.Text              = "TUSAŞ Gelişim Pusulası";
			this.Size              = new Size(1100, 660);
			this.FormBorderStyle   = FormBorderStyle.None;
			this.StartPosition     = FormStartPosition.CenterScreen;
			this.BackColor         = Color.White;

			// ── SOL PANEL (TUSAŞ lacivert) ────────────────────────────
			pnlLeft               = new Panel();
			pnlLeft.Dock          = DockStyle.Left;
			pnlLeft.Width         = 420;
			pnlLeft.BackColor     = T_Navy;
			pnlLeft.MouseDown    += Form_MouseDown;
			this.Controls.Add(pnlLeft);

			// Kırmızı alt şerit
			var pnlRedStripe = new Panel { Size = new Size(420, 5), Location = new Point(0, 0), BackColor = T_Red };
			pnlLeft.Controls.Add(pnlRedStripe);

			// ── Logo — direkt panel üstünde, beyaz kutu yok ──────────────
			var pb = new PictureBox
			{
				Size      = new Size(300, 100),
				Location  = new Point(40, 55),
				SizeMode  = PictureBoxSizeMode.Zoom,
				BackColor = Color.Transparent
			};

			bool logoLoaded = false;
			try
			{
				string[] kandidatlar = {
					System.IO.Path.Combine(Application.StartupPath, "Resources", "tusas_logo.png"),
					System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.ExecutablePath), "Resources", "tusas_logo.png"),
					System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "tusas_logo.png")
				};
				foreach (string logoPath in kandidatlar)
				{
					if (System.IO.File.Exists(logoPath))
					{
						pb.Image = System.Drawing.Image.FromFile(logoPath);
						logoLoaded = true;
						break;
					}
				}
			}
			catch { }
			pnlLeft.Controls.Add(pb);

			// Logo yoksa metin fallback
			if (!logoLoaded)
			{
				var lblFallbackBig = new Label
				{
					Text      = "TUSAŞ",
					Font      = new Font("Segoe UI", 32, FontStyle.Bold),
					ForeColor = Color.White,
					Location  = new Point(40, 70),
					AutoSize  = true
				};
				pnlLeft.Controls.Add(lblFallbackBig);
			}

			// "GELİŞİM PUSULASI" — büyük ve kurumsal
			var lblBrand = new Label
			{
				Text      = "GELİŞİM PUSULASI",
				Font      = new Font("Segoe UI", 16, FontStyle.Bold),
				ForeColor = Color.White,
				Location  = new Point(40, 170),
				AutoSize  = true
			};
			pnlLeft.Controls.Add(lblBrand);

			var lblBrandSub = new Label
			{
				Text      = "360° Performans Değerlendirme Sistemi",
				Font      = new Font("Segoe UI", 11),
				ForeColor = Color.FromArgb(160, 190, 225),
				Location  = new Point(40, 202),
				AutoSize  = true
			};
			pnlLeft.Controls.Add(lblBrandSub);
			// NOT: Ayraç çizgisi kaldırıldı — metin bitişiğinde hata gibi görünüyordu

			// Sürüm bilgisi (sol altta)
			var lblVer = new Label
			{
				Text      = "v2.0  ©  2026  TUSAŞ",
				Font      = new Font("Segoe UI", 8),
				ForeColor = Color.FromArgb(100, 130, 170),
				Location  = new Point(40, 610),
				AutoSize  = true
			};
			pnlLeft.Controls.Add(lblVer);

			// ── SAĞ TARAF (beyaz giriş alanı) ───────────────────────
			// Üst kırmızı ince çizgi
			var pnlTopBar = new Panel { Size = new Size(680, 5), Location = new Point(420, 0), BackColor = T_Red };
			this.Controls.Add(pnlTopBar);

			// Kapat butonu
			btnClose = new Button
			{
				Text      = "✕",
				Font      = new Font("Segoe UI", 13),
				ForeColor = T_TextSub,
				BackColor = Color.White,
				FlatStyle = FlatStyle.Flat,
				Size      = new Size(44, 44),
				Location  = new Point(1054, 0),
				Cursor    = Cursors.Hand
			};
			btnClose.FlatAppearance.BorderSize = 0;
			btnClose.Click += (s, e) => Application.Exit();
			this.Controls.Add(btnClose);

			// ── HOŞ GELDİNİZ BAŞLIĞI ────────────────────────────────────
			var lblWelcome = new Label
			{
				Text      = "Hoş Geldiniz",
				Font      = new Font("Segoe UI Semibold", 28),
				ForeColor = T_TextDark,
				AutoSize  = true,
				Location  = new Point(470, 95)
			};
			this.Controls.Add(lblWelcome);

			// Başlık altı ince kırmızı vurgu çizgisi
			var pnlTitleAccent = new Panel
			{
				Size      = new Size(52, 3),
				Location  = new Point(470, 142),
				BackColor = T_Red
			};
			this.Controls.Add(pnlTitleAccent);

			var lblWelcomeSub = new Label
			{
				Text      = "Lütfen Sicil Numaranız ve Şifreniz ile Giriş Yapın.",
				Font      = new Font("Segoe UI", 11, FontStyle.Regular),
				ForeColor = T_TextSub,
				AutoSize  = true,
				Location  = new Point(470, 158)
			};
			this.Controls.Add(lblWelcomeSub);

			// ── SİCİL NUMARASI ──────────────────────────────────────────
			var lblSicilHeader = new Label
			{
				Text      = "SİCİL NUMARASI",
				Font      = new Font("Segoe UI", 8, FontStyle.Bold),
				ForeColor = Color.FromArgb(80, 100, 130),
				AutoSize  = true,
				Location  = new Point(470, 218)
			};
			this.Controls.Add(lblSicilHeader);

			var pnlSicilInput = new Panel { Size = new Size(420, 50), Location = new Point(470, 238), BackColor = Color.White };
			pnlSicilInput.Paint += (s, e) =>
				e.Graphics.DrawRectangle(new System.Drawing.Pen(T_Border, 1f), 0, 0, pnlSicilInput.Width - 1, pnlSicilInput.Height - 1);
			this.Controls.Add(pnlSicilInput);

			txtSicilNo = new TextBox
			{
				BorderStyle = BorderStyle.None,
				Font        = new Font("Segoe UI", 13),
				Location    = new Point(14, 13),
				Width       = 388,
				BackColor   = Color.White,
				ForeColor   = T_TextDark
			};
			txtSicilNo.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { txtPassword.Focus(); } };
			pnlSicilInput.Controls.Add(txtSicilNo);

			// ── ŞİFRE ───────────────────────────────────────────────────
			var lblPassHeader = new Label
			{
				Text      = "ŞİFRE",
				Font      = new Font("Segoe UI", 8, FontStyle.Bold),
				ForeColor = Color.FromArgb(80, 100, 130),
				AutoSize  = true,
				Location  = new Point(470, 308)
			};
			this.Controls.Add(lblPassHeader);

			var pnlPassInput = new Panel { Size = new Size(420, 50), Location = new Point(470, 328), BackColor = Color.White };
			pnlPassInput.Paint += (s, e) =>
				e.Graphics.DrawRectangle(new System.Drawing.Pen(T_Border, 1f), 0, 0, pnlPassInput.Width - 1, pnlPassInput.Height - 1);
			this.Controls.Add(pnlPassInput);

			txtPassword = new TextBox
			{
				BorderStyle  = BorderStyle.None,
				Font         = new Font("Segoe UI", 13),
				Location     = new Point(14, 13),
				Width        = 352,
				BackColor    = Color.White,
				ForeColor    = T_TextDark,
				PasswordChar = '●'
			};
			txtPassword.KeyDown += (s, e) => {
				if (e.KeyCode == Keys.Enter) BtnLogin_Click(s, e);
				// Caps Lock uyarısı
				lblCapsLock.Visible = Control.IsKeyLocked(Keys.CapsLock);
			};
			txtPassword.KeyUp += (s, e) => lblCapsLock.Visible = Control.IsKeyLocked(Keys.CapsLock);
			pnlPassInput.Controls.Add(txtPassword);

			// Göz (toggle) butonu
			btnTogglePass = new Button
			{
				Text      = "👁",
				Size      = new Size(40, 40),
				Location  = new Point(374, 5),
				FlatStyle = FlatStyle.Flat,
				BackColor = Color.White,
				ForeColor = T_TextSub,
				Font      = new Font("Segoe UI", 12),
				Cursor    = Cursors.Hand,
				TabStop   = false
			};
			btnTogglePass.FlatAppearance.BorderSize = 0;
			btnTogglePass.Click += (s, e) => {
				_passVisible        = !_passVisible;
				txtPassword.PasswordChar = _passVisible ? '\0' : '●';
				btnTogglePass.ForeColor  = _passVisible ? T_Navy : T_TextSub;
			};
			pnlPassInput.Controls.Add(btnTogglePass);

			// ── CAPS LOCK UYARISI ────────────────────────────────────────
			lblCapsLock = new Label
			{
				Text      = "⚠  Caps Lock açık — şifrenizi kontrol edin.",
				Font      = new Font("Segoe UI", 8.5f, FontStyle.Bold),
				ForeColor = Color.FromArgb(170, 85, 0),
				AutoSize  = true,
				Location  = new Point(470, 386),
				Visible   = Control.IsKeyLocked(Keys.CapsLock)
			};
			this.Controls.Add(lblCapsLock);

			// ── GÜVENLİ GİRİŞ BUTONU ────────────────────────────────────
			pnlUnderline = new Panel { Size = new Size(0, 0), Visible = false };
			this.Controls.Add(pnlUnderline); // eski referans korunuyor

			var T_ButtonBlue = Color.FromArgb(0, 43, 92);   // #002B5C — TUSAŞ laciverdinin tam tonu
			var T_ButtonHover = Color.FromArgb(0, 32, 70);  // hover için biraz koyu

			btnLogin = new Button
			{
				Text      = "GÜVENLİ GİRİŞ",
				BackColor = T_ButtonBlue,
				ForeColor = Color.White,
				FlatStyle = FlatStyle.Flat,
				Font      = new Font("Segoe UI Semibold", 11),
				Size      = new Size(420, 52),
				Location  = new Point(470, 408),
				Cursor    = Cursors.Hand
			};
			btnLogin.FlatAppearance.BorderSize  = 0;
			btnLogin.FlatAppearance.BorderColor = T_ButtonBlue;
			btnLogin.Click      += new EventHandler(BtnLogin_Click);
			btnLogin.MouseEnter += (s, e) => btnLogin.BackColor = T_ButtonHover;
			btnLogin.MouseLeave += (s, e) => btnLogin.BackColor = T_ButtonBlue;
			this.Controls.Add(btnLogin);

			// ── ŞİFREMİ UNUTTUM ─────────────────────────────────────────
			btnForgotPass = new Button
			{
				Text      = "Şifremi Unuttum",
				Font      = new Font("Segoe UI", 9),
				ForeColor = T_TextSub,
				BackColor = Color.Transparent,
				FlatStyle = FlatStyle.Flat,
				AutoSize  = true,
				Location  = new Point(470, 475),
				Cursor    = Cursors.Hand,
				TabStop   = false
			};
			btnForgotPass.FlatAppearance.BorderSize = 0;
			btnForgotPass.Click     += new EventHandler(BtnForgotPass_Click);
			btnForgotPass.MouseEnter += (s, e) => { btnForgotPass.ForeColor = T_ButtonBlue; btnForgotPass.Font = new Font("Segoe UI", 9, FontStyle.Underline); };
			btnForgotPass.MouseLeave += (s, e) => { btnForgotPass.ForeColor = T_TextSub;    btnForgotPass.Font = new Font("Segoe UI", 9); };
			this.Controls.Add(btnForgotPass);

			// ── GÜVENLİK NOTU ───────────────────────────────────────────
			var lblNote = new Label
			{
				Text      = "🔒  Oturumunuz şifrelenmiş bağlantı üzerinden işlenmektedir.",
				Font      = new Font("Segoe UI", 8.5f),
				ForeColor = Color.FromArgb(160, 170, 185),
				AutoSize  = true,
				Location  = new Point(470, 508)
			};
			this.Controls.Add(lblNote);

			this.MouseDown += Form_MouseDown;
		}

		/// <summary>
		/// Oturum kapatma: giriş alanlarını temizler ve formu ön plana getirir.
		/// DashboardForm tarafından "Güvenli Çıkış" ile çağrılır.
		/// </summary>
		public void ResetAndShow()
		{
			if (txtSicilNo != null)  { txtSicilNo.Clear();  }
			if (txtPassword != null) { txtPassword.Clear(); }
			if (lblCapsLock != null) { lblCapsLock.Visible = false; }
			this.Show();
			this.BringToFront();
			if (txtSicilNo != null) txtSicilNo.Focus();
		}

		private void Form_MouseDown(object sender, MouseEventArgs e)
		{
			if (e.Button == MouseButtons.Left)
			{
				ReleaseCapture();
				SendMessage(this.Handle, 0x112, 0xf012, 0);
			}
		}
	}
}