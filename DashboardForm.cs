using System;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using WindowsFormsApp1.AI;
using WindowsFormsApp1.AI.Core;

namespace WindowsFormsApp1
{
	public partial class DashboardForm : Form
	{
		// Anahtar artık ai.env'den okunur; sohbet servisi de gateway üzerinden çalışır.
		private static string GeminiApiKey => AiConfig.Get("OPENROUTER_API_KEY", "");

		// =================================================
		// 1. AYARLAR VE DEĞİŞKENLER
		// =================================================
		private string _userCode;
		private string _fullName;
		private string _title;
		private string _department;
		private bool _isHR;

		// ── TUSAŞ Kurumsal Renk Paleti ────────────────────────────────────
		// Sidebar: #111E32 (neredeyse siyah-lacivert) → logodaki maviden belirgin koyu
		// Böylece logonun mavi üçgeni (#1B4CA0) ve kırmızı üçgeni (#E30613) net görünür
		private Color colorSidebar        = Color.FromArgb( 14,  26,  50);  // Çok koyu lacivert-siyah
		private Color colorSidebarActive  = Color.FromArgb(  8,  16,  34);  // Aktif: daha da koyu
		private Color colorActiveAccent   = Color.FromArgb(227,   6,  19);  // TUSAŞ kırmızısı
		private Color colorBackground     = Color.FromArgb(238, 243, 250);  // Açık sayfa arka planı
		private Color colorCardTextPrimary = Color.FromArgb(15,  32,  65);  // Koyu yazı
		private Color colorTextSecondary  = Color.FromArgb(100, 116, 139);  // İkincil yazı
		private Color colorHeaderBorder   = Color.FromArgb(213, 220, 232);  // Kart kenarlığı

		private Panel pnlSidebar;
		private Panel pnlRightContainer;
		private Panel pnlHeader;
		private Panel pnlContent;

		[DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
		private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);
		[DllImport("user32.dll", EntryPoint = "ReleaseCapture")]
		private extern static void ReleaseCapture();
		[DllImport("user32.dll", EntryPoint = "SendMessage")]
		private extern static void SendMessage(System.IntPtr hwnd, int wmsg, int wparam, int lparam);

		public DashboardForm(string userCode, string fullName, string title, string department, bool isHR = false)
		{
			InitializeComponent();
			_userCode = userCode;
			_fullName = fullName;
			_title = title;
			_department = department;

			// İK yetkisi, login sırasında Departments.IsHR bayrağından normalize
			// edilerek geliyor (Form1). Elle yazılmış sicil listesi ve isim tabanlı
			// hack'ler kaldırıldı — birim/rol artık merkezi lookup verisinden gelir.
			_isHR = isHR;

			this.FormBorderStyle = FormBorderStyle.None;
			this.WindowState     = FormWindowState.Maximized;
			this.StartPosition   = FormStartPosition.CenterScreen;

			SetupLayout();
			LoadDashboardData();
		}

		public DashboardForm() { InitializeComponent(); }
		public string AktifKullaniciKodu { set { _userCode = value; } }
		public bool IsHR { set { _isHR = value; } }

		private void DashboardForm_Load(object sender, EventArgs e) { }

		private void SetupLayout()
		{
			this.Controls.Clear();
			pnlSidebar = new Panel { Dock = DockStyle.Left, Width = 285, BackColor = colorSidebar };
			this.Controls.Add(pnlSidebar);

			SetupSidebarContent();

			pnlRightContainer = new Panel { Dock = DockStyle.Fill, BackColor = colorBackground };
			this.Controls.Add(pnlRightContainer);
			pnlRightContainer.BringToFront();

			pnlHeader = new Panel { Parent = pnlRightContainer, Dock = DockStyle.Top, Height = 72, BackColor = Color.White };
			pnlHeader.MouseDown += (s, e) => { ReleaseCapture(); SendMessage(this.Handle, 0x112, 0xf012, 0); };
			// Alt kenarlık
			pnlHeader.Paint += (s, e) => {
				using (var pen = new System.Drawing.Pen(colorHeaderBorder, 1))
					e.Graphics.DrawLine(pen, 0, pnlHeader.Height - 1, pnlHeader.Width, pnlHeader.Height - 1);
			};

			// Sol kırmızı dikey şerit
			var pnlTitleStripe = new Panel { Size = new Size(4, 34), Location = new Point(28, 19), BackColor = colorActiveAccent };
			pnlHeader.Controls.Add(pnlTitleStripe);

			Label lblTitle = new Label { Name = "lblHeaderTitle", Text = "Genel Bakış", Font = new Font("Segoe UI", 17, FontStyle.Bold), ForeColor = colorCardTextPrimary, Location = new Point(40, 20), AutoSize = true };
			pnlHeader.Controls.Add(lblTitle);

			// Kullanıcı bilgisi (sağ üst)
			var pnlUser = new Panel { Size = new Size(260, 86), BackColor = Color.White, Anchor = AnchorStyles.Top | AnchorStyles.Right };
			pnlUser.Location = new Point(pnlRightContainer.Width - 270, 0);
			pnlUser.Anchor = AnchorStyles.Top | AnchorStyles.Right;

			var lblUserName = new Label { Text = _fullName, Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = colorCardTextPrimary, Location = new Point(0, 10), AutoSize = true, TextAlign = ContentAlignment.MiddleRight };
			var lblUserTitle = new Label { Text = _title, Font = new Font("Segoe UI", 8.5f), ForeColor = colorTextSecondary, Location = new Point(0, 32), AutoSize = true, TextAlign = ContentAlignment.MiddleRight };
			var lblUserDept = new Label { Text = _department, Font = new Font("Segoe UI", 8.5f), ForeColor = colorTextSecondary, Location = new Point(0, 53), AutoSize = true, TextAlign = ContentAlignment.MiddleRight };

			// Küçük avatar dairesi
			var pnlAvatar = new Panel { Size = new Size(38, 38), Location = new Point(215, 24), BackColor = colorSidebar };
			pnlAvatar.Paint += (s, e) => {
				e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
				using (var b = new System.Drawing.SolidBrush(colorSidebar))
					e.Graphics.FillEllipse(b, 0, 0, 37, 37);
				string ini = _fullName != null && _fullName.Length > 0 ? _fullName[0].ToString().ToUpper() : "?";
				using (var f = new Font("Segoe UI", 14, FontStyle.Bold))
				using (var b = new System.Drawing.SolidBrush(Color.White))
				{
					var sf = new System.Drawing.StringFormat { Alignment = System.Drawing.StringAlignment.Center, LineAlignment = System.Drawing.StringAlignment.Center };
					e.Graphics.DrawString(ini, f, b, new System.Drawing.RectangleF(0, 0, 38, 38), sf);
				}
			};

			pnlUser.Controls.Add(lblUserName);
			pnlUser.Controls.Add(lblUserTitle);
			pnlUser.Controls.Add(lblUserDept);
			pnlUser.Controls.Add(pnlAvatar);
			pnlHeader.Controls.Add(pnlUser);

			pnlContent = new Panel { Parent = pnlRightContainer, Dock = DockStyle.Fill, Padding = new Padding(40) };
			pnlContent.BringToFront();
		}

		private void SetupSidebarContent()
		{
			// ── Üst kırmızı şerit ──────────────────────────────────────
			var pnlRedTop = new Panel { Dock = DockStyle.Top, Height = 4, BackColor = colorActiveAccent };
			pnlSidebar.Controls.Add(pnlRedTop);

			// ── Logo — direkt sidebar üstünde, beyaz kutu yok ─────────────
			Panel pnlLogo = new Panel { Dock = DockStyle.Top, Height = 115, BackColor = colorSidebar };

			var pbLogo = new PictureBox
			{
				Size      = new Size(240, 80),
				Location  = new Point(20, 12),
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
						pbLogo.Image = System.Drawing.Image.FromFile(logoPath);
						logoLoaded = true;
						break;
					}
				}
			}
			catch { }
			pnlLogo.Controls.Add(pbLogo);

			// Logo yoksa metin fallback
			if (!logoLoaded)
			{
				var lblFallback = new Label
				{
					Text      = "TUSAŞ",
					Font      = new Font("Segoe UI", 20, FontStyle.Bold),
					ForeColor = Color.White,
					Location  = new Point(20, 20),
					AutoSize  = true
				};
				pnlLogo.Controls.Add(lblFallback);
			}

			// "GELİŞİM PUSULASI" alt yazı — kurumsal, okunabilir boyut
			var lblBrandSub = new Label
			{
				Text      = "GELİŞİM PUSULASI",
				Font      = new Font("Segoe UI", 9, FontStyle.Bold),
				ForeColor = Color.FromArgb(160, 190, 225),
				Location  = new Point(22, 94),
				AutoSize  = true
			};
			pnlLogo.Controls.Add(lblBrandSub);

			// Alt ayraç
			var pnlLogoDiv = new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = Color.FromArgb(40, 65, 110) };
			pnlLogo.Controls.Add(pnlLogoDiv);

			pnlSidebar.Controls.Add(pnlLogo);

			CreateMenuButton("Genel Bakış", true);
			if (_isHR) {
				CreateMenuButton("İK Yönetim Paneli", false);
				CreateMenuButton("Performans Analizi", false);
			}
			CreateMenuButton("Değerlendirmelerim", false);
			CreateMenuButton("AI Mentor Sohbet", false);

			if ((_title != null) && (_title.Contains("YÖNETİCİ") || _title.Contains("DİREKTÖR") || _title == "BAŞKAN"))
			{
				CreateMenuButton("Ekip Raporları", false);
			}

			CreateMenuButton("Raporlarım", false);
			CreateMenuButton("Gelişim Yolculuğum", false);

			if (_isHR)
			{
				CreateMenuButton("Akıllı Hatırlatıcılar", false);
			}

			Button btnExit = new Button
			{
				Text      = "⬡  Güvenli Çıkış",
				Dock      = DockStyle.Bottom,
				Height    = 54,
				FlatStyle = FlatStyle.Flat,
				BackColor = Color.FromArgb(15, 32, 65),
				ForeColor = Color.FromArgb(200, 215, 235),
				Font      = new Font("Segoe UI", 10, FontStyle.Regular),
				Cursor    = Cursors.Hand,
				TextAlign = ContentAlignment.MiddleLeft,
				Padding   = new Padding(14, 0, 0, 0)
			};
			btnExit.FlatAppearance.BorderSize = 0;
			btnExit.MouseEnter += (s, e) => { btnExit.BackColor = colorActiveAccent; btnExit.ForeColor = Color.White; };
			btnExit.MouseLeave += (s, e) => { btnExit.BackColor = Color.FromArgb(15, 32, 65); btnExit.ForeColor = Color.FromArgb(200, 215, 235); };
			btnExit.Click += (s, e) =>
			{
				// Oturumu kapat: Form1'i bul, alanlarını temizle, göster; dashboard'ı kapat
				Form1 loginForm = null;
				foreach (Form f in Application.OpenForms)
				{
					loginForm = f as Form1;
					if (loginForm != null) break;
				}
				if (loginForm != null)
					loginForm.ResetAndShow();
				else
				{
					var newLogin = new Form1();
					newLogin.Show();
				}
				this.Close();
			};
			pnlSidebar.Controls.Add(btnExit);
		}

		private void CreateMenuButton(string text, bool isActive)
		{
			Button btn = new Button { Dock = DockStyle.Top, Height = 52, Text = "     " + text, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 10.5f), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
			btn.FlatAppearance.BorderSize = 0;
			SetButtonActiveState(btn, isActive);
			pnlSidebar.Controls.Add(btn);
			btn.BringToFront();

			btn.Click += (s, e) => {
				if (!text.Contains("Akıllı Hatırlatıcılar"))
				{
					foreach (Control c in pnlSidebar.Controls)
						if (c is Button && c.Text != "Güvenli Çıkış") SetButtonActiveState((Button)c, false);

					SetButtonActiveState(btn, true);
					pnlHeader.Controls["lblHeaderTitle"].Text = text.Trim();
				}

				if (text.Contains("Genel Bakış")) LoadDashboardData();
				else if (text.Contains("İK Yönetim Paneli")) LoadHRView(true);
				else if (text.Contains("Performans Analizi")) { new PerformansAnalizDashboard().Show(); }
				else if (text.Contains("Ekip Raporları")) LoadHRView(false);
				else if (text.Contains("Değerlendirmelerim")) { EvaluationForm eval = new EvaluationForm(_userCode, _fullName); eval.Show(); }
				else if (text.Contains("AI Mentor Sohbet"))
				{
					EmployeeChatForm chat = new EmployeeChatForm(_userCode, _fullName, _department, GeminiApiKey);
					chat.Show();
				}
				else if (text.Contains("Raporlarım"))
				{
					MyReportsForm myReports = new MyReportsForm(_userCode, _fullName, _title, _department);
					myReports.Show();
				}
				else if (text.Contains("Gelişim Yolculuğum"))
				{
					LoadGelisimYolculugu(_userCode);
				}
				else if (text.Contains("Akıllı Hatırlatıcılar"))
				{
					DialogResult res = MessageBox.Show(
						"Sistemde 14 günden fazla aksiyon almayan çalışanlar tespit edilecek ve Yapay Zeka destekli e-posta bildirimleri oluşturulacaktır.\n\nİşlemi başlatmak istiyor musunuz?",
						"Akıllı Hatırlatıcı", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

					if (res == DialogResult.Yes)
					{
						DemoMailTetikle("Barış Tan", "Problem Çözme", "Kök Neden Analizi Eğitimi");
					}
				}
			};
		}

		private void SetButtonActiveState(Button btn, bool active)
		{
			if (active)
			{
				btn.BackColor = colorSidebarActive;
				btn.ForeColor = Color.White;
				btn.Font      = new Font("Segoe UI", 10.5f, FontStyle.Bold);
				if (btn.Controls.Count == 0)
				{
					Panel line = new Panel { Width = 4, Dock = DockStyle.Left, BackColor = colorActiveAccent };
					btn.Controls.Add(line);
				}
			}
			else
			{
				btn.BackColor = Color.Transparent;
				btn.ForeColor = Color.FromArgb(168, 185, 210);
				btn.Font      = new Font("Segoe UI", 10.5f, FontStyle.Regular);
				btn.Controls.Clear();
			}
		}

		private void LoadDashboardData()
		{
			pnlContent.Controls.Clear();
			int total = 0, completed = 0;
			try
			{
				object o1 = SqlHelper.ExecuteScalar($"SELECT COUNT(*) FROM ResponseHeaders WHERE RaterPersonelCode='{_userCode}'");
				if (o1 != null) total = Convert.ToInt32(o1);
				object o2 = SqlHelper.ExecuteScalar($"SELECT COUNT(*) FROM ResponseHeaders WHERE RaterPersonelCode='{_userCode}' AND Status='Completed'");
				if (o2 != null) completed = Convert.ToInt32(o2);
			}
			catch { }

			int pending = Math.Max(0, total - completed);
			FlowLayoutPanel flowStats = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 160, FlowDirection = FlowDirection.LeftToRight };
			pnlContent.Controls.Add(flowStats);

			flowStats.Controls.Add(CreateStatCard("Bekleyen", pending.ToString(), colorActiveAccent));
			flowStats.Controls.Add(CreateStatCard("Tamamlanan", completed.ToString(), Color.FromArgb(0, 200, 83)));
			flowStats.Controls.Add(CreateStatCard("Toplam Hedef", total.ToString(), Color.FromArgb(41, 121, 255)));

			CreateActionArea(pending);
		}

		private void LoadHRView(bool isFullAccess)
		{
			try
			{
				pnlContent.Controls.Clear();

				// --- Arama kutusu ---
				Panel pnlSearch = new Panel { Dock = DockStyle.Top, Height = 54, BackColor = Color.White };
				TextBox txtSearch = new TextBox
				{
					Text = "Personel Ara...",
					Font = new Font("Segoe UI", 11),
					ForeColor = Color.FromArgb(150, 160, 175),
					BackColor = Color.FromArgb(248, 250, 252),
					BorderStyle = BorderStyle.FixedSingle,
					Size = new Size(320, 32),
					Location = new Point(0, 12)
				};
				txtSearch.GotFocus  += (s, e) => { if (txtSearch.Text == "Personel Ara...") { txtSearch.Text = ""; txtSearch.ForeColor = Color.FromArgb(15, 23, 42); } };
				txtSearch.LostFocus += (s, e) => { if (string.IsNullOrWhiteSpace(txtSearch.Text)) { txtSearch.Text = "Personel Ara..."; txtSearch.ForeColor = Color.FromArgb(150, 160, 175); } };
				pnlSearch.Controls.Add(txtSearch);

				// --- Modern DataGridView ---
				DataGridView grid = new DataGridView
				{
					Dock = DockStyle.Fill,
					BackgroundColor = Color.White,
					BorderStyle = BorderStyle.None,
					CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
					GridColor = Color.FromArgb(226, 232, 240),
					RowHeadersVisible = false,
					AllowUserToAddRows = false,
					ReadOnly = true,
					AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
					SelectionMode = DataGridViewSelectionMode.FullRowSelect,
					EnableHeadersVisualStyles = false,
					RowTemplate = { Height = 48 }
				};

				// Başlık stili
				grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(0, 43, 92);
				grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
				grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10, FontStyle.Bold);
				grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(0, 43, 92);
				grid.ColumnHeadersHeight = 44;

				// Satır stili
				grid.DefaultCellStyle.Font = new Font("Segoe UI", 10);
				grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(224, 235, 255);
				grid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(15, 23, 42);
				grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 252);

				// Self-healing: atamalardan eksik form kabuklarını üret ki HR/genel görünüm ve
				// değerlendirme listeleri RaterAssignments ile tutarlı olsun.
				if (isFullAccess) ResponseHeaderSync.EnsureAll();
				else ResponseHeaderSync.EnsureForRater(_userCode);

				string query = isFullAccess
					? "SELECT RH.RateePersonelCode as [Kod], E.AdSoyad as [Personel], E.Unvan, MAX(RH.Status) as [Durum]" +
					  " FROM ResponseHeaders RH" +
					  " JOIN Employees_Tablo E ON RH.RateePersonelCode = E.PersonelCode" +
					  " WHERE RH.RateePersonelCode <> '" + _userCode + "'" +
					  " AND ISNULL(E.Yaka, '') <> 'Mavi'" +
					  " GROUP BY RH.RateePersonelCode, E.AdSoyad, E.Unvan"
					: "SELECT RA_mine.RateePersonelCode AS [Kod], E.AdSoyad AS [Personel], E.Unvan," +
					  " CASE WHEN NOT EXISTS (" +
					  "   SELECT 1 FROM RaterAssignments RA_all" +
					  "   LEFT JOIN ResponseHeaders RH_all" +
					  "     ON RH_all.RateePersonelCode = RA_all.RateePersonelCode" +
					  "    AND RH_all.RaterPersonelCode = RA_all.RaterPersonelCode" +
					  "   WHERE RA_all.RateePersonelCode = RA_mine.RateePersonelCode" +
					  "     AND ISNULL(RH_all.Status,'') <> 'Completed'" +
					  " ) THEN 'Completed' ELSE 'Draft' END AS [Durum]" +
					  " FROM RaterAssignments RA_mine" +
					  " JOIN Employees_Tablo E ON RA_mine.RateePersonelCode = E.PersonelCode" +
					  " WHERE RA_mine.RaterPersonelCode = '" + _userCode + "'" +
					  " AND RA_mine.RateePersonelCode <> '" + _userCode + "'" +
					  " GROUP BY RA_mine.RateePersonelCode, E.AdSoyad, E.Unvan";

				DataTable _fullData = SqlHelper.GetDataTable(query);

				// Buton sütununu DataSource bağlanmadan ÖNCE ekle — böylece DisplayIndex sırası bozulmaz
				var btnCol = new DataGridViewButtonColumn
				{
					Name = "btnRapor",
					HeaderText = "Rapor",
					Text = "Raporu Görüntüle",
					UseColumnTextForButtonValue = true,
					FlatStyle = FlatStyle.Flat,
					AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
					Width = 155,
					MinimumWidth = 155
				};
				btnCol.DefaultCellStyle.BackColor = Color.FromArgb(0, 43, 92);
				btnCol.DefaultCellStyle.ForeColor = Color.White;
				btnCol.DefaultCellStyle.SelectionBackColor = Color.FromArgb(0, 43, 92);
				btnCol.DefaultCellStyle.SelectionForeColor = Color.White;
				btnCol.DefaultCellStyle.Font = new Font("Segoe UI", 9, FontStyle.Bold);
				btnCol.DefaultCellStyle.Padding = new Padding(15, 8, 15, 8);
				btnCol.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
				btnCol.HeaderCell.Style.BackColor = Color.FromArgb(0, 43, 92);
				btnCol.HeaderCell.Style.ForeColor = Color.White;
				btnCol.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
				grid.Columns.Add(btnCol);

				// DataSource bağla — veri sütunları butonun SAĞINA eklenir
				grid.AutoGenerateColumns = true;
				grid.DataSource = _fullData;

				// DataBindingComplete: sütunları kesin olarak düzenle
				grid.DataBindingComplete += (s, e) => {
					// Kod gizle
					foreach (DataGridViewColumn col in grid.Columns)
						if (col.Name.Equals("Kod", StringComparison.OrdinalIgnoreCase)) { col.Visible = false; break; }

					// Buton en sağa
					grid.Columns["btnRapor"].DisplayIndex = grid.Columns.Count - 1;

					// Fill ağırlıkları
					if (grid.Columns.Contains("Personel")) grid.Columns["Personel"].FillWeight = 35;
					if (grid.Columns.Contains("Unvan"))    grid.Columns["Unvan"].FillWeight    = 30;
					if (grid.Columns.Contains("Durum"))    grid.Columns["Durum"].FillWeight    = 20;
				};

				// Durum sütunu çevirisi + renklendirme
				grid.CellFormatting += (s, e) => {
					if (e.RowIndex < 0) return;

					// Durum hücresi çevirisi
					if (grid.Columns[e.ColumnIndex].Name == "Durum" && e.Value != null)
					{
						string rawStatus = e.Value.ToString();
						if (rawStatus == "Completed")
						{
							e.Value = "Tamamlandı";
							e.CellStyle.ForeColor = Color.FromArgb(39, 174, 96);
							e.CellStyle.Font = new Font("Segoe UI", 10, FontStyle.Bold);
							e.FormattingApplied = true;
						}
						else if (rawStatus == "Draft")
						{
							e.Value = "Bekliyor";
							e.CellStyle.ForeColor = Color.FromArgb(230, 126, 34);
							e.FormattingApplied = true;
						}
					}

				};

				grid.CellPainting += (s, e) => {
					if (e.RowIndex < 0 || !grid.Columns[e.ColumnIndex].Name.Equals("btnRapor")) return;

					string rawDurum = grid.Rows[e.RowIndex].Cells["Durum"].Value?.ToString() ?? "";
					bool isCompleted = rawDurum == "Completed";

					// Hücre arka planını satır rengiyle temiz boyuyoruz (lacivert kenar kalmasın)
					Color rowBack = grid.Rows[e.RowIndex].Selected
						? Color.FromArgb(224, 235, 255)
						: (e.RowIndex % 2 == 1 ? Color.FromArgb(248, 250, 252) : Color.White);
					using (var bgBrush = new System.Drawing.SolidBrush(rowBack))
						e.Graphics.FillRectangle(bgBrush, e.CellBounds);

					Color btnBack = isCompleted ? Color.FromArgb(0, 43, 92) : Color.FromArgb(203, 213, 225);
					Color btnFore = isCompleted ? Color.White : Color.FromArgb(120, 130, 145);

					Rectangle rect = new Rectangle(e.CellBounds.X + 12, e.CellBounds.Y + 8,
						e.CellBounds.Width - 24, e.CellBounds.Height - 16);

					using (var brush = new System.Drawing.SolidBrush(btnBack))
						e.Graphics.FillRectangle(brush, rect);

					using (var font = new Font("Segoe UI", 9, FontStyle.Bold))
					using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
					using (var foreBrush = new System.Drawing.SolidBrush(btnFore))
						e.Graphics.DrawString("Raporu Görüntüle", font, foreBrush, rect, sf);

					e.Handled = true;
				};

				grid.CellContentClick += (s, e) => {
					if (e.RowIndex >= 0 && grid.Columns[e.ColumnIndex].Name == "btnRapor")
					{
						string status = grid.Rows[e.RowIndex].Cells["Durum"].Value?.ToString() ?? "";
						if (status != "Completed")
						{
							MessageBox.Show("Bu personelin değerlendirme süreci henüz tamamlanmadığı için rapor oluşturulamaz.", "Rapor Hazır Değil", MessageBoxButtons.OK, MessageBoxIcon.Warning);
							return;
						}
						string pCode = grid.Rows[e.RowIndex].Cells["Kod"].Value.ToString();
						string pName = grid.Rows[e.RowIndex].Cells["Personel"].Value.ToString();
						string pTitle = grid.Rows[e.RowIndex].Cells["Unvan"].Value.ToString();

						ReportForm rpt = new ReportForm(pCode, pName, pTitle);
						rpt.Show();
					}
				};

				// Anlık arama filtresi
				txtSearch.TextChanged += (s, e) => {
					string filter = (txtSearch.Text == "Personel Ara..." ? "" : txtSearch.Text.Trim()).Replace("'", "''");
					try
					{
						_fullData.DefaultView.RowFilter = string.IsNullOrEmpty(filter)
							? ""
							: $"Personel LIKE '%{filter}%' OR Unvan LIKE '%{filter}%'";
					}
					catch { }
				};

				pnlContent.Controls.Add(grid);
				pnlContent.Controls.Add(pnlSearch);
			}
			catch (Exception ex) { MessageBox.Show("Liste yüklenirken hata: " + ex.Message); }
		}

		private Panel CreateStatCard(string title, string value, Color stripColor)
		{
			Panel card = new Panel { Size = new Size(260, 130), BackColor = Color.White, Margin = new Padding(0, 0, 20, 0) };
			// Üst renk şeridi
			card.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 4, BackColor = stripColor });
			// Alt kenarlık
			card.Paint += (s, e) => {
				using (var pen = new System.Drawing.Pen(colorHeaderBorder, 1))
					e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
			};
			card.Controls.Add(new Label
			{
				Text     = value,
				Font     = new Font("Segoe UI", 34, FontStyle.Bold),
				ForeColor = colorCardTextPrimary,
				Location = new Point(20, 26),
				AutoSize = true
			});
			card.Controls.Add(new Label
			{
				Text      = title.ToUpper(),
				Font      = new Font("Segoe UI", 8, FontStyle.Bold),
				ForeColor = colorTextSecondary,
				Location  = new Point(22, 90),
				AutoSize  = true
			});
			return card;
		}

		private void CreateActionArea(int pendingCount)
		{
			Panel pnlAction = new Panel { Parent = pnlContent, Location = new Point(40, 220), Size = new Size(820, 200), BackColor = Color.White };
			pnlAction.Controls.Add(new Label { Text = pendingCount > 0 ? "Değerlendirmeleriniz Bekliyor" : "Tebrikler! Tüm Görevler Tamam.", Font = new Font("Segoe UI", 18, FontStyle.Bold), ForeColor = colorCardTextPrimary, Location = new Point(30, 30), AutoSize = true });
			pnlAction.Controls.Add(new Label { Text = "2026 Performans dönemine ait değerlendirmeleri tamamlamak için butona tıklayın.", Font = new Font("Segoe UI", 11), ForeColor = colorTextSecondary, Location = new Point(30, 70), Size = new Size(600, 30) });

			Button btnStart = new Button { Text = "BAŞLA    →", Size = new Size(200, 50), Location = new Point(30, 120), FlatStyle = FlatStyle.Flat, BackColor = colorActiveAccent, ForeColor = Color.White, Font = new Font("Segoe UI Bold", 11), Cursor = Cursors.Hand };
			btnStart.FlatAppearance.BorderSize = 0;
			btnStart.Click += (s, e) => { EvaluationForm eval = new EvaluationForm(_userCode, _fullName); eval.Show(); };
			pnlAction.Controls.Add(btnStart);
		}

		private void LoadGelisimYolculugu(string sicilNo)
		{
			pnlContent.Controls.Clear();
			FlowLayoutPanel flpGelisim = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(20), BackColor = colorBackground };
			pnlContent.Controls.Add(flpGelisim);
			try
			{
				string sql = "EXEC dbo.GetEmployeeDevelopmentPlan @RateeCode = @code";
				SqlParameter[] p = { new SqlParameter("@code", sicilNo) };
				DataTable dt = SqlHelper.GetDataTable(sql, p);
				if (dt.Rows.Count == 0)
				{
					Label lblBos = new Label { Text = "Henüz tamamlanmış bir gelişim planınız bulunmamaktadır.", Font = new Font("Segoe UI", 12), AutoSize = true, ForeColor = colorTextSecondary };
					flpGelisim.Controls.Add(lblBos); return;
				}
				foreach (DataRow row in dt.Rows)
				{
					Panel pnlKart = new Panel { Size = new Size(flpGelisim.Width - 60, 160), BackColor = Color.White, Margin = new Padding(0, 0, 0, 15) };
					pnlKart.Region = System.Drawing.Region.FromHrgn(CreateRoundRectRgn(0, 0, pnlKart.Width, pnlKart.Height, 15, 15));
					string yetkinlikAdi = row["CompetencyName"].ToString(); string egitimAdi = row["TrainingName"].ToString();
					Label lblYetkinlik = new Label { Text = yetkinlikAdi.ToUpper(), Font = new Font("Segoe UI", 9, FontStyle.Bold), ForeColor = colorActiveAccent, Location = new Point(20, 15), AutoSize = true };
					Label lblEgitimAdi = new Label { Text = egitimAdi, Font = new Font("Segoe UI", 12, FontStyle.Bold), ForeColor = colorCardTextPrimary, Location = new Point(20, 40), AutoSize = true };
					Label lblSkor = new Label { Text = "Ortalama Puan: " + Convert.ToDouble(row["AverageScore"]).ToString("0.00"), Font = new Font("Segoe UI", 9), ForeColor = colorTextSecondary, Location = new Point(20, 75), AutoSize = true };
					Button btnGit = new Button { Text = "EĞİTİME GİT", Size = new Size(130, 35), Location = new Point(pnlKart.Width - 150, 20), BackColor = Color.FromArgb(41, 128, 185), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8, FontStyle.Bold), Cursor = Cursors.Hand };
					btnGit.FlatAppearance.BorderSize = 0; string link = row["TrainingLink"].ToString();
					// Eşleşen eğitim/atama yoksa (link boş) butonu pasifleştir — kart yine listelenir
					if (string.IsNullOrEmpty(link))
					{
						btnGit.Enabled = false;
						btnGit.BackColor = Color.FromArgb(203, 213, 225);
						btnGit.ForeColor = Color.FromArgb(120, 130, 145);
						btnGit.Cursor = Cursors.Default;
					}
					btnGit.Click += (s, e) => { if (!string.IsNullOrEmpty(link)) System.Diagnostics.Process.Start(link); };
					Button btnAI = new Button { Text = "✨ AI MENTOR'A SOR", Size = new Size(130, 35), Location = new Point(pnlKart.Width - 150, 65), BackColor = Color.FromArgb(46, 204, 113), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8, FontStyle.Bold), Cursor = Cursors.Hand };
					btnAI.FlatAppearance.BorderSize = 0;
					btnAI.Click += (s, e) => {
						EmployeeChatForm chat = new EmployeeChatForm(_userCode, _fullName, _department, GeminiApiKey); chat.Show();
						Control[] txtControls = chat.Controls.Find("txtMessage", true);
						if (txtControls.Length > 0 && txtControls[0] is TextBox txtMessage) { txtMessage.Text = $"Merhaba! {yetkinlikAdi} yetkinliğimi geliştirmek istiyorum..."; }
					};
					Button btnTamamla = new Button { Text = "✔ TAMAMLADIM", Size = new Size(130, 35), Location = new Point(pnlKart.Width - 150, 110), BackColor = Color.White, ForeColor = Color.FromArgb(46, 204, 113), FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8, FontStyle.Bold), Cursor = Cursors.Hand };
					btnTamamla.FlatAppearance.BorderColor = Color.FromArgb(46, 204, 113);
					btnTamamla.Click += (s, e) => {
						string kazanim = KazanimAciklamasiIste(egitimAdi); if (string.IsNullOrEmpty(kazanim)) return;
						MessageBox.Show("Tebrikler! Gelişim adımınız başarıyla kaydedildi.", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
						btnTamamla.Text = "TAMAMLANDI"; btnTamamla.Enabled = false; btnTamamla.BackColor = Color.FromArgb(240, 240, 240);
					};
					pnlKart.Controls.Add(lblYetkinlik); pnlKart.Controls.Add(lblEgitimAdi); pnlKart.Controls.Add(lblSkor);
					pnlKart.Controls.Add(btnGit); pnlKart.Controls.Add(btnAI); pnlKart.Controls.Add(btnTamamla);
					flpGelisim.Controls.Add(pnlKart);
				}
			}
			catch (Exception ex) { MessageBox.Show("Yükleme hatası: " + ex.Message); }
		}

		private string KazanimAciklamasiIste(string egitimAdi)
		{
			Form prompt = new Form() { Width = 500, Height = 350, FormBorderStyle = FormBorderStyle.FixedDialog, Text = "Gelişim Adımı Tamamlama", StartPosition = FormStartPosition.CenterParent, BackColor = Color.White, MaximizeBox = false, MinimizeBox = false };
			Label lblText = new Label() { Left = 20, Top = 20, Width = 450, Height = 40, Font = new Font("Segoe UI", 10, FontStyle.Bold), Text = $"'{egitimAdi}' gelişim adımını tamamladınız!\nLütfen nasıl bir yol izlediğinizi seçin:" };
			RadioButton rbOriginal = new RadioButton { Text = "Sistemdeki Standart Eğitimi Tamamladım", Left = 30, Top = 70, Width = 400, Checked = true, Font = new Font("Segoe UI", 9) };
			RadioButton rbAI = new RadioButton { Text = "AI Mentor'un Önerdiği Alternatif Yolu Tamamladım", Left = 30, Top = 95, Width = 400, Font = new Font("Segoe UI", 9) };
			Label lblComment = new Label { Text = "Düşünceleriniz (En az 20 karakter):", Left = 20, Top = 135, AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
			TextBox txtBox = new TextBox() { Left = 20, Top = 160, Width = 440, Height = 80, Multiline = true, Font = new Font("Segoe UI", 10) };
			Button btnOnay = new Button() { Text = "GELİŞİMİ KAYDET", Left = 310, Width = 150, Height = 35, Top = 255, BackColor = Color.FromArgb(46, 204, 113), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9, FontStyle.Bold), Cursor = Cursors.Hand };
			btnOnay.FlatAppearance.BorderSize = 0; string result = "";
			btnOnay.Click += (sender, e) => {
				if (txtBox.Text.Trim().Length < 20) { MessageBox.Show("Lütfen düşüncelerinizi detaylandırın.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
				string prefix = rbOriginal.Checked ? "[STANDART] " : "[AI] "; result = prefix + txtBox.Text.Trim(); prompt.Close();
			};
			prompt.Controls.Add(lblText); prompt.Controls.Add(rbOriginal); prompt.Controls.Add(rbAI); prompt.Controls.Add(lblComment); prompt.Controls.Add(txtBox); prompt.Controls.Add(btnOnay); prompt.ShowDialog(); return result;
		}

		private void ShowMockEmailPreview(string toName, string subject, string body)
		{
			Form emailForm = new Form() { Width = 650, Height = 550, FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterParent, Text = "Sistem Otomatik Mail Gönderimi", BackColor = Color.White };
			Panel pnlHeader = new Panel() { Dock = DockStyle.Top, Height = 100, BackColor = Color.FromArgb(240, 242, 245) };
			Label lblFrom = new Label() { Text = "Kimden: LiftUp 360 AI Mentor Sistemi <noreply@liftup360.com>", AutoSize = true, Location = new Point(20, 15), Font = new Font("Segoe UI", 9, FontStyle.Bold) };
			string emailSafe = toName.ToLower().Replace(" ", ".").Replace("ğ", "g").Replace("ş", "s").Replace("ı", "i").Replace("ü", "u").Replace("ö", "o").Replace("ç", "c");
			Label lblTo = new Label() { Text = $"Kime: {toName} <{emailSafe}@uedas.com.tr>", AutoSize = true, Location = new Point(20, 40), Font = new Font("Segoe UI", 9) };
			Label lblSubject = new Label() { Text = $"Konu: {subject}", AutoSize = true, Location = new Point(20, 65), Font = new Font("Segoe UI", 9, FontStyle.Bold), ForeColor = Color.FromArgb(41, 128, 185) };
			pnlHeader.Controls.Add(lblFrom); pnlHeader.Controls.Add(lblTo); pnlHeader.Controls.Add(lblSubject);
			TextBox txtBody = new TextBox() { Multiline = true, ReadOnly = true, Text = body.Replace("\n", Environment.NewLine), Font = new Font("Segoe UI", 10), Dock = DockStyle.Fill, BackColor = Color.White, BorderStyle = BorderStyle.None, ScrollBars = ScrollBars.Vertical };
			Panel pnlContent = new Panel() { Dock = DockStyle.Fill, Padding = new Padding(20) }; pnlContent.Controls.Add(txtBody);
			Panel pnlBottom = new Panel() { Dock = DockStyle.Bottom, Height = 60, BackColor = Color.White };
			Button btnKapat = new Button() { Text = "PENCEREYİ KAPAT", Size = new Size(180, 35), Location = new Point(225, 10), BackColor = Color.FromArgb(41, 128, 185), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9, FontStyle.Bold), Cursor = Cursors.Hand };
			btnKapat.FlatAppearance.BorderSize = 0; btnKapat.Click += (s, e) => emailForm.Close();
			Label lblInfo = new Label() { Text = "✔ E-posta başarıyla iletilmiştir.", ForeColor = Color.Green, AutoSize = true, Location = new Point(20, 20), Font = new Font("Segoe UI", 8, FontStyle.Italic) };
			pnlBottom.Controls.Add(lblInfo); pnlBottom.Controls.Add(btnKapat);
			emailForm.Controls.Add(pnlContent); emailForm.Controls.Add(pnlBottom); emailForm.Controls.Add(pnlHeader); emailForm.ShowDialog();
		}

		private async void DemoMailTetikle(string calisanAd, string yetkinlik, string egitim)
		{
			try
			{
				MessageBox.Show("Sistem tarama yapıyor ve Yapay Zeka mail taslağını hazırlıyor. Lütfen bekleyiniz...", "LiftUp 360", MessageBoxButtons.OK, MessageBoxIcon.Information);
				this.Cursor = Cursors.WaitCursor;

				GeminiAIService ai = new GeminiAIService();
				string aiMail = await ai.GenerateReminderEmailAsync(calisanAd, yetkinlik, egitim);

				this.Cursor = Cursors.Default;

				if (aiMail.Contains("HATA"))
				{
					MessageBox.Show(aiMail, "Bağlantı Sorunu", MessageBoxButtons.OK, MessageBoxIcon.Error);
				}
				else
				{
					ShowMockEmailPreview(calisanAd, "Gelişim Planı Hatırlatması", aiMail);
				}
			}
			catch (Exception ex)
			{
				this.Cursor = Cursors.Default;
				MessageBox.Show("Beklenmedik bir hata oluştu: " + ex.Message);
			}
		}
	}
}