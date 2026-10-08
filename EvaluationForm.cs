using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WindowsFormsApp1
{
    public partial class EvaluationForm : Form
    {
        // ── Kullanıcı ─────────────────────────────────────────────────────
        private string _raterCode;
        private string _raterName;
        private string _selectedRateeCode = "";
        private int    _selectedHeaderID  = 0;

        // Seçilen cevaplar: ResponseItemID → puan (1-5)
        private Dictionary<int, int> _answers = new Dictionary<int, int>();

        // ── Orijinal Renk Paleti (sol panel için) ─────────────────────────
        private Color clrBackground = Color.FromArgb(245, 247, 250);
        private Color clrSidebar    = Color.White;
        private Color clrSuccess    = Color.FromArgb(46, 204, 113);
        private Color clrPending    = Color.FromArgb(241, 196, 15);
        private Color clrDanger     = Color.FromArgb(231, 76, 60);
        private Color clrTextDark   = Color.FromArgb(44, 62, 80);
        private Color clrTextLight  = Color.FromArgb(149, 165, 166);

        // ── Sağ taraf (soru kartları) renk paleti ─────────────────────────
        private readonly Color C_Bg         = Color.FromArgb(243, 246, 250);
        private readonly Color C_Card       = Color.White;
        private readonly Color C_CardBorder = Color.FromArgb(226, 232, 240);
        private readonly Color C_TextDark2  = Color.FromArgb(30,  40,  60);
        private readonly Color C_TextSub    = Color.FromArgb(100, 116, 139);
        private readonly Color C_Navy       = Color.FromArgb(0,  43,  92);
        private readonly Color C_Red        = Color.FromArgb(227,  6,  19);
        private readonly Color C_Green      = Color.FromArgb(46, 204, 113);

        // Dinamik puan renkleri
        private readonly Color[] ScoreColors = {
            Color.Empty,
            Color.FromArgb(231, 76,  60),   // 1 Kırmızı
            Color.FromArgb(230, 126, 34),   // 2 Turuncu
            Color.FromArgb(241, 196, 15),   // 3 Amber
            Color.FromArgb(39,  174, 96),   // 4 Açık Yeşil
            Color.FromArgb(46,  204, 113)   // 5 Koyu Yeşil
        };
        private readonly string[] ScoreLabels = {
            "", "Gelişim\nAlanı", "Beklentinin\nAltında",
            "Beklentileri\nKarşılıyor", "Beklentileri\nAşıyor", "Rol\nModel"
        };

        // ── UI Refs ───────────────────────────────────────────────────────
        private TableLayoutPanel tlpMain;
        private Panel             pnlSidebarContainer;
        private Panel             pnlContentContainer;
        private FlowLayoutPanel   flowPersonelList;
        private FlowLayoutPanel   flowQuestions;

        // ─────────────────────────────────────────────────────────────────
        public EvaluationForm(string raterCode, string raterName)
        {
            InitializeComponent();
            _raterCode = raterCode;
            _raterName = raterName;

            this.Text          = "Performans Değerlendirme";
            this.Size          = new Size(1400, 850);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor     = clrBackground;

            BuildLayout();

            // Self-healing senkron TEK SEFER (form açılışında): bu değerlendiricinin
            // atamalarına karşılık gelen eksik form kabukları + soru (ResponseItems) üret.
            // Kişiden kişiye geçişte (LoadPersonelList) tekrar çalıştırılmaz → kasma olmaz.
            ResponseHeaderSync.EnsureForRater(_raterCode);

            LoadPersonelList();
        }

        private void EvaluationForm_Load(object sender, EventArgs e) { }

        // ════════════════════════════════════════════════════════════════
        //  LAYOUT — Sol panel: orijinal hali, Sağ panel: yeni tasarım
        // ════════════════════════════════════════════════════════════════
        private void BuildLayout()
        {
            this.Controls.Clear();

            // Ana ızgara
            tlpMain = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 2,
                RowCount    = 1
            };
            tlpMain.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 360F));
            tlpMain.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            this.Controls.Add(tlpMain);

            // ── SOL PANEL (ORİJİNAL) ─────────────────────────────────────
            pnlSidebarContainer = new Panel
            {
                Dock      = DockStyle.Fill,
                BackColor = clrSidebar,
                Padding   = new Padding(15)
            };
            tlpMain.Controls.Add(pnlSidebarContainer, 0, 0);

            Button btnBack = new Button
            {
                Text      = "←  PANELE DÖN",
                Dock      = DockStyle.Top,
                Height    = 45,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = clrDanger,
                Font      = new Font("Segoe UI", 10, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Cursor    = Cursors.Hand
            };
            btnBack.FlatAppearance.BorderSize = 0;
            btnBack.Click += (s, e) => this.Close();
            pnlSidebarContainer.Controls.Add(btnBack);

            Label lblSideTitle = new Label
            {
                Text      = "Personel Listesi",
                Font      = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = clrTextDark,
                Dock      = DockStyle.Top,
                Height    = 50,
                TextAlign = ContentAlignment.MiddleLeft
            };
            pnlSidebarContainer.Controls.Add(lblSideTitle);

            btnBack.BringToFront();
            lblSideTitle.SendToBack();

            flowPersonelList = new FlowLayoutPanel
            {
                Dock          = DockStyle.Fill,
                AutoScroll    = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents  = false,
                Padding       = new Padding(0, 10, 0, 0)
            };
            pnlSidebarContainer.Controls.Add(flowPersonelList);
            flowPersonelList.BringToFront();

            // ── SAĞ PANEL (YENİ TASARIM KORUNDU) ─────────────────────────
            pnlContentContainer = new Panel
            {
                Dock      = DockStyle.Fill,
                BackColor = C_Bg,
                Padding   = new Padding(0)
            };
            tlpMain.Controls.Add(pnlContentContainer, 1, 0);

            pnlContentContainer.Controls.Add(new Label
            {
                Name      = "lblWelcome",
                Text      = "Değerlendirmeye başlamak için\nsoldaki listeden bir çalışan seçiniz.",
                Font      = new Font("Segoe UI", 16),
                ForeColor = clrTextLight,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock      = DockStyle.Fill
            });
        }

        // ════════════════════════════════════════════════════════════════
        //  KİŞİ LİSTESİ (ORİJİNAL)
        // ════════════════════════════════════════════════════════════════
        private void LoadPersonelList()
        {
            flowPersonelList.SuspendLayout();
            flowPersonelList.Controls.Clear();

            // ResponseHeaders taban tablo olarak kullan: her satır bir değerlendirme formu.
            // RaterAssignments JOIN kullanmak Cartesian product üretir (aynı kişi için
            // birden fazla RaterTypeID varsa kart tekrarlanır). RH satırı = 1 form = 1 kart.
            string query = @"
                SELECT rh.ResponseHeaderID, et.PersonelCode, et.AdSoyad AS FullName,
                       et.Unvan AS Title, rh.Status,
                       CASE rh.RaterTypeID
                           WHEN 1 THEN 'Kendiniz'
                           WHEN 2 THEN '1. Yönetici Olarak'
                           WHEN 3 THEN '2. Yönetici Olarak'
                           WHEN 4 THEN 'Ekip Arkadaşı Olarak'
                           WHEN 5 THEN 'Ast Olarak'
                           WHEN 6 THEN 'Ortak İş Yürütülen'
                           ELSE      'Değerlendirici'
                       END AS RolAdi
                FROM ResponseHeaders rh
                JOIN Employees_Tablo et ON et.PersonelCode = rh.RateePersonelCode
                WHERE rh.RaterPersonelCode = @code
                  AND rh.CycleID = (SELECT TOP 1 CycleID FROM EvaluationCycles WHERE IsActive = 1)
                ORDER BY CASE WHEN rh.Status='Completed' THEN 2 ELSE 1 END, et.AdSoyad ASC";

            try
            {
                var dt = SqlHelper.GetDataTable(query,
                    new[] { new SqlParameter("@code", _raterCode) });
                foreach (DataRow row in dt.Rows)
                    AddPersonCard(row);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Liste hatası: " + ex.Message);
            }
            finally
            {
                flowPersonelList.ResumeLayout();
            }
        }

        private void AddPersonCard(DataRow row)
        {
            int    headerID   = Convert.ToInt32(row["ResponseHeaderID"]);
            string pCode      = row["PersonelCode"].ToString();
            string pName      = row["FullName"].ToString();
            string pTitle     = row["Title"].ToString();
            bool   isCompleted= row["Status"].ToString() == "Completed";
            bool   isSelected = (headerID == _selectedHeaderID);
            string rolAdi     = row.Table.Columns.Contains("RolAdi") ? row["RolAdi"].ToString() : "";

            Panel card = new Panel
            {
                Size      = new Size(310, 100),
                Margin    = new Padding(0, 0, 0, 10),
                BackColor = isSelected ? Color.FromArgb(235, 245, 251) : Color.White,
                Cursor    = Cursors.Hand
            };
            card.Paint += (s, e) =>
                ControlPaint.DrawBorder(e.Graphics, card.ClientRectangle,
                    Color.LightGray, ButtonBorderStyle.Solid);

            Color stripColor = isCompleted ? clrSuccess : clrPending;
            Panel strip = new Panel { Dock = DockStyle.Left, Width = 6, BackColor = stripColor };
            card.Controls.Add(strip);

            Label lblName = new Label
            {
                Text      = pName,
                Font      = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                ForeColor = isCompleted ? Color.Gray : clrTextDark,
                Location  = new Point(15, 10),
                AutoSize  = true
            };
            Label lblTitle = new Label
            {
                Text      = pTitle,
                Font      = new Font("Segoe UI", 8.5f),
                ForeColor = clrTextLight,
                Location  = new Point(15, 30),
                AutoSize  = true
            };
            Label lblRol = new Label
            {
                Text      = rolAdi,
                Font      = new Font("Segoe UI", 8f, FontStyle.Italic),
                ForeColor = Color.FromArgb(41, 121, 255),
                Location  = new Point(15, 50),
                AutoSize  = true
            };
            Label lblStatus = new Label
            {
                Text      = isCompleted ? "TAMAMLANDI" : "BEKLİYOR",
                Font      = new Font("Segoe UI", 8, FontStyle.Bold),
                ForeColor = stripColor,
                Location  = new Point(15, 75),
                AutoSize  = true
            };

            card.Controls.Add(lblName);
            card.Controls.Add(lblTitle);
            card.Controls.Add(lblRol);
            card.Controls.Add(lblStatus);

            EventHandler clickEvent = (s, e) => {
                _selectedRateeCode = pCode;
                _selectedHeaderID  = headerID;
                _answers.Clear();
                LoadPersonelList();
                LoadQuestions(pName, headerID, isCompleted);
            };

            card.Click    += clickEvent;
            lblName.Click += clickEvent;
            lblTitle.Click+= clickEvent;
            strip.Click   += clickEvent;
            lblStatus.Click+=clickEvent;

            flowPersonelList.Controls.Add(card);
        }

        // ════════════════════════════════════════════════════════════════
        //  SORULAR — YENİ TASARIM KORUNDU
        // ════════════════════════════════════════════════════════════════
        private void LoadQuestions(string personName, int headerID, bool isReadOnly)
        {
            pnlContentContainer.Controls.Clear();

            // Üst başlık şeridi
            var pnlHeader = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 80,
                BackColor = C_Card,
                Padding   = new Padding(36, 0, 36, 0)
            };
            pnlHeader.Paint += (s, e) => {
                using (var pen = new Pen(C_CardBorder, 1))
                    e.Graphics.DrawLine(pen, 0, pnlHeader.Height - 1, pnlHeader.Width, pnlHeader.Height - 1);
                using (var b = new SolidBrush(C_Red))
                    e.Graphics.FillRectangle(b, 0, 20, 4, 40);
            };

            pnlHeader.Controls.Add(new Label
            {
                Text      = personName + " — Performans Değerlendirmesi",
                Font      = new Font("Segoe UI Semibold", 15),
                ForeColor = C_TextDark2,
                Location  = new Point(20, 20),
                AutoSize  = true
            });
            pnlHeader.Controls.Add(new Label
            {
                Text      = isReadOnly
                    ? "Bu değerlendirme tamamlanmıştır.  (Salt Okunur)"
                    : "Aşağıdaki yetkinlik sorularını 1-5 arasında puanlayınız.",
                Font      = new Font("Segoe UI", 9.5f),
                ForeColor = isReadOnly ? C_Green : C_TextSub,
                Location  = new Point(20, 50),
                AutoSize  = true
            });
            pnlContentContainer.Controls.Add(pnlHeader);

            // Soru akışı
            flowQuestions = new FlowLayoutPanel
            {
                Dock          = DockStyle.Fill,
                AutoScroll    = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents  = false,
                Padding       = new Padding(32, 20, 32, 60),
                BackColor     = C_Bg
            };
            pnlContentContainer.Controls.Add(flowQuestions);
            flowQuestions.BringToFront();

            string sql = @"
                SELECT ri.ResponseItemID, q.QuestionText, c.CompetencyName, ri.NumericAnswer
                FROM ResponseItems ri
                JOIN Questions q ON q.QuestionID = ri.QuestionID
                JOIN Competencies c ON c.CompetencyID = q.CompetencyID
                WHERE ri.ResponseHeaderID = @hid
                ORDER BY c.CompetencyName, q.SortOrder";

            var dt = SqlHelper.GetDataTable(sql,
                new[] { new SqlParameter("@hid", headerID) });

            if (dt.Rows.Count == 0)
            {
                flowQuestions.Controls.Add(new Label
                {
                    Text = "Soru bulunamadı.", ForeColor = Color.Red, AutoSize = true
                });
                return;
            }

            foreach (DataRow row in dt.Rows)
                AddQuestionCard(row, isReadOnly);

            if (!isReadOnly)
            {
                var btnSave = new Button
                {
                    Text      = "Değerlendirmeyi Tamamla  →",
                    Font      = new Font("Segoe UI Semibold", 11),
                    BackColor = C_Navy,
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Size      = new Size(320, 52),
                    Margin    = new Padding(0, 24, 0, 40),
                    Cursor    = Cursors.Hand
                };
                btnSave.FlatAppearance.BorderSize = 0;
                btnSave.MouseEnter += (s, e) => btnSave.BackColor = Color.FromArgb(8, 16, 34);
                btnSave.MouseLeave += (s, e) => btnSave.BackColor = C_Navy;
                btnSave.Click      += (s, e) => SaveData();
                flowQuestions.Controls.Add(btnSave);
            }
        }

        private Label MakeCompetencyHeader(string compName)
        {
            return new Label
            {
                Text      = compName.ToUpper(),
                Font      = new Font("Segoe UI", 8, FontStyle.Bold),
                ForeColor = C_Navy,
                AutoSize  = false,
                Size      = new Size(flowQuestions.ClientSize.Width - 64, 28),
                Margin    = new Padding(0, 16, 0, 4),
                BackColor = Color.Transparent
            };
        }

        private void AddQuestionCard(DataRow row, bool isReadOnly)
        {
            int    itemID      = Convert.ToInt32(row["ResponseItemID"]);
            string question    = row["QuestionText"].ToString().Trim();
            int    savedAnswer = row["NumericAnswer"] != DBNull.Value
                ? Convert.ToInt32(row["NumericAnswer"]) : 0;

            if (savedAnswer > 0) _answers[itemID] = savedAnswer;

            int cardWidth = Math.Max(600, flowQuestions.ClientSize.Width - 68);

            // Soru metninin kaç satır olacağını ölç → kart yüksekliğini dinamik ayarla
            int lblWidth = cardWidth - 44;
            int lblHeight;
            using (var g = Graphics.FromHwnd(IntPtr.Zero))
            using (var fnt = new Font("Segoe UI", 10.5f))
            {
                SizeF measured = g.MeasureString(question, fnt, lblWidth);
                lblHeight = Math.Max(40, (int)Math.Ceiling(measured.Height) + 6);
            }
            int btnY    = 16 + lblHeight + 10;
            int btnH    = 58;
            int cardH   = btnY + btnH + 20;

            var card = new Panel
            {
                Size      = new Size(cardWidth, cardH),
                BackColor = C_Card,
                Margin    = new Padding(0, 0, 0, 12),
                Padding   = new Padding(20, 16, 20, 12)
            };
            card.Paint += (s, e) => {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var pen = new Pen(C_CardBorder, 1))
                    e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
            };

            card.Controls.Add(new Label
            {
                Text      = question,
                Font      = new Font("Segoe UI", 10.5f),
                ForeColor = C_TextDark2,
                Size      = new Size(lblWidth, lblHeight),
                Location  = new Point(20, 16),
                AutoSize  = false
            });

            var btns = new Button[6];
            int startX = 20, btnW = 88, gap = 10;

            for (int score = 1; score <= 5; score++)
            {
                int s = score;
                var btn = new Button
                {
                    Size      = new Size(btnW, btnH),
                    Location  = new Point(startX, btnY),
                    FlatStyle = FlatStyle.Flat,
                    Cursor    = Cursors.Hand,
                    Tag       = itemID,
                    Name      = "btn_" + itemID + "_" + s,
                    Text      = "",   // Paint ile çizilecek
                    Font      = new Font("Segoe UI", 7.5f)
                };
                int _s = s;
                btn.Paint += (ps, pe) => {
                    pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    var numFont  = new Font("Segoe UI", 16, FontStyle.Bold);
                    var lblFont  = new Font("Segoe UI", 7f);
                    var fgBrush  = new SolidBrush(btn.ForeColor);
                    var sf       = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    // Rakam — üst %55
                    var numRect  = new RectangleF(0, 2, btn.Width, btn.Height * 0.55f);
                    // Etiket — alt %38
                    var lblRect  = new RectangleF(0, btn.Height * 0.55f, btn.Width, btn.Height * 0.38f);
                    pe.Graphics.DrawString(_s.ToString(), numFont, fgBrush, numRect, sf);
                    pe.Graphics.DrawString(ScoreLabels[_s], lblFont, fgBrush, lblRect, sf);
                    numFont.Dispose(); lblFont.Dispose(); fgBrush.Dispose(); sf.Dispose();
                };
                btn.FlatAppearance.BorderSize  = 1;
                btn.FlatAppearance.BorderColor = C_CardBorder;
                SetButtonState(btn, s, savedAnswer == s);
                if (isReadOnly) btn.Enabled = false;

                btn.Click += (sender, e) => {
                    for (int k = 1; k <= 5; k++)
                        if (btns[k] != null) SetButtonState(btns[k], k, false);
                    SetButtonState(btn, s, true);
                    _answers[itemID] = s;
                };

                btns[s] = btn;
                card.Controls.Add(btn);
                startX += btnW + gap;
            }

            flowQuestions.Controls.Add(card);
        }

        private void SetButtonState(Button btn, int score, bool selected)
        {
            if (selected)
            {
                btn.BackColor = ScoreColors[score];
                btn.ForeColor = Color.White;
                btn.FlatAppearance.BorderColor = ScoreColors[score];
                btn.Font = new Font("Segoe UI", 7.5f, FontStyle.Bold);
            }
            else
            {
                btn.BackColor = Color.FromArgb(249, 250, 252);
                btn.ForeColor = C_TextSub;
                btn.FlatAppearance.BorderColor = C_CardBorder;
                btn.Font = new Font("Segoe UI", 7.5f);
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  KAYDETME
        // ════════════════════════════════════════════════════════════════
        private void SaveData()
        {
            try
            {
                // 1) Mevcut puanları kaydet — boş soru kalsa bile kısmi ilerleme saklanır.
                foreach (var kv in _answers)
                    SqlHelper.ExecuteNonQuery(
                        "UPDATE ResponseItems SET NumericAnswer=@score WHERE ResponseItemID=@id",
                        new[] {
                            new SqlParameter("@score", kv.Value),
                            new SqlParameter("@id",    kv.Key)
                        });

                // 2) Boş (cevaplanmamış) soruları yetkinlik + soru metniyle tespit et.
                //    NumericAnswer NULL veya 0 ise o soru cevaplanmamıştır.
                DataTable bos = SqlHelper.GetDataTable(
                    @"SELECT c.CompetencyName, q.QuestionText
                      FROM ResponseItems ri
                      JOIN Questions q     ON q.QuestionID   = ri.QuestionID
                      JOIN Competencies c  ON c.CompetencyID = q.CompetencyID
                      WHERE ri.ResponseHeaderID = @hid
                        AND (ri.NumericAnswer IS NULL OR ri.NumericAnswer = 0)
                      ORDER BY c.CompetencyName, q.SortOrder",
                    new[] { new SqlParameter("@hid", _selectedHeaderID) });

                if (bos.Rows.Count > 0)
                {
                    const int gosterilecek = 12;
                    string liste = "";
                    int n = Math.Min(bos.Rows.Count, gosterilecek);
                    for (int i = 0; i < n; i++)
                    {
                        string comp  = bos.Rows[i]["CompetencyName"] == DBNull.Value ? "" : bos.Rows[i]["CompetencyName"].ToString();
                        string qtext = bos.Rows[i]["QuestionText"]   == DBNull.Value ? "" : bos.Rows[i]["QuestionText"].ToString();
                        liste += "•  [" + comp + "]  " + qtext + "\n";
                    }
                    if (bos.Rows.Count > n)
                        liste += "…  ve " + (bos.Rows.Count - n) + " soru daha.\n";

                    MessageBox.Show(
                        "Değerlendirme tamamlanamadı — " + bos.Rows.Count + " soru henüz cevaplanmadı.\n" +
                        "Verdiğiniz puanlar kaydedildi. Lütfen aşağıdaki soruları puanlayıp tekrar deneyin:\n\n" +
                        liste,
                        "Eksik Cevaplar Var", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // 3) Tüm sorular dolu → onay iste.
                var dr = MessageBox.Show(
                    "Tüm sorular cevaplandı. Değerlendirmeyi tamamlamak istiyor musunuz?\n" +
                    "Tamamladıktan sonra bu değerlendirme üzerinde değişiklik yapamazsınız.",
                    "Onay", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (dr == DialogResult.No) return;

                // 4) Değerlendirmeyi tamamla.
                SqlHelper.ExecuteNonQuery(
                    "UPDATE ResponseHeaders SET Status='Completed', SubmittedAt=GETDATE() WHERE ResponseHeaderID=@hid",
                    new[] { new SqlParameter("@hid", _selectedHeaderID) });

                MessageBox.Show("Değerlendirme başarıyla tamamlandı.",
                    "Tebrikler!", MessageBoxButtons.OK, MessageBoxIcon.Information);

                _selectedRateeCode = "";
                _selectedHeaderID  = 0;
                _answers.Clear();
                LoadPersonelList();

                pnlContentContainer.Controls.Clear();
                pnlContentContainer.Controls.Add(new Label
                {
                    Text      = "✓  Değerlendirme tamamlandı.\n\nListeden başka bir çalışanı seçebilirsiniz.",
                    Font      = new Font("Segoe UI", 14),
                    ForeColor = clrSuccess,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Dock      = DockStyle.Fill
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Hata: " + ex.Message, "Hata",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
