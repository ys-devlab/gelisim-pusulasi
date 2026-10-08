using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using WindowsFormsApp1.AI;
using WindowsFormsApp1.AI.Core;

namespace WindowsFormsApp1
{
    public partial class MyReportsForm : Form
    {
        // ── API ───────────────────────────────────────────────────────────────
        // Anahtar artık ai.env'den okunur (AiGateway).
        private static string ApiKey => AiConfig.Get("OPENROUTER_API_KEY", "");

        // ── Kullanıcı ─────────────────────────────────────────────────────────
        private readonly string _userCode;
        private readonly string _fullName;
        private readonly string _title;
        private readonly string _department;

        // ── Renkler ───────────────────────────────────────────────────────────
        private readonly Color C_Dark   = Color.FromArgb(15, 23, 42);
        private readonly Color C_Card   = Color.White;
        private readonly Color C_Bg     = Color.FromArgb(241, 245, 249);
        private readonly Color C_Text   = Color.FromArgb(15, 23, 42);
        private readonly Color C_Sub    = Color.FromArgb(100, 116, 139);
        private readonly Color C_Border = Color.FromArgb(226, 232, 240);
        private readonly Color C_Accent = Color.FromArgb(0, 43, 92);
        private readonly Color C_Green  = Color.FromArgb(22, 163, 74);
        private readonly Color C_Active = Color.FromArgb(16, 185, 129);

        // ── Dönem verisi ──────────────────────────────────────────────────────
        private class PeriodInfo
        {
            public int       CycleId   { get; set; }
            public string    CycleName { get; set; }
            public bool      IsActive  { get; set; }
            public double    Score     { get; set; } = -1;
            public DataTable Data      { get; set; }   // yetkinlik skorları (AI + radar için)
        }

        private List<PeriodInfo> _periods = new List<PeriodInfo>();

        // ── UI ────────────────────────────────────────────────────────────────
        private Panel      _pnlReportList;  // dönem kartları
        private RichTextBox _rtbAI;
        private Button     _btnAnalyze;
        private Label      _lblAIStatus;
        private TableLayoutPanel _tlpBody;   // üst: dönem listesi (sınırlı+scroll), alt: AI kart (esner)

        // ─────────────────────────────────────────────────────────────────────
        public MyReportsForm(string userCode, string fullName, string title, string department)
        {
            InitializeComponent();
            _userCode   = userCode;
            _fullName   = fullName;
            _title      = title;
            _department = department;

            this.Text            = "Raporlarım";
            this.Size            = new Size(860, 760);
            this.MinimumSize     = new Size(700, 580);
            this.StartPosition   = FormStartPosition.CenterScreen;
            this.BackColor       = C_Bg;
            this.FormBorderStyle = FormBorderStyle.Sizable;

            BuildUI();
            this.Shown += (s, e) => LoadPeriods();
        }

        // ════════════════════════════════════════════════════════════════════
        //  ARAYÜZ OLUŞTURMA
        // ════════════════════════════════════════════════════════════════════
        private void BuildUI()
        {
            // ── Üst başlık çubuğu ─────────────────────────────────────────
            var pnlHeader = new Panel { Dock = DockStyle.Top, Height = 68, BackColor = C_Dark };

            var pnlAvatar = new Panel { Size = new Size(40, 40), Location = new Point(20, 14), BackColor = C_Accent };
            pnlAvatar.Paint += (s, e) => {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var path = new GraphicsPath())
                {
                    path.AddEllipse(0, 0, 39, 39);
                    pnlAvatar.Region = new Region(path);
                }
                string letter = _fullName.Length > 0 ? _fullName[0].ToString().ToUpper() : "?";
                using (var f = new Font("Segoe UI", 16, FontStyle.Bold))
                using (var b = new SolidBrush(Color.White))
                {
                    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    e.Graphics.DrawString(letter, f, b, new RectangleF(0, 0, 40, 40), sf);
                }
            };

            pnlHeader.Controls.Add(pnlAvatar);
            pnlHeader.Controls.Add(new Label
            {
                Text      = _fullName,
                Font      = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = Color.White,
                Location  = new Point(70, 12),
                AutoSize  = true
            });
            pnlHeader.Controls.Add(new Label
            {
                Text      = (string.IsNullOrWhiteSpace(_title) ? "" : _title + "  ·  ") + "Raporlarım",
                Font      = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(148, 163, 184),
                Location  = new Point(70, 36),
                AutoSize  = true
            });

            var btnKapat = new Button
            {
                Text      = "← Geri Dön",
                Size      = new Size(110, 32),
                Anchor    = AnchorStyles.Top | AnchorStyles.Right,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(30, 41, 59),
                ForeColor = Color.FromArgb(148, 163, 184),
                Font      = new Font("Segoe UI", 9, FontStyle.Bold),
                Cursor    = Cursors.Hand
            };
            btnKapat.FlatAppearance.BorderSize = 0;
            btnKapat.Location = new Point(pnlHeader.Width - 130, 18);
            btnKapat.Anchor   = AnchorStyles.Top | AnchorStyles.Right;
            btnKapat.Click   += (s, e) => this.Close();
            pnlHeader.Controls.Add(btnKapat);

            this.Controls.Add(pnlHeader);

            // ── Gövde: dikey yerleşim (flex column) ───────────────────────
            //   Satır 0: dönem kartları — içerik kadar, max ~%42, taşarsa kaydırılır
            //   Satır 1: AI analizi — kalan tüm alanı kaplar (flex-grow)
            _tlpBody = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 1,
                RowCount    = 2,
                BackColor   = C_Bg,
                Padding     = new Padding(24, 18, 24, 22)
            };
            _tlpBody.RowStyles.Add(new RowStyle(SizeType.Absolute, 200f)); // RebuildList'te içeriğe göre güncellenir
            _tlpBody.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            // Üst: dönem kartları
            _pnlReportList = new Panel
            {
                Dock       = DockStyle.Fill,
                BackColor  = C_Bg,
                AutoScroll = true,
                Padding    = new Padding(2, 0, 2, 4)
            };

            // Alt: AI analizi — şık beyaz KART (kenarlık + hafif gölge + iç boşluk)
            var pnlAI = new Panel
            {
                Dock      = DockStyle.Fill,
                BackColor = Color.White,
                Margin    = new Padding(0, 16, 0, 0),     // üst alanla arada dengeli boşluk
                Padding   = new Padding(28, 16, 28, 22)
            };
            pnlAI.Paint += (s, e) =>
            {
                var r = pnlAI.ClientRectangle;
                using (var sh = new Pen(Color.FromArgb(20, 0, 0, 0), 1))
                    e.Graphics.DrawRectangle(sh, 2, 3, r.Width - 4, r.Height - 5);   // yumuşak gölge
                using (var pen = new Pen(C_Border, 1))
                    e.Graphics.DrawRectangle(pen, 0, 0, r.Width - 1, r.Height - 1);  // kart kenarlığı
            };

            // AI başlık satırı (kart içinde)
            var pnlAIHead = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Color.White };
            var lblAITitle = new Label
            {
                Text      = "Yapay Zeka Gelişim Analizi",
                Font      = new Font("Segoe UI", 12.5f, FontStyle.Bold),
                ForeColor = C_Text,
                Location  = new Point(0, 6),
                AutoSize  = true
            };
            _btnAnalyze = new Button
            {
                Text      = "Analiz Et",
                Size      = new Size(130, 34),
                Anchor    = AnchorStyles.Top | AnchorStyles.Right,
                FlatStyle = FlatStyle.Flat,
                BackColor = C_Accent,
                ForeColor = Color.White,
                Font      = new Font("Segoe UI", 9, FontStyle.Bold),
                Cursor    = Cursors.Hand
            };
            _btnAnalyze.FlatAppearance.BorderSize = 0;
            _btnAnalyze.Location = new Point(pnlAIHead.Width - 134, 4);
            _btnAnalyze.Click   += BtnAnalyze_Click;
            pnlAIHead.Controls.Add(lblAITitle);
            pnlAIHead.Controls.Add(_btnAnalyze);

            _lblAIStatus = new Label
            {
                Text      = "Dönemlerinizi yapay zeka ile analiz etmek için \"Analiz Et\" butonuna tıklayın.",
                Font      = new Font("Segoe UI", 8.5f),
                ForeColor = C_Sub,
                Dock      = DockStyle.Top,
                Height    = 24
            };

            _rtbAI = new RichTextBox
            {
                Dock        = DockStyle.Fill,
                ReadOnly    = true,
                BackColor   = Color.White,
                Font        = new Font("Segoe UI", 11f),
                BorderStyle = BorderStyle.None,
                ForeColor   = C_Text,
                ScrollBars  = RichTextBoxScrollBars.Vertical
            };

            pnlAI.Controls.Add(_rtbAI);       // Fill — kart iç boşluğu (Padding) metni kenardan ayırır
            pnlAI.Controls.Add(_lblAIStatus); // Top
            pnlAI.Controls.Add(pnlAIHead);    // Top

            _tlpBody.Controls.Add(_pnlReportList, 0, 0);
            _tlpBody.Controls.Add(pnlAI,          0, 1);

            this.Controls.Add(_tlpBody);   // gövde tüm alanı doldurur (header'ın altında)
        }

        // ════════════════════════════════════════════════════════════════════
        //  DÖNEM VERİSİ YÜKLEME
        // ════════════════════════════════════════════════════════════════════
        private void LoadPeriods()
        {
            _periods.Clear();

            // Dönemler EvaluationCycles tablosundan gelir (aktif önce, sonra eskiler).
            try
            {
                var dt = SqlHelper.GetDataTable(
                    "SELECT CycleID, CycleName, IsActive FROM EvaluationCycles ORDER BY IsActive DESC, CycleID DESC");
                foreach (DataRow r in dt.Rows)
                    _periods.Add(new PeriodInfo
                    {
                        CycleId   = Convert.ToInt32(r["CycleID"]),
                        CycleName = r["CycleName"] == DBNull.Value ? "" : r["CycleName"].ToString(),
                        IsActive  = r["IsActive"] != DBNull.Value && Convert.ToBoolean(r["IsActive"])
                    });
            }
            catch { }

            // Listeleri çiz
            RebuildList();

            // Arka planda skorları yükle (liste kartlarını günceller)
            foreach (var p in _periods.ToList())
            {
                var period = p;
                Task.Run(() => {
                    LoadPeriodData(period);
                    if (this.IsHandleCreated)
                        this.BeginInvoke(new Action(RebuildList));
                });
            }
        }

        private void LoadPeriodData(PeriodInfo period)
        {
            if (period.Data != null) return;

            string yf = $" AND RH.CycleID = {period.CycleId}";

            string sql = @"
WITH BA AS (
    SELECT C.CompetencyName, RT.RaterTypeCode, E.Yaka,
           AVG(CAST(RI.NumericAnswer AS FLOAT)) AS GrpAvg
    FROM ResponseItems RI
    JOIN ResponseHeaders RH ON RI.ResponseHeaderID = RH.ResponseHeaderID
    JOIN Employees_Tablo E  ON RH.RateePersonelCode = E.PersonelCode
    JOIN Questions Q         ON RI.QuestionID = Q.QuestionID
    JOIN Competencies C      ON Q.CompetencyID = C.CompetencyID
    JOIN RaterTypes RT       ON RH.RaterTypeID = RT.RaterTypeID
    WHERE RH.RateePersonelCode=@code AND RH.Status='Completed' AND RI.NumericAnswer>0" + yf + @"
    GROUP BY C.CompetencyName, RT.RaterTypeCode, E.Yaka
),
RF AS (
    SELECT CASE WHEN COUNT(*)>0 THEN 1 ELSE 0 END AS HasSub
    FROM RaterAssignments RA JOIN RaterTypes RT ON RA.RaterTypeID=RT.RaterTypeID
    WHERE RA.RateePersonelCode=@code AND RT.RaterTypeCode='Subordinate'
),
WL AS (
    SELECT B.*,
        CASE
            WHEN B.Yaka='Mavi'  AND B.RaterTypeCode LIKE 'Manager%' THEN 50.0
            WHEN B.Yaka='Beyaz' AND B.RaterTypeCode LIKE 'Manager%'
                THEN CASE WHEN (SELECT HasSub FROM RF)=1 THEN 25.0 ELSE 50.0 END
            WHEN B.RaterTypeCode IN ('Subordinate','Peer','JointWorker') THEN 25.0
            ELSE 0.0
        END AS Weight
    FROM BA B
)
SELECT CompetencyName,
    CAST(ISNULL(MAX(CASE WHEN RaterTypeCode='Self'     THEN GrpAvg END),0) AS DECIMAL(5,2)) AS SelfScore,
    CAST(ISNULL(MAX(CASE WHEN RaterTypeCode='Manager1' THEN GrpAvg END),0) AS DECIMAL(5,2)) AS ManagerScore,
    CAST(ISNULL(SUM(CASE WHEN RaterTypeCode<>'Self' THEN GrpAvg*Weight END)
          /NULLIF(SUM(CASE WHEN RaterTypeCode<>'Self' THEN Weight END),0),0) AS DECIMAL(5,2)) AS WeightedScore
FROM WL GROUP BY CompetencyName ORDER BY WeightedScore DESC;";

            try
            {
                var ps = new[] { new SqlParameter("@code", _userCode) };
                var dt = SqlHelper.GetDataTable(sql, ps);
                period.Data  = dt;
                period.Score = dt.Rows.Count > 0
                    ? dt.AsEnumerable().Average(r => ToDouble(r["WeightedScore"]))
                    : -1;
            }
            catch { period.Score = -1; }
        }

        // ════════════════════════════════════════════════════════════════════
        //  LİSTE ÇİZİMİ
        // ════════════════════════════════════════════════════════════════════
        private void RebuildList()
        {
            if (!this.IsHandleCreated) return;
            if (InvokeRequired) { this.BeginInvoke(new Action(RebuildList)); return; }

            int w = _pnlReportList.ClientSize.Width - 8;
            if (w < 200) return;

            _pnlReportList.SuspendLayout();
            _pnlReportList.Controls.Clear();

            // Başlık
            var lblHead = new Label
            {
                Text      = "Performans Raporlarım",
                Font      = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = C_Text,
                Location  = new Point(0, 0),
                AutoSize  = true
            };
            _pnlReportList.Controls.Add(lblHead);

            var lblSub = new Label
            {
                Text      = "Döneme ait raporu görüntülemek için \"Raporu Aç\" butonuna tıklayın.",
                Font      = new Font("Segoe UI", 9),
                ForeColor = C_Sub,
                Location  = new Point(0, 36),
                AutoSize  = true
            };
            _pnlReportList.Controls.Add(lblSub);

            // Dönem kartları
            int y = 76;
            foreach (var period in _periods)
            {
                var card = MakePeriodCard(period, w);
                card.Location = new Point(0, y);
                _pnlReportList.Controls.Add(card);
                y += card.Height + 14;
            }

            if (_periods.Count == 0)
            {
                var lblEmpty = new Label
                {
                    Text      = "Henüz tamamlanmış bir değerlendirme döneminiz bulunmamaktadır.",
                    Font      = new Font("Segoe UI", 11),
                    ForeColor = C_Sub,
                    Location  = new Point(0, 90),
                    AutoSize  = true
                };
                _pnlReportList.Controls.Add(lblEmpty);
            }

            // Üst alan içeriği kadar yer kaplasın; ama formun ~%42'sini geçmesin (taşarsa scroll).
            if (_tlpBody != null && _tlpBody.RowStyles.Count > 0)
            {
                int contentH = y + 12;
                int cap      = Math.Max(150, (int)(this.ClientSize.Height * 0.42));
                int rowH     = Math.Min(contentH, cap);
                _tlpBody.RowStyles[0] = new RowStyle(SizeType.Absolute, rowH);
            }

            _pnlReportList.ResumeLayout();
        }

        private Panel MakePeriodCard(PeriodInfo period, int cardWidth)
        {
            var card = new Panel
            {
                Size      = new Size(cardWidth, 90),
                BackColor = C_Card,
                Cursor    = Cursors.Default
            };

            // Sol renkli şerit
            card.Controls.Add(new Panel
            {
                Dock      = DockStyle.Left,
                Width     = 5,
                BackColor = Color.FromArgb(0, 43, 92)
            });

            // Dönem adı (1. satır)
            card.Controls.Add(new Label
            {
                Text      = period.CycleName,
                Font      = new Font("Segoe UI", 13.5f, FontStyle.Bold),
                ForeColor = C_Text,
                Location  = new Point(20, 12),
                AutoSize  = true
            });

            // Durum rozeti
            // Rozet (3. satır)
            string rozetText = period.IsActive ? "● AKTİF DÖNEM" : "✓ TAMAMLANDI";
            Color  rozetRenk = period.IsActive ? C_Active : C_Sub;
            card.Controls.Add(new Label
            {
                Text      = rozetText,
                Font      = new Font("Segoe UI", 8, FontStyle.Bold),
                ForeColor = rozetRenk,
                Location  = new Point(22, 64),
                AutoSize  = true
            });

            // Skor
            string skorText = period.Score >= 0
                ? "Genel Puan:  " + period.Score.ToString("0.00") + " / 5"
                : "Değerlendirme sonucu henüz oluşmadı.";
            Color skorRenk = period.Score >= 4.0 ? C_Green
                           : period.Score >= 3.0 ? C_Accent
                           : period.Score >= 0   ? Color.FromArgb(234, 88, 12)
                           : C_Sub;

            var lblSkor = new Label
            {
                Text      = skorText,
                Font      = new Font("Segoe UI", 11, period.Score >= 0 ? FontStyle.Bold : FontStyle.Regular),
                ForeColor = skorRenk,
                AutoSize  = true,
                Anchor    = AnchorStyles.Top | AnchorStyles.Left
            };
            lblSkor.Location = new Point(22, 38);
            card.Controls.Add(lblSkor);

            // "Raporu Aç" butonu
            var btnAc = new Button
            {
                Text      = "Raporu Aç",
                Size      = new Size(140, 38),
                Anchor    = AnchorStyles.Top | AnchorStyles.Right,
                FlatStyle = FlatStyle.Flat,
                BackColor = period.Score >= 0 ? Color.FromArgb(0, 43, 92) : Color.FromArgb(180, 190, 200),
                ForeColor = Color.White,
                Font      = new Font("Segoe UI", 9, FontStyle.Bold),
                Cursor    = period.Score >= 0 ? Cursors.Hand : Cursors.Default,
                Enabled   = period.Score >= 0
            };
            btnAc.FlatAppearance.BorderSize = 0;
            btnAc.Location = new Point(cardWidth - 158, 26);
            btnAc.Anchor   = AnchorStyles.Top | AnchorStyles.Right;
            btnAc.Click   += (s, e) => AcRapor(period, btnAc);
            card.Controls.Add(btnAc);

            // Kart kenarlığı
            card.Paint += (s, e) => {
                using (var pen = new Pen(C_Border, 1))
                    e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
            };

            return card;
        }

        // ════════════════════════════════════════════════════════════════════
        //  RAPORU AÇ — PDF direkt oluşturulur ve açılır
        // ════════════════════════════════════════════════════════════════════
        private async void AcRapor(PeriodInfo period, Button btn)
        {
            btn.Enabled = false;
            btn.Text    = "⏳ Hazırlanıyor...";
            try
            {
                await Task.Run(() => { }); // UI'ı serbest bırak

                // ── 1. 4 SQL sorgusunu çalıştır ──────────────────────────
                string yf = $" AND RH.CycleID = {period.CycleId}";

                string sql = $@"
-- 1. ANA YETKİNLİKLER
WITH BaseAverages AS (
    SELECT C.CompetencyName, RT.RaterTypeCode, E.Yaka,
           AVG(CAST(RI.NumericAnswer AS FLOAT)) AS GrpAvg
    FROM ResponseItems RI
    JOIN ResponseHeaders RH ON RI.ResponseHeaderID = RH.ResponseHeaderID
    JOIN Employees_Tablo E  ON RH.RateePersonelCode = E.PersonelCode
    JOIN Questions Q         ON RI.QuestionID = Q.QuestionID
    JOIN Competencies C      ON Q.CompetencyID = C.CompetencyID
    JOIN RaterTypes RT       ON RH.RaterTypeID = RT.RaterTypeID
    WHERE RH.RateePersonelCode=@code AND RH.Status='Completed' AND RI.NumericAnswer>0{yf}
    GROUP BY C.CompetencyName, RT.RaterTypeCode, E.Yaka
),
RateeFlags AS (
    SELECT CASE WHEN COUNT(*)>0 THEN 1 ELSE 0 END AS HasSub
    FROM RaterAssignments RA JOIN RaterTypes RT ON RA.RaterTypeID=RT.RaterTypeID
    WHERE RA.RateePersonelCode=@code AND RT.RaterTypeCode='Subordinate'
),
WeightLogic AS (
    SELECT B.*,
        CASE
            WHEN B.Yaka='Mavi'  AND B.RaterTypeCode LIKE 'Manager%' THEN 50.0
            WHEN B.Yaka='Beyaz' AND B.RaterTypeCode LIKE 'Manager%'
                THEN CASE WHEN (SELECT HasSub FROM RateeFlags)=1 THEN 25.0 ELSE 50.0 END
            WHEN B.RaterTypeCode IN ('Subordinate','Peer','JointWorker') THEN 25.0
            ELSE 0.0
        END AS Weight
    FROM BaseAverages B
),
MainScores AS (
    SELECT CompetencyName,
        CAST(ISNULL(MAX(CASE WHEN RaterTypeCode='Self'     THEN GrpAvg END),0) AS DECIMAL(5,2)) AS SelfScore,
        CAST(ISNULL(MAX(CASE WHEN RaterTypeCode='Manager1' THEN GrpAvg END),0) AS DECIMAL(5,2)) AS ManagerScore,
        CAST(ISNULL(SUM(CASE WHEN RaterTypeCode<>'Self' THEN GrpAvg*Weight END)
              /NULLIF(SUM(CASE WHEN RaterTypeCode<>'Self' THEN Weight END),0),0) AS DECIMAL(5,2)) AS WeightedScore,
        CAST(ISNULL(SUM(CASE WHEN RaterTypeCode<>'Self' THEN GrpAvg*Weight END)
              /NULLIF(SUM(CASE WHEN RaterTypeCode<>'Self' THEN Weight END),0),0)
              - ISNULL(MAX(CASE WHEN RaterTypeCode='Self' THEN GrpAvg END),0) AS DECIMAL(5,2)) AS JohariGap
    FROM WeightLogic GROUP BY CompetencyName
),
TitleAvg AS (
    SELECT C.CompetencyName, AVG(CAST(RI.NumericAnswer AS FLOAT)) AS CompanyTitleAvg
    FROM ResponseItems RI
    JOIN ResponseHeaders RH ON RI.ResponseHeaderID=RH.ResponseHeaderID
    JOIN Employees_Tablo E  ON RH.RateePersonelCode=E.PersonelCode
    JOIN Questions Q         ON RI.QuestionID=Q.QuestionID
    JOIN Competencies C      ON Q.CompetencyID=C.CompetencyID
    WHERE RH.Status='Completed' AND RI.NumericAnswer>0 AND E.Unvan=@title
    GROUP BY C.CompetencyName
)
SELECT M.CompetencyName, M.SelfScore, M.ManagerScore, M.WeightedScore, M.JohariGap,
       CAST(ISNULL(T.CompanyTitleAvg,0) AS DECIMAL(5,2)) AS CompanyTitleAvg
FROM MainScores M LEFT JOIN TitleAvg T ON M.CompetencyName=T.CompetencyName
ORDER BY M.CompetencyName;

-- 2. SORU DETAYI
WITH BaseQ AS (
    SELECT C.CompetencyName,
           LTRIM(RTRIM(REPLACE(REPLACE(Q.QuestionText,CHAR(13),''),CHAR(10),''))) AS QuestionText,
           RT.RaterTypeCode, E.Yaka, AVG(CAST(RI.NumericAnswer AS FLOAT)) AS GrpAvg
    FROM ResponseItems RI
    JOIN ResponseHeaders RH ON RI.ResponseHeaderID=RH.ResponseHeaderID
    JOIN Employees_Tablo E  ON RH.RateePersonelCode=E.PersonelCode
    JOIN Questions Q         ON RI.QuestionID=Q.QuestionID
    JOIN Competencies C      ON Q.CompetencyID=C.CompetencyID
    JOIN RaterTypes RT       ON RH.RaterTypeID=RT.RaterTypeID
    WHERE RH.RateePersonelCode=@code AND RH.Status='Completed' AND RI.NumericAnswer>0{yf}
    GROUP BY C.CompetencyName,LTRIM(RTRIM(REPLACE(REPLACE(Q.QuestionText,CHAR(13),''),CHAR(10),''))),RT.RaterTypeCode,E.Yaka
),
RFQ AS (
    SELECT CASE WHEN COUNT(*)>0 THEN 1 ELSE 0 END AS HasSub
    FROM RaterAssignments RA JOIN RaterTypes RT ON RA.RaterTypeID=RT.RaterTypeID
    WHERE RA.RateePersonelCode=@code AND RT.RaterTypeCode='Subordinate'
),
WLQ AS (
    SELECT B.*,
        CASE
            WHEN B.Yaka='Mavi'  AND B.RaterTypeCode LIKE 'Manager%' THEN 50.0
            WHEN B.Yaka='Beyaz' AND B.RaterTypeCode LIKE 'Manager%'
                THEN CASE WHEN (SELECT HasSub FROM RFQ)=1 THEN 25.0 ELSE 50.0 END
            WHEN B.RaterTypeCode IN ('Subordinate','Peer','JointWorker') THEN 25.0
            ELSE 0.0
        END AS Weight
    FROM BaseQ B
),
MainQ AS (
    SELECT CompetencyName, QuestionText,
        CAST(ISNULL(MAX(CASE WHEN RaterTypeCode='Self'     THEN GrpAvg END),0) AS DECIMAL(5,2)) AS SelfScore,
        CAST(ISNULL(MAX(CASE WHEN RaterTypeCode='Manager1' THEN GrpAvg END),0) AS DECIMAL(5,2)) AS ManagerScore,
        CAST(ISNULL(SUM(CASE WHEN RaterTypeCode<>'Self' THEN GrpAvg*Weight END)
              /NULLIF(SUM(CASE WHEN RaterTypeCode<>'Self' THEN Weight END),0),0) AS DECIMAL(5,2)) AS WeightedScore
    FROM WLQ GROUP BY CompetencyName, QuestionText
),
TitleAvgQ AS (
    SELECT C.CompetencyName,
           LTRIM(RTRIM(REPLACE(REPLACE(Q.QuestionText,CHAR(13),''),CHAR(10),''))) AS QuestionText,
           AVG(CAST(RI.NumericAnswer AS FLOAT)) AS CompanyTitleAvg
    FROM ResponseItems RI
    JOIN ResponseHeaders RH ON RI.ResponseHeaderID=RH.ResponseHeaderID
    JOIN Employees_Tablo E  ON RH.RateePersonelCode=E.PersonelCode
    JOIN Questions Q         ON RI.QuestionID=Q.QuestionID
    JOIN Competencies C      ON Q.CompetencyID=C.CompetencyID
    WHERE RH.Status='Completed' AND RI.NumericAnswer>0 AND E.Unvan=@title
    GROUP BY C.CompetencyName,LTRIM(RTRIM(REPLACE(REPLACE(Q.QuestionText,CHAR(13),''),CHAR(10),'')))
)
SELECT M.CompetencyName, M.QuestionText, M.SelfScore, M.ManagerScore, M.WeightedScore,
       CAST(ISNULL(T.CompanyTitleAvg,0) AS DECIMAL(5,2)) AS CompanyTitleAvg
FROM MainQ M LEFT JOIN TitleAvgQ T ON M.CompetencyName=T.CompetencyName AND M.QuestionText=T.QuestionText
ORDER BY M.CompetencyName, M.QuestionText;

-- 3. EĞİTİM PLANI
WITH BaseT AS (
    SELECT C.CompetencyName, RT.RaterTypeCode, E.Yaka,
           AVG(CAST(RI.NumericAnswer AS FLOAT)) AS GrpAvg
    FROM ResponseItems RI
    JOIN ResponseHeaders RH ON RI.ResponseHeaderID=RH.ResponseHeaderID
    JOIN Employees_Tablo E  ON RH.RateePersonelCode=E.PersonelCode
    JOIN Questions Q         ON RI.QuestionID=Q.QuestionID
    JOIN Competencies C      ON Q.CompetencyID=C.CompetencyID
    JOIN RaterTypes RT       ON RH.RaterTypeID=RT.RaterTypeID
    WHERE RH.RateePersonelCode=@code AND RH.Status='Completed' AND RI.NumericAnswer>0{yf}
    GROUP BY C.CompetencyName, RT.RaterTypeCode, E.Yaka
),
RFT AS (
    SELECT CASE WHEN COUNT(*)>0 THEN 1 ELSE 0 END AS HasSub
    FROM RaterAssignments RA JOIN RaterTypes RT ON RA.RaterTypeID=RT.RaterTypeID
    WHERE RA.RateePersonelCode=@code AND RT.RaterTypeCode='Subordinate'
),
WLT AS (
    SELECT B.*,
        CASE
            WHEN B.Yaka='Mavi'  AND B.RaterTypeCode LIKE 'Manager%' THEN 50.0
            WHEN B.Yaka='Beyaz' AND B.RaterTypeCode LIKE 'Manager%'
                THEN CASE WHEN (SELECT HasSub FROM RFT)=1 THEN 25.0 ELSE 50.0 END
            WHEN B.RaterTypeCode IN ('Subordinate','Peer','JointWorker') THEN 25.0
            ELSE 0.0
        END AS Weight
    FROM BaseT B
),
FST AS (
    SELECT CompetencyName,
        CAST(ISNULL(SUM(CASE WHEN RaterTypeCode<>'Self' THEN GrpAvg*Weight END)
              /NULLIF(SUM(CASE WHEN RaterTypeCode<>'Self' THEN Weight END),0),0) AS DECIMAL(5,2)) AS WeightedScore
    FROM WLT GROUP BY CompetencyName
)
SELECT F.CompetencyName, F.WeightedScore,
    CASE WHEN F.WeightedScore<=2.99 THEN 'Düşük' WHEN F.WeightedScore<=4.00 THEN 'Orta' ELSE 'Yüksek' END AS Seviye,
    ISNULL(T.TrainingName,'Gelişim planı atanmamış.') AS TrainingName,
    ISNULL(T.TrainingDescription,'Detay bulunmuyor.') AS TrainingDescription,
    ISNULL(T.TrainingType,'') AS TrainingType,
    ISNULL(T.TrainingLink,'') AS TrainingLink
FROM FST F
LEFT JOIN Trainings T ON F.CompetencyName=T.CompetencyName
    AND T.Level=(CASE WHEN F.WeightedScore<=2.99 THEN 'Düşük' WHEN F.WeightedScore<=4.00 THEN 'Orta' ELSE 'Yüksek' END);

-- 4. DONUT AĞIRLIKLARI
WITH RateeDataD AS (
    SELECT Yaka,
        (SELECT CASE WHEN COUNT(*)>0 THEN 1 ELSE 0 END
         FROM RaterAssignments RA2 JOIN RaterTypes RT2 ON RA2.RaterTypeID=RT2.RaterTypeID
         WHERE RA2.RateePersonelCode=@code AND RT2.RaterTypeCode='Subordinate') AS HasSub
    FROM Employees_Tablo WHERE PersonelCode=@code
),
BaseWD AS (
    SELECT RT.Description AS Rol, COUNT(DISTINCT RA.RaterPersonelCode) AS KisiSayisi,
        CASE
            WHEN MAX(D.Yaka)='Mavi'  AND RT.RaterTypeCode LIKE 'Manager%' THEN 50.0
            WHEN MAX(D.Yaka)='Beyaz' AND RT.RaterTypeCode LIKE 'Manager%'
                THEN CASE WHEN MAX(D.HasSub)=1 THEN 25.0 ELSE 50.0 END
            WHEN RT.RaterTypeCode IN ('Subordinate','Peer','JointWorker') THEN 25.0
            ELSE 0.0
        END AS RawWeight
    FROM RaterAssignments RA
    JOIN RaterTypes RT ON RA.RaterTypeID=RT.RaterTypeID
    CROSS JOIN RateeDataD D
    WHERE RA.RateePersonelCode=@code AND RT.RaterTypeCode<>'Self'
      AND EXISTS (SELECT 1 FROM ResponseHeaders RH
                  WHERE RH.RateePersonelCode=RA.RateePersonelCode
                    AND RH.RaterPersonelCode=RA.RaterPersonelCode
                    AND RH.Status='Completed')
    GROUP BY RT.Description, RT.RaterTypeCode
),
TotalWD AS (SELECT SUM(RawWeight) AS TotalW FROM BaseWD),
GWD AS (
    SELECT
        CASE
            WHEN Rol LIKE '%Ekip Arkadaşı%' OR Rol LIKE '%Ekip Arkadaşları%' THEN 'Ekip Arkadaşları'
            WHEN Rol LIKE '%Ortak İş%' THEN 'Ortak İş Yürütülenler'
            ELSE Rol
        END AS Rol,
        SUM(KisiSayisi) AS KisiSayisi,
        SUM(RawWeight) AS TotalGroupWeight
    FROM BaseWD WHERE RawWeight>0
    GROUP BY
        CASE
            WHEN Rol LIKE '%Ekip Arkadaşı%' OR Rol LIKE '%Ekip Arkadaşları%' THEN 'Ekip Arkadaşları'
            WHEN Rol LIKE '%Ortak İş%' THEN 'Ortak İş Yürütülenler'
            ELSE Rol
        END
)
SELECT Rol, KisiSayisi,
       CAST(ROUND((TotalGroupWeight*100.0)/NULLIF((SELECT TotalW FROM TotalWD),0),0) AS INT) AS EtkiYuzdesi
FROM GWD;";

                DataTable dtYetkinlik = null, dtSorular = null, dtEgitimler = null, dtAgirliklar = null;

                using (var conn = new SqlConnection(System.Configuration.ConfigurationManager
                    .ConnectionStrings["LiftUpConnection"].ConnectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@code", _userCode);
                        cmd.Parameters.AddWithValue("@title", _title ?? "");
                        cmd.CommandTimeout = 60;
                        using (var da = new SqlDataAdapter(cmd))
                        {
                            var ds = new DataSet();
                            da.Fill(ds);
                            if (ds.Tables.Count >= 4)
                            {
                                dtYetkinlik = ds.Tables[0];
                                dtSorular   = ds.Tables[1];
                                dtEgitimler = ds.Tables[2];
                                dtAgirliklar = ds.Tables[3];
                            }
                        }
                    }
                }

                if (dtYetkinlik == null || dtYetkinlik.Rows.Count == 0)
                {
                    MessageBox.Show("Bu döneme ait tamamlanmış değerlendirme verisi bulunamadı.",
                        "Veri Yok", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // ── 2. Genel skor ──────────────────────────────────────────
                double overallScore = dtYetkinlik.AsEnumerable()
                    .Average(r => ToDouble(r["WeightedScore"]));

                // ── 3. ReportForm ile birebir aynı biçimlendirme ─────────
                string titleDeptLine    = TitleDepartmentParser.FormatTitleDepartment(_title, _department);
                string coverDeptLine    = string.IsNullOrWhiteSpace(_department) ? "" : _department.Trim();
                string coverTitleLine   = string.IsNullOrWhiteSpace(_title)      ? "" : ("Unvan: " + _title).Trim();
                string coverDeptAndTitle = string.Join("\n",
                    new[] { coverDeptLine, coverTitleLine }.Where(s => !string.IsNullOrWhiteSpace(s)));

                var parsed = TitleDepartmentParser.ParseTitleDepartmentLine(titleDeptLine);
                string unvan = parsed.Title;
                string birim = parsed.Department;

                // ── 4. AI Ağırlık + Nihai Skor (ReportForm ile aynı) ─────
                var compNames = dtYetkinlik.AsEnumerable()
                    .Select(r => r["CompetencyName"]?.ToString()?.Trim() ?? "")
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct().ToList();

                var weightSvc  = new GeminiWeightService(ApiKey);
                var weights    = await weightSvc.CalculateWeightsAsync(birim, compNames);
                var scoringSvc = new PerformanceScoringService();
                double finalScore = scoringSvc.ComputeFinalScore5(dtYetkinlik, weights, overallScore);

                // ── 5. AI Yorum (ReportForm ile aynı parametre sırası) ───
                var narrativeSvc = new GeminiNarrativeService(ApiKey);
                // isEmployeeView=true → Kariyer Koçu dili, çalışana 2. tekil şahısla hitap
                string aiText    = await narrativeSvc.GenerateNarrativeAsync(
                    _title, _department, dtYetkinlik, dtSorular, finalScore,
                    isEmployeeView: true);

                // ── 6. Chart nesneleri (off-screen) ──────────────────────
                Chart chartGap   = BuildGapChart(dtYetkinlik, overallScore);
                Chart chartDonut = BuildDonutChart(dtAgirliklar);

                // ── 7. PDF oluştur ve aç ──────────────────────────────────
                string folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "LiftUp360", "Raporlar");
                Directory.CreateDirectory(folder);

                string fileName = $"{_fullName.Replace(" ", "_")}_Donem{period.CycleId}_Rapor.pdf";
                string filePath = Path.Combine(folder, fileName);

                PdfReportHelper.CreateReport(
                    filePath,
                    _fullName,
                    _title ?? "",
                    coverDeptAndTitle,
                    chartGap,
                    chartDonut,
                    dtYetkinlik,
                    dtSorular,
                    dtEgitimler,
                    dtAgirliklar,
                    finalScore,
                    aiText,
                    isEmployeeView: true   // çalışan kendi raporunu görüntülüyor → 2. tekil şahıs
                );

                System.Diagnostics.Process.Start(filePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Rapor oluşturulurken hata:\n" + ex.Message,
                    "Hata", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                btn.Enabled = true;
                btn.Text    = "Raporu Aç";
            }
        }

        // ── ReportForm.SetupChart ile birebir aynı ───────────────────────────
        private static Chart SetupOffscreenChart(SeriesChartType type)
        {
            var chart = new Chart { BackColor = Color.Transparent };
            chart.ChartAreas.Clear();
            chart.ChartAreas.Add(new ChartArea { BackColor = Color.Transparent });
            chart.Legends.Clear();
            chart.Legends.Add(new Legend
            {
                Docking    = Docking.Bottom,
                Alignment  = StringAlignment.Center,
                BackColor  = Color.Transparent,
                Font       = new Font("Segoe UI", 10, FontStyle.Regular)
            });
            return chart;
        }

        // ── ReportForm.LoadRealDataFromSQL'deki chartGap ile birebir aynı ───
        private Chart BuildGapChart(DataTable dtYetkinlik, double overallScore)
        {
            var chart = SetupOffscreenChart(SeriesChartType.Bar);
            chart.BackColor = Color.Transparent;

            double sumSelf = 0, sumWeighted = 0;
            int rowCount = 0;
            foreach (DataRow dr in dtYetkinlik.Rows)
            {
                sumSelf     += ToDouble(dr["SelfScore"]);
                sumWeighted += ToDouble(dr["WeightedScore"]);
                rowCount++;
            }
            if (rowCount == 0) return chart;

            double avgSelf    = sumSelf    / rowCount;
            double avgGeneral = sumWeighted / rowCount;

            var sSummary = new Series("Ozet") { ChartType = SeriesChartType.Bar };
            sSummary["PointWidth"]        = "0.5";
            sSummary.IsValueShownAsLabel  = true;
            sSummary.Font                 = new Font("Segoe UI", 12, FontStyle.Bold);
            sSummary.LabelForeColor       = Color.Black;
            sSummary.IsVisibleInLegend    = false;

            int idxGen  = sSummary.Points.AddXY("Genel Ortalama", avgGeneral);
            sSummary.Points[idxGen].Color = Color.FromArgb(0, 174, 239);
            sSummary.Points[idxGen].Label = avgGeneral.ToString("0.0");

            int idxSelf = sSummary.Points.AddXY("Siz", avgSelf);
            sSummary.Points[idxSelf].Color = Color.FromArgb(238, 50, 36);
            sSummary.Points[idxSelf].Label = avgSelf.ToString("0.0");

            chart.Series.Add(sSummary);

            chart.ChartAreas[0].AxisX.MajorGrid.Enabled          = false;
            chart.ChartAreas[0].AxisY.MajorGrid.LineColor         = Color.LightGray;
            chart.ChartAreas[0].AxisY.MajorGrid.LineDashStyle     = ChartDashStyle.Dash;
            chart.ChartAreas[0].AxisX.LabelStyle.Font             = new Font("Segoe UI", 11, FontStyle.Bold);
            chart.ChartAreas[0].AxisX.Interval                    = 1;
            chart.ChartAreas[0].AxisY.Minimum                     = 0;
            chart.ChartAreas[0].AxisY.Maximum                     = 5;
            chart.ChartAreas[0].AxisY.Interval                    = 1;

            chart.Titles.Clear();
            chart.Titles.Add(new Title(
                "FARK SKORU (GENEL ORTALAMA)",
                Docking.Top,
                new Font("Segoe UI", 14, FontStyle.Bold),
                Color.Black));

            return chart;
        }

        // ── ReportForm.LoadRealDataFromSQL'deki chartDonut ile birebir aynı ─
        private Chart BuildDonutChart(DataTable dtAgirliklar)
        {
            var chart = SetupOffscreenChart(SeriesChartType.Doughnut);
            chart.BackColor = Color.White;
            chart.Size      = new Size(600, 400);
            chart.Legends.Clear();   // donut için legend yok (ReportForm da kaldırıyor)

            if (dtAgirliklar == null || dtAgirliklar.Rows.Count == 0) return chart;

            var sDonut = new Series("Etki") { ChartType = SeriesChartType.Doughnut };
            sDonut["PieLabelStyle"]  = "Outside";
            sDonut["DoughnutRadius"] = "40";

            Color[] palette = {
                Color.FromArgb(41, 128, 185), Color.FromArgb(46, 204, 113),
                Color.FromArgb(241, 196, 15), Color.FromArgb(155, 89, 182),
                Color.Gray
            };

            var donutLabels = new List<string>();
            var donutValues = new List<double>();
            double totalEtki = 0;

            foreach (DataRow r in dtAgirliklar.Rows)
            {
                string rol = r["Rol"].ToString();
                if (rol == "Kendi") continue;
                double etki = r["EtkiYuzdesi"] != DBNull.Value ? ToDouble(r["EtkiYuzdesi"]) : 0;
                if (etki > 0) { donutLabels.Add(rol); donutValues.Add(etki); totalEtki += etki; }
            }

            // %100 tamamlama garantisi (ReportForm ile aynı mantık)
            if (donutValues.Count > 0 && totalEtki > 0 && totalEtki != 100)
            {
                double diff = 100 - totalEtki;
                int maxIdx  = donutValues.IndexOf(donutValues.Max());
                donutValues[maxIdx] += diff;
            }

            int colorIdx = 0;
            for (int i = 0; i < donutLabels.Count; i++)
            {
                if (donutValues[i] <= 0) continue;
                int pt = sDonut.Points.AddXY(donutLabels[i], donutValues[i]);
                sDonut.Points[pt].Label = $"{donutLabels[i]}\n%{donutValues[i]}";
                sDonut.Points[pt].Color = palette[colorIdx % palette.Length];
                sDonut.Points[pt].Font  = new Font("Segoe UI", 10, FontStyle.Bold);
                colorIdx++;
            }

            chart.Series.Add(sDonut);
            return chart;
        }

        // ════════════════════════════════════════════════════════════════════
        //  YAPAY ZEKA ANALİZİ
        // ════════════════════════════════════════════════════════════════════
        private async void BtnAnalyze_Click(object sender, EventArgs e)
        {
            _btnAnalyze.Enabled = false;
            _btnAnalyze.Text    = "Hazırlanıyor...";
            _rtbAI.Clear();
            _lblAIStatus.Text   = "Yapay zeka analizi hazırlanıyor, lütfen bekleyin...";

            try
            {
                // Henüz yüklenmeyen dönemleri yükle
                foreach (var p in _periods.Where(p => p.Data == null))
                    await Task.Run(() => LoadPeriodData(p));

                string result = await GenerateAIAsync();
                RenderAnalysis(result);
                _lblAIStatus.Text = "";
            }
            catch (Exception ex)
            {
                _rtbAI.Text       = "Analiz oluşturulamadı: " + ex.Message;
                _lblAIStatus.Text = "";
            }
            finally
            {
                _btnAnalyze.Enabled = true;
                _btnAnalyze.Text    = "Analiz Et";
            }
        }

        // AI metnini okunaklı biçimde RichTextBox'a yazar: BÜYÜK HARF başlıklar kalın/renkli,
        // gövde normal, bölümler arası boşluklu.
        private void RenderAnalysis(string text)
        {
            _rtbAI.Clear();
            if (string.IsNullOrWhiteSpace(text)) return;

            var headFont  = new Font("Segoe UI Semibold", 12.5f, FontStyle.Bold);
            var bodyFont  = new Font("Segoe UI", 11f);
            var headColor = Color.FromArgb(0, 43, 92);
            var trUp      = System.Globalization.CultureInfo.GetCultureInfo("tr-TR");

            foreach (string raw in text.Replace("\r\n", "\n").Split('\n'))
            {
                string line = raw.Trim();
                bool isHeading = line.Length > 0 && line.EndsWith(":")
                                 && line == line.ToUpper(trUp) && line.Length <= 60;

                if (isHeading)
                {
                    if (_rtbAI.TextLength > 0) _rtbAI.AppendText("\n");
                    _rtbAI.SelectionFont  = headFont;
                    _rtbAI.SelectionColor = headColor;
                    _rtbAI.AppendText(line.TrimEnd(':').Trim() + "\n");
                }
                else
                {
                    _rtbAI.SelectionFont  = bodyFont;
                    _rtbAI.SelectionColor = C_Text;
                    _rtbAI.AppendText(line + "\n");
                }
            }

            SetLineSpacing(_rtbAI, 1.55);   // satır aralığını ferahlat (line-height ~1.6)
            _rtbAI.SelectionStart = 0;
            _rtbAI.ScrollToCaret();
        }

        // ── RichTextBox satır aralığı (line-height) — Win32 EM_SETPARAFORMAT ───
        [StructLayout(LayoutKind.Sequential)]
        private struct PARAFORMAT2
        {
            public int cbSize;
            public uint dwMask;
            public short wNumbering;
            public short wEffects;
            public int dxStartIndent;
            public int dxRightIndent;
            public int dxOffset;
            public short wAlignment;
            public short cTabCount;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public int[] rgxTabs;
            public int dySpaceBefore;
            public int dySpaceAfter;
            public int dyLineSpacing;
            public short sStyle;
            public byte bLineSpacingRule;
            public byte bOutlineLevel;
            public short wShadingWeight;
            public short wShadingStyle;
            public short wNumberingStart;
            public short wNumberingStyle;
            public short wNumberingTab;
            public short wBorderSpace;
            public short wBorderWidth;
            public short wBorders;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, ref PARAFORMAT2 lParam);
        private const int EM_SETPARAFORMAT = 0x0447;
        private const int SCF_SELECTION    = 0x0001;
        private const uint PFM_LINESPACING = 0x00000100;

        private static void SetLineSpacing(RichTextBox rtb, double lines)
        {
            try
            {
                var pf = new PARAFORMAT2
                {
                    cbSize           = Marshal.SizeOf(typeof(PARAFORMAT2)),
                    rgxTabs          = new int[32],
                    dwMask           = PFM_LINESPACING,
                    bLineSpacingRule = 5,                  // dyLineSpacing = satırın yirmide biri cinsinden
                    dyLineSpacing    = (int)(lines * 20.0) // 1.55 satır ≈ 31
                };
                int sel0 = rtb.SelectionStart, len0 = rtb.SelectionLength;
                rtb.SelectAll();
                SendMessage(rtb.Handle, EM_SETPARAFORMAT, SCF_SELECTION, ref pf);
                rtb.Select(sel0, len0);
            }
            catch { /* satır aralığı kritik değil */ }
        }

        private async Task<string> GenerateAIAsync()
        {
            var aktifPeriod = _periods.FirstOrDefault(p => p.IsActive);
            var gecmisler  = _periods.Where(p => !p.IsActive && p.Data != null && p.Data.Rows.Count > 0)
                                      .OrderByDescending(p => p.CycleId)
                                      .ToList();

            if (aktifPeriod?.Data == null || aktifPeriod.Data.Rows.Count == 0)
            {
                // Aktif dönem verisi yoksa, mevcut olan tüm dönemleri analiz et
                var mevcutlar = _periods.Where(p => p.Data != null && p.Data.Rows.Count > 0).ToList();
                if (mevcutlar.Count == 0)
                    return "Analiz yapılabilmesi için en az bir tamamlanmış değerlendirme dönemi gereklidir.";
                aktifPeriod = mevcutlar.OrderByDescending(p => p.CycleId).First();
                gecmisler   = mevcutlar.Where(p => p.CycleId != aktifPeriod.CycleId).ToList();
            }

            // Dönem özetlerini hazırla
            string aktifOzet = DönemOzeti(aktifPeriod);
            var gecmisOzetler = gecmisler.Select(p => DönemOzeti(p)).ToList();
            bool gecmisVar = gecmisOzetler.Count > 0;

            string prompt;
            if (gecmisVar)
            {
                prompt =
                    $"Çalışan: {_fullName}  |  Unvan: {_title}  |  Birim: {_department}\n\n" +
                    $"EN SON DÖNEM:\n{aktifOzet}\n\n" +
                    $"GEÇMİŞ DÖNEMLER:\n{string.Join("\n", gecmisOzetler)}\n\n" +
                    "Lütfen aşağıdaki 5 bölümü sırasıyla yaz. Her bölüm başlığını BÜYÜK HARFLE yaz ve iki nokta üst üste ile bitir. " +
                    "Her bölüm aralarına boş satır bırak. Madde işareti veya liste kullanma, akıcı paragraf yaz:\n\n" +
                    "GELİŞİM YOLCULUĞU:\n" +
                    "Dönemler arasındaki değişimi somut puanlarla anlat. Hangi yetkinlikte ne kadar gelişti?\n\n" +
                    "PARLAYAN YILDIZLAR:\n" +
                    "Tutarlı güçlü kalan veya en çok gelişme gösteren yetkinlikleri öv.\n\n" +
                    "GELİŞİM FIRSATI:\n" +
                    "Gelişim gerektiren alanlara 2-3 somut ve uygulanabilir öneri sun. Bunları fırsat olarak çerçevele.\n\n" +
                    "KARİYER VİZYONU:\n" +
                    "Bu trende bakarak önümüzdeki dönemde ulaşabileceği potansiyeli anlat.\n\n" +
                    "İLHAM MESAJI:\n" +
                    "Çalışana samimi, güçlü ve motive edici 3 cümlelik kapanış mesajı yaz.";
            }
            else
            {
                prompt =
                    $"Çalışan: {_fullName}  |  Unvan: {_title}  |  Birim: {_department}\n\n" +
                    $"DÖNEM VERİSİ:\n{aktifOzet}\n\n" +
                    "Lütfen aşağıdaki 5 bölümü sırasıyla yaz. Her bölüm başlığını BÜYÜK HARFLE yaz ve iki nokta üst üste ile bitir. " +
                    "Her bölüm aralarına boş satır bırak. Madde işareti veya liste kullanma, akıcı paragraf yaz:\n\n" +
                    "PERFORMANS PORTRESİ:\n" +
                    "Genel skor ne anlama geliyor, çalışanın profiliyle ilişkilendir.\n\n" +
                    "GÜÇLÜ YÖNLERİN IŞIĞI:\n" +
                    "En yüksek yetkinlikleri öv, ekibe ve kuruma katkısını anlat.\n\n" +
                    "GELİŞİM HARİTASI:\n" +
                    "2-3 somut, uygulanabilir öneri sun. Kariyer hızlanması fırsatı olarak çerçevele.\n\n" +
                    "ÖZ FARKINDALIK:\n" +
                    "Kendi puanı ile çevre puanı arasındaki farkı yapıcı şekilde yorumla.\n\n" +
                    "İLHAM MESAJI:\n" +
                    "Çalışana samimi, güçlü ve motive edici 3 cümlelik kapanış mesajı yaz.";
            }

            string sys =
                "Sen LiftUp 360 performans sisteminin kariyer gelişim koçusun. " +
                "Çalışana 'Sen' diyerek sıcak, samimi ve cesaretlendirici bir dille yaz. " +
                "Asla * veya # gibi markdown işaretleri kullanma. " +
                "Bölüm başlıklarını aynen yaz (büyük harf + iki nokta). " +
                "Her bölüm aralarında tek boş satır bırak. " +
                "Çalışanın güçlü yönlerini ön plana çıkar, gelişim alanlarını tehdit değil fırsat olarak sun.";

            using (var g = new GeminiClient(null, AiPurpose.Narrative))
            {
                string result = await g.GenerateAsync(prompt, false, sys);
                return result.Replace("**", "").Replace("##", "").Replace("*", "").Trim();
            }
        }

        private string DönemOzeti(PeriodInfo p)
        {
            if (p.Data == null || p.Data.Rows.Count == 0) return $"{p.CycleName}: Veri yok";

            double ort  = p.Data.AsEnumerable().Average(r => ToDouble(r["WeightedScore"]));
            double self = p.Data.AsEnumerable().Average(r => ToDouble(r["SelfScore"]));
            var guclu   = p.Data.AsEnumerable().OrderByDescending(r => ToDouble(r["WeightedScore"]))
                                .Take(2).Select(r => $"{r["CompetencyName"]} ({ToDouble(r["WeightedScore"]):0.00})");
            var gelisim = p.Data.AsEnumerable().OrderBy(r => ToDouble(r["WeightedScore"]))
                                .Take(2).Select(r => $"{r["CompetencyName"]} ({ToDouble(r["WeightedScore"]):0.00})");

            return $"{p.CycleName} | " +
                   $"Ağırlıklı Ort: {ort:0.00} | Öz Değerlendirme: {self:0.00} | " +
                   $"Güçlü: [{string.Join(", ", guclu)}] | " +
                   $"Gelişim: [{string.Join(", ", gelisim)}]";
        }

        // ─────────────────────────────────────────────────────────────────────
        private static double ToDouble(object o)
        {
            try { return o == null || o == DBNull.Value ? 0 : Convert.ToDouble(o); }
            catch { return 0; }
        }

        private void MyReportsForm_Load(object sender, EventArgs e)
        {

        }
    }
}
