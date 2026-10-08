using System;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace WindowsFormsApp1
{
    public partial class PerformansAnalizDashboard : Form
    {
        // ── Renkler ──────────────────────────────────────────────────────────
        private static readonly Color C_Bg     = Color.FromArgb(244, 246, 249);
        private static readonly Color C_Navy   = Color.FromArgb(0,  43,  92);
        private static readonly Color C_Green  = Color.FromArgb(39, 174,  96);
        private static readonly Color C_Orange = Color.FromArgb(230, 126,  34);
        private static readonly Color C_White  = Color.White;
        private static readonly Color C_Border = Color.FromArgb(220, 226, 235);
        private static readonly Color C_Sub    = Color.FromArgb(100, 116, 139);

        // ── Filtreler ─────────────────────────────────────────────────────────
        private ComboBox _cboDönem;
        private ComboBox _cboBirim;
        private ComboBox _cboUnvan;

        // ── KPI değer etiketleri ──────────────────────────────────────────────
        private Label _lblKpi1, _lblKpi2, _lblKpi3, _lblKpi4;

        // ── Karne kaydırma panelleri (DataGridView yerine) ────────────────────
        private Panel _scrollBirim;
        private Panel _scrollUnvan;

        // ── İK Analitik Özet paneli etiketleri ───────────────────────────────
        private Label _lblAnalitik1;
        private Label _lblAnalitik2;
        private Label _lblAnalitik3;

        // ─────────────────────────────────────────────────────────────────────
        public PerformansAnalizDashboard()
        {
            InitializeComponent();
            this.Text            = "Performans Analiz Paneli";
            this.Size            = new Size(1280, 820);
            this.MinimumSize     = new Size(960, 640);
            this.StartPosition   = FormStartPosition.CenterScreen;
            this.BackColor       = C_Bg;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.Font            = new Font("Segoe UI", 9.5f);

            BuildUI();
            LoadFilters();
            // Dönem / Birim / Unvan dropdown'ları değişince grafikleri/verileri otomatik güncelle.
            // (LoadFilters'tan SONRA bağlanır ki ilk varsayılan seçimler erkenden tetiklemesin.)
            _cboDönem.SelectedIndexChanged += (s, e) => RefreshAll();
            _cboBirim.SelectedIndexChanged += (s, e) => RefreshAll();
            _cboUnvan.SelectedIndexChanged += (s, e) => RefreshAll();
            this.Shown += (s, e) => RefreshAll();
        }

        // ════════════════════════════════════════════════════════════════════
        //  ARAYÜZ KURULUMU
        // ════════════════════════════════════════════════════════════════════
        private void BuildUI()
        {
            // ── Header ───────────────────────────────────────────────────────
            var pnlHeader = new Panel { Dock = DockStyle.Top, Height = 62, BackColor = C_Navy };
            pnlHeader.Controls.Add(new Label
            {
                Text      = "Performans Analiz Paneli",
                Font      = new Font("Segoe UI", 15, FontStyle.Bold),
                ForeColor = C_White,
                Location  = new Point(28, 13),
                AutoSize  = true
            });
            pnlHeader.Controls.Add(new Label
            {
                Text      = "TUSAŞ · LiftUp 360 Yönetim Gösterge Paneli",
                Font      = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(155, 185, 220),
                Location  = new Point(30, 40),
                AutoSize  = true
            });
            this.Controls.Add(pnlHeader);

            // ── Filtre Çubuğu ─────────────────────────────────────────────────
            var pnlFilter = new Panel { Dock = DockStyle.Top, Height = 54, BackColor = C_White };
            pnlFilter.Paint += (s, e) => {
                using (var pen = new Pen(C_Border))
                    e.Graphics.DrawLine(pen, 0, pnlFilter.Height - 1, pnlFilter.Width, pnlFilter.Height - 1);
            };

            pnlFilter.Controls.Add(new Label { Text = "Dönem:", AutoSize = true, Location = new Point(16, 18), ForeColor = C_Sub });
            _cboDönem = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(62, 13), Size = new Size(150, 28) };
            pnlFilter.Controls.Add(_cboDönem);

            pnlFilter.Controls.Add(new Label { Text = "Birim:", AutoSize = true, Location = new Point(226, 18), ForeColor = C_Sub });
            _cboBirim = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(266, 13), Size = new Size(140, 28) };
            pnlFilter.Controls.Add(_cboBirim);

            pnlFilter.Controls.Add(new Label { Text = "Unvan:", AutoSize = true, Location = new Point(420, 18), ForeColor = C_Sub });
            _cboUnvan = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(464, 13), Size = new Size(150, 28) };
            pnlFilter.Controls.Add(_cboUnvan);

            var btnFiltrele = new Button
            {
                Text = "Filtrele", Location = new Point(630, 11), Size = new Size(84, 32),
                FlatStyle = FlatStyle.Flat, BackColor = C_Navy, ForeColor = C_White,
                Font = new Font("Segoe UI", 9, FontStyle.Bold), Cursor = Cursors.Hand
            };
            btnFiltrele.FlatAppearance.BorderSize = 0;
            btnFiltrele.Click += (s, e) => RefreshAll();
            pnlFilter.Controls.Add(btnFiltrele);

            var btnSifirla = new Button
            {
                Text = "Sıfırla", Location = new Point(722, 11), Size = new Size(76, 32),
                FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(240, 244, 250),
                ForeColor = C_Sub, Cursor = Cursors.Hand
            };
            btnSifirla.FlatAppearance.BorderSize  = 1;
            btnSifirla.FlatAppearance.BorderColor = C_Border;
            btnSifirla.Click += (s, e) =>
            {
                _cboDönem.SelectedIndex = 0;
                _cboBirim.SelectedIndex = 0;
                _cboUnvan.SelectedIndex = 0;
                RefreshAll();
            };
            pnlFilter.Controls.Add(btnSifirla);

            this.Controls.Add(pnlFilter);
            pnlFilter.BringToFront();

            // ── KPI Satırı ────────────────────────────────────────────────────
            var pnlKpiRow = new Panel { Dock = DockStyle.Top, Height = 120, BackColor = C_Bg, Padding = new Padding(24, 16, 24, 8) };

            _lblKpi1 = new Label();
            _lblKpi2 = new Label();
            _lblKpi3 = new Label();
            _lblKpi4 = new Label();

            var kpiTlp = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 4,
                RowCount    = 1,
                BackColor   = Color.Transparent
            };
            kpiTlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            kpiTlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            kpiTlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            kpiTlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));

            kpiTlp.Controls.Add(MakeKpiCard("Toplam Atama",  _lblKpi1, C_Navy),   0, 0);
            kpiTlp.Controls.Add(MakeKpiCard("Tamamlanma %",  _lblKpi2, C_Green),  1, 0);
            kpiTlp.Controls.Add(MakeKpiCard("Tamamlanan",    _lblKpi3, C_Green),  2, 0);
            kpiTlp.Controls.Add(MakeKpiCard("Bekleyen",      _lblKpi4, C_Orange), 3, 0);

            pnlKpiRow.Controls.Add(kpiTlp);
            this.Controls.Add(pnlKpiRow);
            pnlKpiRow.BringToFront();

            // ── Alt Alan: Dikey 60/40 split ──────────────────────────────────
            var pnlBody = new Panel
            {
                Dock      = DockStyle.Fill,
                BackColor = C_Bg,
                Padding   = new Padding(20, 14, 20, 20)
            };

            var outerTlp = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 1,
                RowCount    = 2,
                BackColor   = Color.Transparent
            };
            outerTlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            outerTlp.RowStyles.Add(new RowStyle(SizeType.Percent, 60f));
            outerTlp.RowStyles.Add(new RowStyle(SizeType.Percent, 40f));

            // ── Üst %60: Birim + Unvan Karneleri (yatay 50/50) ───────────────
            var splitTlp = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 2,
                RowCount    = 1,
                BackColor   = Color.Transparent,
                Margin      = new Padding(0, 0, 0, 0)
            };
            splitTlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            splitTlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            splitTlp.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            // ── Sol Kart: Birim Karnesi ───────────────────────────────────────
            var pnlLeft = BuildKarneCard(
                "Birim Bazlı Tamamlama Karnesi",
                "Birime göre ilerleme oranları · Düşükten yükseğe",
                out _scrollBirim);
            pnlLeft.Margin = new Padding(0, 0, 8, 0);
            splitTlp.Controls.Add(pnlLeft, 0, 0);

            // ── Sağ Kart: Unvan Karnesi ───────────────────────────────────────
            var pnlRight = BuildKarneCard(
                "Unvan Bazlı Tamamlama Karnesi",
                "Unvana göre ilerleme oranları · Düşükten yükseğe",
                out _scrollUnvan);
            pnlRight.Margin = new Padding(7, 0, 0, 0);
            splitTlp.Controls.Add(pnlRight, 1, 0);

            outerTlp.Controls.Add(splitTlp, 0, 0);

            // ── Alt %40: İK Analitik Özet Paneli ─────────────────────────────
            var pnlIKAnalitik = new Panel
            {
                Dock      = DockStyle.Fill,
                BackColor = C_White,
                Margin    = new Padding(0, 12, 0, 0)
            };
            pnlIKAnalitik.Paint += (s, e) =>
            {
                var p = (Panel)s;
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var pen = new Pen(C_Border))
                    e.Graphics.DrawRectangle(pen, 0, 0, p.Width - 1, p.Height - 1);
                using (var b = new SolidBrush(Color.FromArgb(250, 251, 253)))
                    e.Graphics.FillRectangle(b, 1, 1, p.Width - 2, 46);
                using (var pen2 = new Pen(C_Border))
                    e.Graphics.DrawLine(pen2, 1, 47, p.Width - 2, 47);
                using (var b2 = new SolidBrush(C_Navy))
                    e.Graphics.FillRectangle(b2, 1, 1, 4, 46);
                using (var fnt = new Font("Segoe UI", 10, FontStyle.Bold))
                using (var br = new SolidBrush(C_Navy))
                    e.Graphics.DrawString("Performans Göstergeleri", fnt, br, new PointF(18, 14));
            };

            var analitikTlp = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 3,
                RowCount    = 1,
                BackColor   = Color.Transparent,
                Padding     = new Padding(12, 58, 12, 12)
            };
            analitikTlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            analitikTlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            analitikTlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34f));
            analitikTlp.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            _lblAnalitik1 = new Label { AutoSize = false, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 11f, FontStyle.Bold), ForeColor = C_Navy,   TextAlign = ContentAlignment.MiddleLeft };
            _lblAnalitik2 = new Label { AutoSize = false, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 11f, FontStyle.Bold), ForeColor = C_Green,  TextAlign = ContentAlignment.MiddleLeft };
            _lblAnalitik3 = new Label { AutoSize = false, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 11f, FontStyle.Bold), ForeColor = C_Orange, TextAlign = ContentAlignment.MiddleLeft };

            analitikTlp.Controls.Add(MakeAnalitikCard(
                "Performans Lideri Birim",
                "Tamamlanan değerlendirmeler bazında en yüksek yetkinlik ortalamasına sahip departman.",
                _lblAnalitik1, C_Navy),   0, 0);
            analitikTlp.Controls.Add(MakeAnalitikCard(
                "Performans Lideri Unvan",
                "Tüm departmanlar genelinde yetkinlikleri en üst düzeyde sergileyen rol grubu.",
                _lblAnalitik2, C_Green),  1, 0);
            analitikTlp.Controls.Add(MakeAnalitikCard(
                "Darboğaz — En Fazla Bekleyen",
                "Sistemde atanmış ancak henüz puanlanmamış değerlendirme görevlerinin en yoğun olduğu departman.",
                _lblAnalitik3, C_Orange), 2, 0);

            pnlIKAnalitik.Controls.Add(analitikTlp);
            outerTlp.Controls.Add(pnlIKAnalitik, 0, 1);

            pnlBody.Controls.Add(outerTlp);
            this.Controls.Add(pnlBody);
            pnlBody.SendToBack();
        }

        // ════════════════════════════════════════════════════════════════════
        //  KARNE KART FACTORY (Panel-tabanlı, DataGridView yok)
        // ════════════════════════════════════════════════════════════════════
        private Panel BuildKarneCard(string title, string subtitle, out Panel scrollArea)
        {
            // Dış kart paneli
            var card = new Panel { Dock = DockStyle.Fill, BackColor = C_White };
            card.Paint += (s, e) =>
            {
                var p = (Panel)s;
                using (var pen = new Pen(C_Border))
                    e.Graphics.DrawRectangle(pen, 0, 0, p.Width - 1, p.Height - 1);
            };

            // İç düzen: 2 satır (başlık + veri alanı)
            var tlp = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 1,
                RowCount    = 2,
                BackColor   = Color.Transparent
            };
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 48f));   // başlık
            tlp.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));   // veri

            // Başlık paneli (lacivert arka plan)
            var hdr = new Panel { Dock = DockStyle.Fill, BackColor = C_Navy };
            // Sol aksent
            hdr.Paint += (s, e) =>
            {
                using (var b = new SolidBrush(Color.FromArgb(255, 200, 0)))
                    e.Graphics.FillRectangle(b, 0, 0, 4, ((Panel)s).Height);
            };
            hdr.Controls.Add(new Label
            {
                Text      = title,
                Font      = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = C_White,
                Location  = new Point(14, 8),
                AutoSize  = true
            });
            hdr.Controls.Add(new Label
            {
                Text      = subtitle,
                Font      = new Font("Segoe UI", 7.5f),
                ForeColor = Color.FromArgb(180, 200, 225),
                Location  = new Point(14, 28),
                AutoSize  = true
            });
            tlp.Controls.Add(hdr, 0, 0);

            // Kaydırılabilir veri alanı
            scrollArea = new Panel
            {
                Dock        = DockStyle.Fill,
                AutoScroll  = true,
                BackColor   = C_White
            };
            tlp.Controls.Add(scrollArea, 0, 1);

            card.Controls.Add(tlp);
            return card;
        }

        // ════════════════════════════════════════════════════════════════════
        //  KPI KART FACTORY
        // ════════════════════════════════════════════════════════════════════
        private Panel MakeKpiCard(string title, Label valueLabel, Color accentColor)
        {
            var card = new Panel { Dock = DockStyle.Fill, BackColor = C_White, Margin = new Padding(0, 0, 12, 0) };
            card.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var b = new SolidBrush(accentColor))
                    e.Graphics.FillRectangle(b, 0, 0, 5, card.Height);
                using (var pen = new Pen(C_Border))
                    e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
            };

            card.Controls.Add(new Label
            {
                Text      = title.ToUpper(),
                Font      = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                ForeColor = C_Sub,
                Location  = new Point(18, 14),
                AutoSize  = true
            });

            valueLabel.Text      = "—";
            valueLabel.Font      = new Font("Segoe UI", 24, FontStyle.Bold);
            valueLabel.ForeColor = accentColor;
            valueLabel.Location  = new Point(16, 36);
            valueLabel.AutoSize  = true;
            card.Controls.Add(valueLabel);

            return card;
        }

        // ════════════════════════════════════════════════════════════════════
        //  FACTORY: İK Analitik mini kart  (başlık + alt metin + dinamik değer)
        // ════════════════════════════════════════════════════════════════════
        private Panel MakeAnalitikCard(string heading, string subtext, Label valueLabel, Color accentColor)
        {
            var card = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(248, 250, 253), Margin = new Padding(4, 0, 4, 0) };
            card.Paint += (s, e) =>
            {
                var p = (Panel)s;
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var pen = new Pen(C_Border))
                    e.Graphics.DrawRectangle(pen, 0, 0, p.Width - 1, p.Height - 1);
                using (var b = new SolidBrush(accentColor))
                    e.Graphics.FillRectangle(b, 0, 0, 5, p.Height);
            };

            // İç TableLayoutPanel: 3 satır (başlık | alt metin | dinamik değer)
            var inner = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 1,
                RowCount    = 3,
                BackColor   = Color.Transparent,
                Padding     = new Padding(16, 10, 10, 8)
            };
            inner.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            inner.RowStyles.Add(new RowStyle(SizeType.Absolute, 28f));  // başlık
            inner.RowStyles.Add(new RowStyle(SizeType.Absolute, 52f));  // alt metin (2 satır için yeterli)
            inner.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));  // dinamik değer

            // Başlık etiketi
            var lblHeading = new Label
            {
                Text      = heading,
                Font      = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = accentColor,
                AutoSize  = false,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.BottomLeft,
                Padding   = new Padding(0)
            };
            inner.Controls.Add(lblHeading, 0, 0);

            // Alt metin etiketi (italic, gri) — AutoSize=false + Dock=Fill → metin otomatik kaydırılır
            var lblSub = new Label
            {
                Text        = subtext,
                Font        = new Font("Segoe UI", 8.5f, FontStyle.Italic),
                ForeColor   = Color.DimGray,
                AutoSize    = false,
                Dock        = DockStyle.Fill,
                TextAlign   = ContentAlignment.TopLeft,
                Padding     = new Padding(0)
            };
            inner.Controls.Add(lblSub, 0, 1);

            // Dinamik değer etiketi (dışarıdan set edilir)
            valueLabel.AutoSize  = false;
            valueLabel.Dock      = DockStyle.Fill;
            valueLabel.TextAlign = ContentAlignment.MiddleLeft;
            valueLabel.Padding   = new Padding(0);
            inner.Controls.Add(valueLabel, 0, 2);

            card.Controls.Add(inner);
            return card;
        }

        // ════════════════════════════════════════════════════════════════════
        //  KARNE SATIR FACTORY — her satır için özel boyalı Panel
        // ════════════════════════════════════════════════════════════════════
        private Panel MakeKarneRow(string rowLabel, double pct, string durum, bool alternate)
        {
            Color bgColor  = alternate ? Color.FromArgb(248, 249, 250) : C_White;
            string capLbl  = rowLabel;
            double capPct  = pct;
            string capDur  = durum;

            var row = new Panel { Height = 40, BackColor = bgColor };

            row.Paint += (s, e) =>
            {
                var p = (Panel)s;
                var g = e.Graphics;
                g.SmoothingMode       = SmoothingMode.AntiAlias;
                g.TextRenderingHint   = TextRenderingHint.ClearTypeGridFit;

                // Sütun genişlikleri (oransal)
                int nameW  = (int)(p.Width * 0.30);
                int durumW = 72;
                int barX   = nameW + 6;
                int barW   = p.Width - nameW - durumW - 20;
                if (barW < 8) barW = 8;
                int barY   = 11;
                int barH   = p.Height - 22;

                // İsim (sol)
                var nameRect = new RectangleF(10, 0, nameW - 4, p.Height);
                using (var fnt = new Font("Segoe UI", 9.5f))
                using (var br  = new SolidBrush(Color.FromArgb(30, 40, 60)))
                {
                    var sf = new StringFormat();
                    sf.LineAlignment = StringAlignment.Center;
                    sf.Trimming      = StringTrimming.EllipsisCharacter;
                    g.DrawString(capLbl, fnt, br, nameRect, sf);
                }

                // Progress bar arka planı (boş, gri)
                var barRect2 = new Rectangle(barX, barY, barW, barH);
                using (var br2 = new SolidBrush(Color.FromArgb(226, 232, 240)))
                using (var gp  = RoundedRect(barRect2, 4))
                    g.FillPath(br2, gp);

                // Dolu kısım
                if (capPct > 0)
                {
                    int fillW = (int)Math.Max(8, barW * capPct / 100.0);
                    var fillRect = new Rectangle(barRect2.X, barRect2.Y, fillW, barRect2.Height);
                    Color fc = capPct >= 100.0 ? C_Green : C_Navy;
                    using (var br3 = new SolidBrush(fc))
                    using (var gp2 = RoundedRect(fillRect, 4))
                        g.FillPath(br3, gp2);
                }

                // Yüzde metni (bar üzerinde sağda)
                string pctTxt = capPct.ToString("0") + "%";
                using (var fnt2 = new Font("Segoe UI", 8f, FontStyle.Bold))
                {
                    SizeF sz = g.MeasureString(pctTxt, fnt2);
                    float tx = barRect2.Right - sz.Width - 2;
                    float ty = barRect2.Y + (barRect2.Height - sz.Height) / 2f;
                    double fr = capPct / 100.0;
                    float fr2 = barRect2.X + (float)(barRect2.Width * fr);
                    Color tc = (tx > fr2 - 4) ? Color.FromArgb(80, 95, 115) : C_White;
                    using (var brt = new SolidBrush(tc))
                        g.DrawString(pctTxt, fnt2, brt, tx, ty);
                }

                // Durum (sağ-hizalı)
                var durRect = new RectangleF(p.Width - durumW - 4, 0, durumW, p.Height);
                using (var fnt3 = new Font("Segoe UI", 9f, FontStyle.Bold))
                using (var br4  = new SolidBrush(C_Sub))
                {
                    var sf2 = new StringFormat();
                    sf2.Alignment     = StringAlignment.Far;
                    sf2.LineAlignment = StringAlignment.Center;
                    g.DrawString(capDur, fnt3, br4, durRect, sf2);
                }

                // Alt ayırıcı çizgi
                using (var pen = new Pen(Color.FromArgb(240, 243, 247)))
                    g.DrawLine(pen, 0, p.Height - 1, p.Width, p.Height - 1);
            };

            return row;
        }

        // ── Yuvarlak köşeli dikdörtgen yardımcı metodu ───────────────────────
        private static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            var gp = new GraphicsPath();
            int d = radius * 2;
            gp.AddArc(r.X,         r.Y,          d, d, 180, 90);
            gp.AddArc(r.Right - d, r.Y,          d, d, 270, 90);
            gp.AddArc(r.Right - d, r.Bottom - d, d, d,   0, 90);
            gp.AddArc(r.X,         r.Bottom - d, d, d,  90, 90);
            gp.CloseFigure();
            return gp;
        }

        // ════════════════════════════════════════════════════════════════════
        //  KARNE DOLDURMA — DataTable → Panel satırları
        // ════════════════════════════════════════════════════════════════════
        private void FillKarneScroll(Panel scroll, DataTable dt, string groupCol)
        {
            scroll.SuspendLayout();
            scroll.Controls.Clear();
            scroll.AutoScrollPosition = new Point(0, 0);

            int y   = 0;
            bool alt = false;

            foreach (DataRow r in dt.Rows)
            {
                string lbl        = r[groupCol].ToString();
                int    toplam     = Convert.ToInt32(r["Toplam"]);
                int    tamamlanan = Convert.ToInt32(r["Tamamlanan"]);
                double pct        = toplam > 0 ? Math.Round((double)tamamlanan / toplam * 100.0, 1) : 0;
                string durum      = tamamlanan.ToString() + " / " + toplam.ToString();

                var row = MakeKarneRow(lbl, pct, durum, alt);
                // Tam genişlikte konumlandır; Anchor ile yatay boyutlanır
                row.Location = new Point(0, y);
                row.Width    = Math.Max(10, scroll.ClientSize.Width);
                row.Anchor   = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
                scroll.Controls.Add(row);

                y  += 40;
                alt = !alt;
            }

            // Dikey kaydırma için toplam yüksekliği bildir
            scroll.AutoScrollMinSize = new Size(0, y);
            scroll.ResumeLayout();
            scroll.Invalidate(true);
        }

        // ════════════════════════════════════════════════════════════════════
        //  FİLTRE YÜKLEME
        // ════════════════════════════════════════════════════════════════════
        private void LoadFilters()
        {
            _cboDönem.Items.Add("Tüm Dönemler");
            int aktifIndex = 0; // varsayılan: aktif dönem yoksa "Tüm Dönemler"
            try
            {
                var dt = SqlHelper.GetDataTable("SELECT CycleID, CycleName, IsActive FROM EvaluationCycles ORDER BY CycleID DESC");
                foreach (DataRow r in dt.Rows)
                {
                    _cboDönem.Items.Add(new CycleItem(Convert.ToInt32(r["CycleID"]), r["CycleName"].ToString()));
                    // Sayfa ilk açıldığında her zaman AKTİF dönem seçili gelsin
                    if (r["IsActive"] != DBNull.Value && Convert.ToBoolean(r["IsActive"]))
                        aktifIndex = _cboDönem.Items.Count - 1;
                }
            }
            catch { }
            _cboDönem.SelectedIndex = aktifIndex;

            _cboBirim.Items.Add("Tüm Birimler");
            try
            {
                // Birim listesi merkezi lookup tablosundan (tekrarsız, tutarlı)
                var dt = SqlHelper.GetDataTable("SELECT DepartmentName AS Birim FROM Departments WHERE DepartmentName IS NOT NULL ORDER BY DepartmentName");
                foreach (DataRow r in dt.Rows)
                    _cboBirim.Items.Add(r["Birim"].ToString());
            }
            catch { }
            _cboBirim.SelectedIndex = 0;

            _cboUnvan.Items.Add("Tüm Unvanlar");
            try
            {
                // Unvan listesi merkezi lookup tablosundan (tekrarsız, tutarlı)
                var dt = SqlHelper.GetDataTable("SELECT UnvanAdi AS Unvan FROM Titles WHERE UnvanAdi IS NOT NULL ORDER BY UnvanAdi");
                foreach (DataRow r in dt.Rows)
                    _cboUnvan.Items.Add(r["Unvan"].ToString());
            }
            catch { }
            _cboUnvan.SelectedIndex = 0;
        }

        // ── Filtre parametrelerini oluştur ────────────────────────────────────
        private FilterResult BuildFilters()
        {
            string cw = "", cwRA = "", bw = "", uw = "";
            var pl = new System.Collections.Generic.List<SqlParameter>();

            var ci = _cboDönem.SelectedItem as CycleItem;
            if (ci != null)
            {
                cw   = " AND RH.CycleID = @cycleID";
                cwRA = " AND RA.CycleID = @cycleID";
                pl.Add(new SqlParameter("@cycleID", ci.CycleID));
            }

            if (_cboBirim.SelectedIndex > 0 && _cboBirim.SelectedItem != null)
            { bw = " AND E.Birim = @birim"; pl.Add(new SqlParameter("@birim", _cboBirim.SelectedItem.ToString())); }

            if (_cboUnvan.SelectedIndex > 0 && _cboUnvan.SelectedItem != null)
            { uw = " AND E.Unvan = @unvan"; pl.Add(new SqlParameter("@unvan", _cboUnvan.SelectedItem.ToString())); }

            return new FilterResult { CycleWhere = cw, CycleWhereRA = cwRA, BirimWhere = bw, UnvanWhere = uw, Parms = pl.ToArray() };
        }

        private class FilterResult
        {
            public string CycleWhere;
            public string CycleWhereRA;
            public string BirimWhere;
            public string UnvanWhere;
            public SqlParameter[] Parms;
        }

        // Aynı parametre dizisini birden çok SqlCommand'da kullanmak hata verir; her çağrı için klonla.
        private static SqlParameter[] CloneParms(SqlParameter[] src)
        {
            if (src == null) return null;
            var arr = new SqlParameter[src.Length];
            for (int i = 0; i < src.Length; i++)
                arr[i] = new SqlParameter(src[i].ParameterName, src[i].Value);
            return arr;
        }

        // ════════════════════════════════════════════════════════════════════
        //  VERİ YENİLEME
        // ════════════════════════════════════════════════════════════════════
        private void RefreshAll()
        {
            try { LoadKPIs(); }
            catch (Exception ex) { MessageBox.Show("KPI hatası: " + ex.Message); }

            try { LoadBirimKarnesi(); }
            catch (Exception ex)
            {
                _scrollBirim.Controls.Clear();
                _scrollBirim.Controls.Add(new Label
                {
                    Text      = "HATA: " + ex.Message,
                    ForeColor = Color.Red,
                    Location  = new Point(8, 8),
                    AutoSize  = true
                });
            }

            try { LoadUnvanKarnesi(); }
            catch (Exception ex)
            {
                _scrollUnvan.Controls.Clear();
                _scrollUnvan.Controls.Add(new Label
                {
                    Text      = "HATA: " + ex.Message,
                    ForeColor = Color.Red,
                    Location  = new Point(8, 8),
                    AutoSize  = true
                });
            }

            try { LoadIKAnalitik(); }
            catch (Exception ex) { _lblAnalitik1.Text = "HATA: " + ex.Message; }
        }

        // ── KPI ──────────────────────────────────────────────────────────────
        private void LoadKPIs()
        {
            var f = BuildFilters();
            string sql =
                " SELECT" +
                "   COUNT(*)                                                                    AS Toplam," +
                "   SUM(CASE WHEN RH.Status='Completed'        THEN 1 ELSE 0 END)              AS Tamamlanan," +
                "   SUM(CASE WHEN ISNULL(RH.Status,'') <> 'Completed' THEN 1 ELSE 0 END)       AS Bekleyen" +
                " FROM ResponseHeaders RH" +
                " LEFT JOIN Employees_Tablo E ON RH.RateePersonelCode = E.PersonelCode" +
                " WHERE 1=1" + f.CycleWhere + f.BirimWhere + f.UnvanWhere;

            var dt = SqlHelper.GetDataTable(sql, f.Parms);
            if (dt.Rows.Count == 0) return;

            var row        = dt.Rows[0];
            int toplam     = Convert.ToInt32(row["Toplam"]);
            int tamamlanan = Convert.ToInt32(row["Tamamlanan"]);
            int bekleyen   = Convert.ToInt32(row["Bekleyen"]);
            double pct     = toplam > 0 ? Math.Round((double)tamamlanan / toplam * 100, 1) : 0;

            _lblKpi1.Text = toplam.ToString("N0");
            _lblKpi2.Text = "%" + pct.ToString("0.0");
            _lblKpi3.Text = tamamlanan.ToString("N0");
            _lblKpi4.Text = bekleyen.ToString("N0");
        }

        // ── Birim Karnesi ─────────────────────────────────────────────────────
        private void LoadBirimKarnesi()
        {
            var f = BuildFilters();
            string sql =
                // Tüm birimler DAİMA listelenir (ana tablodan, bağımsız tam liste)
                " WITH AllBirims AS (" +
                "   SELECT DepartmentName AS Birim FROM Departments WHERE DepartmentName IS NOT NULL" +
                " )," +
                " Compl AS (" +
                "   SELECT ISNULL(E.Birim,'Belirtilmemiş') AS Birim," +
                "          COUNT(*) AS Toplam," +
                "          SUM(CASE WHEN RH.Status='Completed' THEN 1 ELSE 0 END) AS Tamamlanan" +
                "   FROM RaterAssignments RA" +
                "   JOIN Employees_Tablo E ON E.PersonelCode = RA.RateePersonelCode" +
                "   LEFT JOIN ResponseHeaders RH" +
                "     ON RH.RateePersonelCode = RA.RateePersonelCode" +
                "    AND RH.RaterPersonelCode = RA.RaterPersonelCode" +
                "    AND RH.CycleID           = RA.CycleID" +
                "   WHERE 1=1" + f.CycleWhereRA + f.BirimWhere + f.UnvanWhere +
                "   GROUP BY ISNULL(E.Birim,'Belirtilmemiş')" +
                " )" +
                " SELECT B.Birim," +
                "        ISNULL(C.Toplam,0)     AS Toplam," +
                "        ISNULL(C.Tamamlanan,0) AS Tamamlanan" +
                " FROM AllBirims B" +
                " LEFT JOIN Compl C ON LTRIM(RTRIM(UPPER(C.Birim))) = LTRIM(RTRIM(UPPER(B.Birim)))" +
                " ORDER BY CAST(ISNULL(C.Tamamlanan,0) AS FLOAT)" +
                "        / NULLIF(ISNULL(C.Toplam,0),0) ASC, B.Birim ASC";

            FillKarneScroll(_scrollBirim, SqlHelper.GetDataTable(sql, f.Parms), "Birim");
        }

        // ── Unvan Karnesi ─────────────────────────────────────────────────────
        private void LoadUnvanKarnesi()
        {
            var f = BuildFilters();
            string sql =
                // Tüm unvanlar DAİMA listelenir (ana tablodan, bağımsız tam liste)
                " WITH AllUnvanlar AS (" +
                "   SELECT UnvanAdi AS Unvan FROM Titles WHERE UnvanAdi IS NOT NULL" +
                " )," +
                " Compl AS (" +
                "   SELECT ISNULL(E.Unvan,'Belirtilmemiş') AS Unvan," +
                "          COUNT(*) AS Toplam," +
                "          SUM(CASE WHEN RH.Status='Completed' THEN 1 ELSE 0 END) AS Tamamlanan" +
                "   FROM RaterAssignments RA" +
                "   JOIN Employees_Tablo E ON E.PersonelCode = RA.RateePersonelCode" +
                "   LEFT JOIN ResponseHeaders RH" +
                "     ON RH.RateePersonelCode = RA.RateePersonelCode" +
                "    AND RH.RaterPersonelCode = RA.RaterPersonelCode" +
                "    AND RH.CycleID           = RA.CycleID" +
                "   WHERE 1=1" + f.CycleWhereRA + f.BirimWhere + f.UnvanWhere +
                "   GROUP BY ISNULL(E.Unvan,'Belirtilmemiş')" +
                " )" +
                " SELECT U.Unvan," +
                "        ISNULL(C.Toplam,0)     AS Toplam," +
                "        ISNULL(C.Tamamlanan,0) AS Tamamlanan" +
                " FROM AllUnvanlar U" +
                " LEFT JOIN Compl C ON LTRIM(RTRIM(UPPER(C.Unvan))) = LTRIM(RTRIM(UPPER(U.Unvan)))" +
                " ORDER BY CAST(ISNULL(C.Tamamlanan,0) AS FLOAT)" +
                "        / NULLIF(ISNULL(C.Toplam,0),0) ASC, U.Unvan ASC";

            FillKarneScroll(_scrollUnvan, SqlHelper.GetDataTable(sql, f.Parms), "Unvan");
        }

        // ── İK Analitik Özet ──────────────────────────────────────────────────
        private void LoadIKAnalitik()
        {
            var f = BuildFilters();

            string sqlBestBirim =
                " SELECT TOP 1 ISNULL(E.Birim,'Belirtilmemiş') AS Grup," +
                "   CAST(AVG(CAST(RI.NumericAnswer AS FLOAT)) AS DECIMAL(5,2)) AS OrtPuan" +
                " FROM ResponseItems RI" +
                " JOIN ResponseHeaders RH ON RI.ResponseHeaderID = RH.ResponseHeaderID" +
                " LEFT JOIN Employees_Tablo E ON RH.RateePersonelCode = E.PersonelCode" +
                " WHERE RH.Status = 'Completed' AND RI.NumericAnswer > 0" +
                f.CycleWhere + f.BirimWhere + f.UnvanWhere +
                " GROUP BY ISNULL(E.Birim,'Belirtilmemiş')" +
                " HAVING COUNT(*) > 0" +
                " ORDER BY OrtPuan DESC";

            string sqlBestUnvan =
                " SELECT TOP 1 ISNULL(E.Unvan,'Belirtilmemiş') AS Grup," +
                "   CAST(AVG(CAST(RI.NumericAnswer AS FLOAT)) AS DECIMAL(5,2)) AS OrtPuan" +
                " FROM ResponseItems RI" +
                " JOIN ResponseHeaders RH ON RI.ResponseHeaderID = RH.ResponseHeaderID" +
                " LEFT JOIN Employees_Tablo E ON RH.RateePersonelCode = E.PersonelCode" +
                " WHERE RH.Status = 'Completed' AND RI.NumericAnswer > 0" +
                f.CycleWhere + f.BirimWhere + f.UnvanWhere +
                " GROUP BY ISNULL(E.Unvan,'Belirtilmemiş')" +
                " HAVING COUNT(*) > 0" +
                " ORDER BY OrtPuan DESC";

            string sqlBottleneck =
                " SELECT TOP 1 ISNULL(E.Birim,'Belirtilmemiş') AS Grup," +
                "   SUM(CASE WHEN ISNULL(RH.Status,'') <> 'Completed' THEN 1 ELSE 0 END) AS Bekleyen" +
                " FROM ResponseHeaders RH" +
                " LEFT JOIN Employees_Tablo E ON RH.RateePersonelCode = E.PersonelCode" +
                " WHERE 1=1" + f.CycleWhere + f.BirimWhere + f.UnvanWhere +
                " GROUP BY ISNULL(E.Birim,'Belirtilmemiş')" +
                " HAVING COUNT(*) > 0" +
                " ORDER BY Bekleyen DESC";

            try
            {
                // Her sorgu için parametre KLONU kullan — aynı SqlParameter nesneleri
                // birden çok komutta yeniden kullanılamaz (aksi halde exception -> "Veri alınamadı").
                var dtBirim      = SqlHelper.GetDataTable(sqlBestBirim,  CloneParms(f.Parms));
                var dtUnvan      = SqlHelper.GetDataTable(sqlBestUnvan,  CloneParms(f.Parms));
                var dtBottleneck = SqlHelper.GetDataTable(sqlBottleneck, CloneParms(f.Parms));

                if (dtBirim.Rows.Count > 0)
                {
                    string grup = dtBirim.Rows[0]["Grup"].ToString();
                    string puan = dtBirim.Rows[0]["OrtPuan"] == DBNull.Value
                        ? "—"
                        : Convert.ToDecimal(dtBirim.Rows[0]["OrtPuan"]).ToString("0.00");
                    _lblAnalitik1.Text = grup + "\n" + puan + " / 5 ortalama skor";
                }
                else { _lblAnalitik1.Text = "Tamamlanmış değerlendirme yok"; }

                if (dtUnvan.Rows.Count > 0)
                {
                    string grup = dtUnvan.Rows[0]["Grup"].ToString();
                    string puan = dtUnvan.Rows[0]["OrtPuan"] == DBNull.Value
                        ? "—"
                        : Convert.ToDecimal(dtUnvan.Rows[0]["OrtPuan"]).ToString("0.00");
                    _lblAnalitik2.Text = grup + "\n" + puan + " / 5 ortalama skor";
                }
                else { _lblAnalitik2.Text = "Tamamlanmış değerlendirme yok"; }

                if (dtBottleneck.Rows.Count > 0)
                {
                    int bekleyen = 0;
                    int.TryParse(dtBottleneck.Rows[0]["Bekleyen"].ToString(), out bekleyen);
                    if (bekleyen <= 0)
                    {
                        // Bu dönemde bekleyen görev yok (örn. eski dönem tamamen tamamlanmış)
                        _lblAnalitik3.Text = "Tüm değerlendirmeler tamamlanmıştır";
                    }
                    else
                    {
                        string grup = dtBottleneck.Rows[0]["Grup"].ToString();
                        _lblAnalitik3.Text = grup + "\n" + bekleyen + " adet görev aksiyon bekliyor";
                    }
                }
                else { _lblAnalitik3.Text = "Veri bulunamadı"; }
            }
            catch
            {
                _lblAnalitik1.Text = "Veri alınamadı";
                _lblAnalitik2.Text = "Veri alınamadı";
                _lblAnalitik3.Text = "Veri alınamadı";
            }
        }

        // ── Yardımcı sınıflar ─────────────────────────────────────────────────
        private class CycleItem
        {
            public int    CycleID   { get; private set; }
            public string CycleName { get; private set; }
            public CycleItem(int id, string name) { CycleID = id; CycleName = name; }
            public override string ToString() { return CycleName; }
        }
    }
}
