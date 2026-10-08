using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using WindowsFormsApp1.AI;

namespace WindowsFormsApp1
{
	public partial class ReportForm : Form
	{
		// --- AYARLAR ---
		private string connectionString = @"Server=.\SQLEXPRESS;Database=LiftUp360DB;Trusted_Connection=True;TrustServerCertificate=True;";

		private string defaultTestSicil = "P086";
		private string defaultTestIsim = "Kenan Bal";
		private string defaultTestUnvan = "MÜHENDİS";

		// --- DEĞİŞKENLER ---
		private string _selectedPersonName;
		private string _selectedPersonTitle;
		private string _selectedPersonDepartment;
		private string _selectedPersonCode;

		// UI
		private Panel pnlContent;
		private FlowLayoutPanel flowTeam;
		private Chart chartRadar;
		private Chart chartGap;
		private Chart chartDonut;
		private Button btnCreateReport;

		// Veri Tabloları 
		private DataTable _dtAnaYetkinlikler;
		private DataTable _dtSorular;
		private DataTable _dtEgitimler;
		private DataTable _dtAgirliklar;
		private double _overallScore = 0;

		public ReportForm(string pCode, string pName = "", string pTitle = "", string pDepartment = "")
		{
			InitializeComponent();
			this.Size = new Size(1450, 950);
			this.StartPosition = FormStartPosition.CenterScreen;
			this.Text = "LIFTUP 360 - Profesyonel Raporlama";

			BuildLayout();

			if (!string.IsNullOrEmpty(pName)) LoadRealDataFromSQL(pCode, pName, pTitle, pDepartment);
			else LoadManualTeam();
		}

		private void BuildLayout()
		{
			this.Controls.Clear();
			TableLayoutPanel tlpMain = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
			tlpMain.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 320F));
			tlpMain.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
			this.Controls.Add(tlpMain);

			// Sol Panel
			Panel pnlLeft = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
			tlpMain.Controls.Add(pnlLeft, 0, 0);

			Button btnBack = new Button { Text = "← KAPAT", Dock = DockStyle.Top, Height = 60, FlatStyle = FlatStyle.Flat, ForeColor = Color.Gray, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
			btnBack.Click += (s, e) => this.Close();
			pnlLeft.Controls.Add(btnBack);

			Label lblListTitle = new Label { Text = "EKİP LİSTESİ", Dock = DockStyle.Top, Height = 50, Font = new Font("Segoe UI", 12, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter };
			pnlLeft.Controls.Add(lblListTitle);

			flowTeam = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(10), AutoScroll = true };
			pnlLeft.Controls.Add(flowTeam);

			// Sağ Panel
			pnlContent = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(248, 249, 251), Padding = new Padding(40) };
			tlpMain.Controls.Add(pnlContent, 1, 0);

			Panel pnlHeader = new Panel { Dock = DockStyle.Top, Height = 80 };
			pnlContent.Controls.Add(pnlHeader);

			Label lblHeader = new Label { Name = "lblHeader", Text = "Personel Seçimi Bekleniyor...", Font = new Font("Segoe UI", 24, FontStyle.Bold), AutoSize = true, Location = new Point(0, 15) };
			pnlHeader.Controls.Add(lblHeader);

			btnCreateReport = new Button { Text = "📄 PDF OLUŞTUR", Size = new Size(200, 50), Location = new Point(pnlHeader.Width - 220, 15), Anchor = AnchorStyles.Top | AnchorStyles.Right, BackColor = Color.LightGray, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Enabled = false };
			btnCreateReport.Click += BtnPdf_Click;
			pnlHeader.Controls.Add(btnCreateReport);

			TableLayoutPanel tlpCharts = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(0, 80, 0, 0) };
			tlpCharts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55F));
			tlpCharts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45F));
			pnlContent.Controls.Add(tlpCharts);

			chartRadar = new Chart { Dock = DockStyle.Fill, BackColor = Color.Transparent };
			SetupChart(chartRadar, SeriesChartType.Radar);
			tlpCharts.Controls.Add(chartRadar, 0, 0);

			chartGap = new Chart { Dock = DockStyle.Fill, BackColor = Color.Transparent };
			SetupChart(chartGap, SeriesChartType.Bar);
			tlpCharts.Controls.Add(chartGap, 1, 0);

			chartDonut = new Chart { Size = new Size(600, 400), BackColor = Color.White };
			SetupChart(chartDonut, SeriesChartType.Doughnut);
			chartDonut.Legends.Clear();
		}

		private void LoadManualTeam()
		{
			flowTeam.Controls.Clear();
			Button btn = new Button { Text = $"{defaultTestIsim}\n{defaultTestUnvan}", Size = new Size(280, 80), BackColor = Color.White, FlatStyle = FlatStyle.Flat, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(15, 0, 0, 0) };
			btn.Click += (s, e) => LoadRealDataFromSQL(defaultTestSicil, defaultTestIsim, defaultTestUnvan);
			flowTeam.Controls.Add(btn);
		}

		private void LoadRealDataFromSQL(string sicilNo, string name, string title, string department = "")
		{
			_selectedPersonName = name;
			_selectedPersonTitle = title;
			_selectedPersonCode = sicilNo;
			_selectedPersonDepartment = department;

			var lbls = pnlContent.Controls.Find("lblHeader", true);
			if (lbls.Length > 0) lbls[0].Text = $"{name} - Rapor";
			btnCreateReport.Enabled = true;
			btnCreateReport.BackColor = Color.FromArgb(41, 128, 185);

			_dtAnaYetkinlikler = new DataTable();
			_dtSorular = new DataTable();
			_dtEgitimler = new DataTable();
			_dtAgirliklar = new DataTable();

			using (SqlConnection conn = new SqlConnection(connectionString))
			{
				try
				{
					conn.Open();

					if (string.IsNullOrWhiteSpace(_selectedPersonDepartment))
					{
						_selectedPersonDepartment = GetDepartmentName(conn, sicilNo);
					}

					string sql = @"
    -- 1. ANA YETKİNLİKLER
    WITH BaseAverages AS (
        SELECT C.CompetencyName, RT.RaterTypeCode, E.Yaka,
               AVG(CAST(RI.NumericAnswer AS FLOAT)) AS GrpAvg
        FROM ResponseItems RI
        JOIN ResponseHeaders RH ON RI.ResponseHeaderID = RH.ResponseHeaderID
        JOIN Employees_Tablo E ON RH.RateePersonelCode = E.PersonelCode
        JOIN Questions Q ON RI.QuestionID = Q.QuestionID
        JOIN Competencies C ON Q.CompetencyID = C.CompetencyID
        JOIN RaterTypes RT ON RH.RaterTypeID = RT.RaterTypeID
        WHERE RH.RateePersonelCode = @code AND RH.Status = 'Completed' AND RH.CycleID = (SELECT TOP 1 CycleID FROM dbo.EvaluationCycles WHERE IsActive = 1) AND RI.NumericAnswer > 0
        GROUP BY C.CompetencyName, RT.RaterTypeCode, E.Yaka
    ),
    RateeFlags AS (
        SELECT CASE WHEN COUNT(*) > 0 THEN 1 ELSE 0 END AS HasSub
        FROM RaterAssignments RA JOIN RaterTypes RT ON RA.RaterTypeID = RT.RaterTypeID 
        WHERE RA.RateePersonelCode = @code AND RT.RaterTypeCode = 'Subordinate'
    ),
    WeightLogic AS (
        SELECT B.*,
            CASE 
                WHEN B.Yaka = 'Mavi' AND B.RaterTypeCode LIKE 'Manager%' THEN 50.0
                WHEN B.Yaka = 'Beyaz' AND B.RaterTypeCode LIKE 'Manager%' THEN CASE WHEN (SELECT HasSub FROM RateeFlags) = 1 THEN 25.0 ELSE 50.0 END
                WHEN B.RaterTypeCode IN ('Subordinate', 'Peer', 'JointWorker') THEN 25.0
                ELSE 0.0
            END AS Weight
        FROM BaseAverages B
    ),
    MainScores AS (
        SELECT CompetencyName,
            CAST(ISNULL(MAX(CASE WHEN RaterTypeCode = 'Self' THEN GrpAvg END), 0) AS DECIMAL(5,2)) AS SelfScore,
            CAST(ISNULL(MAX(CASE WHEN RaterTypeCode = 'Manager1' THEN GrpAvg END), 0) AS DECIMAL(5,2)) AS ManagerScore,
            CAST(ISNULL(SUM(CASE WHEN RaterTypeCode <> 'Self' THEN GrpAvg * Weight END) / NULLIF(SUM(CASE WHEN RaterTypeCode <> 'Self' THEN Weight END), 0), 0) AS DECIMAL(5,2)) AS WeightedScore,
            CAST(ISNULL(SUM(CASE WHEN RaterTypeCode <> 'Self' THEN GrpAvg * Weight END) / NULLIF(SUM(CASE WHEN RaterTypeCode <> 'Self' THEN Weight END), 0), 0) - ISNULL(MAX(CASE WHEN RaterTypeCode = 'Self' THEN GrpAvg END), 0) AS DECIMAL(5,2)) AS JohariGap
        FROM WeightLogic
        GROUP BY CompetencyName
    ),
    TitleAvg AS (
        SELECT C.CompetencyName, AVG(CAST(RI.NumericAnswer AS FLOAT)) AS CompanyTitleAvg
        FROM ResponseItems RI
        JOIN ResponseHeaders RH ON RI.ResponseHeaderID = RH.ResponseHeaderID
        JOIN Employees_Tablo E ON RH.RateePersonelCode = E.PersonelCode
        JOIN Questions Q ON RI.QuestionID = Q.QuestionID
        JOIN Competencies C ON Q.CompetencyID = C.CompetencyID
        WHERE RH.Status = 'Completed' AND RH.CycleID = (SELECT TOP 1 CycleID FROM dbo.EvaluationCycles WHERE IsActive = 1) AND RI.NumericAnswer > 0 
          AND E.Unvan = @title
        GROUP BY C.CompetencyName
    )
    SELECT M.CompetencyName, M.SelfScore, M.ManagerScore, M.WeightedScore, M.JohariGap,
           CAST(ISNULL(T.CompanyTitleAvg, 0) AS DECIMAL(5,2)) AS CompanyTitleAvg
    FROM MainScores M
    LEFT JOIN TitleAvg T ON M.CompetencyName = T.CompetencyName
    ORDER BY M.CompetencyName;

    -- 2. 21 SORULUK DETAY
    WITH BaseQuestions AS (
        SELECT C.CompetencyName, 
               LTRIM(RTRIM(REPLACE(REPLACE(Q.QuestionText, CHAR(13), ''), CHAR(10), ''))) AS QuestionText, 
               RT.RaterTypeCode, E.Yaka,
               AVG(CAST(RI.NumericAnswer AS FLOAT)) AS GrpAvg
        FROM ResponseItems RI
        JOIN ResponseHeaders RH ON RI.ResponseHeaderID = RH.ResponseHeaderID
        JOIN Employees_Tablo E ON RH.RateePersonelCode = E.PersonelCode
        JOIN Questions Q ON RI.QuestionID = Q.QuestionID
        JOIN Competencies C ON Q.CompetencyID = C.CompetencyID
        JOIN RaterTypes RT ON RH.RaterTypeID = RT.RaterTypeID
        WHERE RH.RateePersonelCode = @code AND RH.Status = 'Completed' AND RH.CycleID = (SELECT TOP 1 CycleID FROM dbo.EvaluationCycles WHERE IsActive = 1) AND RI.NumericAnswer > 0
        GROUP BY C.CompetencyName, LTRIM(RTRIM(REPLACE(REPLACE(Q.QuestionText, CHAR(13), ''), CHAR(10), ''))), RT.RaterTypeCode, E.Yaka
    ),
    RateeFlagsQ AS (
        SELECT CASE WHEN COUNT(*) > 0 THEN 1 ELSE 0 END AS HasSub
        FROM RaterAssignments RA JOIN RaterTypes RT ON RA.RaterTypeID = RT.RaterTypeID 
        WHERE RA.RateePersonelCode = @code AND RT.RaterTypeCode = 'Subordinate'
    ),
    WeightLogicQ AS (
        SELECT B.*,
            CASE 
                WHEN B.Yaka = 'Mavi' AND B.RaterTypeCode LIKE 'Manager%' THEN 50.0
                WHEN B.Yaka = 'Beyaz' AND B.RaterTypeCode LIKE 'Manager%' THEN CASE WHEN (SELECT HasSub FROM RateeFlagsQ) = 1 THEN 25.0 ELSE 50.0 END
                WHEN B.RaterTypeCode IN ('Subordinate', 'Peer', 'JointWorker') THEN 25.0
                ELSE 0.0
            END AS Weight
        FROM BaseQuestions B
    ),
    MainQ AS (
        SELECT CompetencyName, QuestionText,
            CAST(ISNULL(MAX(CASE WHEN RaterTypeCode = 'Self' THEN GrpAvg END), 0) AS DECIMAL(5,2)) AS SelfScore,
            CAST(ISNULL(MAX(CASE WHEN RaterTypeCode = 'Manager1' THEN GrpAvg END), 0) AS DECIMAL(5,2)) AS ManagerScore,
            CAST(ISNULL(SUM(CASE WHEN RaterTypeCode <> 'Self' THEN GrpAvg * Weight END) / NULLIF(SUM(CASE WHEN RaterTypeCode <> 'Self' THEN Weight END), 0), 0) AS DECIMAL(5,2)) AS WeightedScore
        FROM WeightLogicQ
        GROUP BY CompetencyName, QuestionText
    ),
    TitleAvgQ AS (
        SELECT C.CompetencyName, 
               LTRIM(RTRIM(REPLACE(REPLACE(Q.QuestionText, CHAR(13), ''), CHAR(10), ''))) AS QuestionText, 
               AVG(CAST(RI.NumericAnswer AS FLOAT)) AS CompanyTitleAvg
        FROM ResponseItems RI
        JOIN ResponseHeaders RH ON RI.ResponseHeaderID = RH.ResponseHeaderID
        JOIN Employees_Tablo E ON RH.RateePersonelCode = E.PersonelCode
        JOIN Questions Q ON RI.QuestionID = Q.QuestionID
        JOIN Competencies C ON Q.CompetencyID = C.CompetencyID
        WHERE RH.Status = 'Completed' AND RH.CycleID = (SELECT TOP 1 CycleID FROM dbo.EvaluationCycles WHERE IsActive = 1) AND RI.NumericAnswer > 0 
          AND E.Unvan = @title
        GROUP BY C.CompetencyName, LTRIM(RTRIM(REPLACE(REPLACE(Q.QuestionText, CHAR(13), ''), CHAR(10), '')))
    )
    SELECT M.CompetencyName, M.QuestionText, M.SelfScore, M.ManagerScore, M.WeightedScore,
           CAST(ISNULL(T.CompanyTitleAvg, 0) AS DECIMAL(5,2)) AS CompanyTitleAvg
    FROM MainQ M
    LEFT JOIN TitleAvgQ T ON M.CompetencyName = T.CompetencyName AND M.QuestionText = T.QuestionText
    ORDER BY M.CompetencyName, M.QuestionText;

    -- 3. GELİŞİM PLANI (EĞİTİMLER)
    WITH BaseAveragesT AS (
        SELECT C.CompetencyName, RT.RaterTypeCode, E.Yaka, AVG(CAST(RI.NumericAnswer AS FLOAT)) AS GrpAvg
        FROM ResponseItems RI
        JOIN ResponseHeaders RH ON RI.ResponseHeaderID = RH.ResponseHeaderID
        JOIN Employees_Tablo E ON RH.RateePersonelCode = E.PersonelCode
        JOIN Questions Q ON RI.QuestionID = Q.QuestionID
        JOIN Competencies C ON Q.CompetencyID = C.CompetencyID
        JOIN RaterTypes RT ON RH.RaterTypeID = RT.RaterTypeID
        WHERE RH.RateePersonelCode = @code AND RH.Status = 'Completed' AND RH.CycleID = (SELECT TOP 1 CycleID FROM dbo.EvaluationCycles WHERE IsActive = 1) AND RI.NumericAnswer > 0
        GROUP BY C.CompetencyName, RT.RaterTypeCode, E.Yaka
    ),
    RateeFlagsT AS (
        SELECT CASE WHEN COUNT(*) > 0 THEN 1 ELSE 0 END AS HasSub
        FROM RaterAssignments RA JOIN RaterTypes RT ON RA.RaterTypeID = RT.RaterTypeID 
        WHERE RA.RateePersonelCode = @code AND RT.RaterTypeCode = 'Subordinate'
    ),
    WeightLogicT AS (
        SELECT B.*,
            CASE 
                WHEN B.Yaka = 'Mavi' AND B.RaterTypeCode LIKE 'Manager%' THEN 50.0
                WHEN B.Yaka = 'Beyaz' AND B.RaterTypeCode LIKE 'Manager%' THEN CASE WHEN (SELECT HasSub FROM RateeFlagsT) = 1 THEN 25.0 ELSE 50.0 END
                WHEN B.RaterTypeCode IN ('Subordinate', 'Peer', 'JointWorker') THEN 25.0
                ELSE 0.0
            END AS Weight
        FROM BaseAveragesT B
    ),
    FinalScoresT AS (
        SELECT CompetencyName,
            CAST(ISNULL(SUM(CASE WHEN RaterTypeCode <> 'Self' THEN GrpAvg * Weight END) / NULLIF(SUM(CASE WHEN RaterTypeCode <> 'Self' THEN Weight END), 0), 0) AS DECIMAL(5,2)) AS WeightedScore
        FROM WeightLogicT GROUP BY CompetencyName
    )
    SELECT F.CompetencyName, F.WeightedScore,
        CASE WHEN F.WeightedScore <= 2.99 THEN 'Düşük' WHEN F.WeightedScore <= 4.00 THEN 'Orta' ELSE 'Yüksek' END AS Seviye,
        ISNULL(T.TrainingName, 'Gelişim planı atanmamış.') AS TrainingName,
        ISNULL(T.TrainingDescription, 'Detay bulunmuyor.') AS TrainingDescription,
        ISNULL(T.TrainingType, '') AS TrainingType,
        ISNULL(T.TrainingLink, '') AS TrainingLink 
    FROM FinalScoresT F
    LEFT JOIN Trainings T ON F.CompetencyName = T.CompetencyName 
        AND T.Level = (CASE WHEN F.WeightedScore <= 2.99 THEN 'Düşük' WHEN F.WeightedScore <= 4.00 THEN 'Orta' ELSE 'Yüksek' END);

    -- 4. DONUT GRAFİĞİ (AĞIRLIKLAR) - HATA DÜZELTİLDİ
    WITH RateeDataD AS (
        SELECT Yaka, (SELECT CASE WHEN COUNT(*) > 0 THEN 1 ELSE 0 END FROM RaterAssignments RA2 JOIN RaterTypes RT2 ON RA2.RaterTypeID = RT2.RaterTypeID WHERE RA2.RateePersonelCode = @code AND RT2.RaterTypeCode = 'Subordinate') AS HasSub
        FROM Employees_Tablo WHERE PersonelCode = @code
    ),
    BaseWeightsD AS (
        SELECT RT.Description AS Rol, COUNT(DISTINCT RA.RaterPersonelCode) AS KisiSayisi,
            CASE 
                WHEN MAX(D.Yaka) = 'Mavi' AND RT.RaterTypeCode LIKE 'Manager%' THEN 50.0
                WHEN MAX(D.Yaka) = 'Beyaz' AND RT.RaterTypeCode LIKE 'Manager%' THEN CASE WHEN MAX(D.HasSub) = 1 THEN 25.0 ELSE 50.0 END
                WHEN RT.RaterTypeCode IN ('Subordinate', 'Peer', 'JointWorker') THEN 25.0
                ELSE 0.0
            END AS RawWeight
        FROM RaterAssignments RA
        JOIN RaterTypes RT ON RA.RaterTypeID = RT.RaterTypeID
        CROSS JOIN RateeDataD D
        WHERE RA.RateePersonelCode = @code AND RT.RaterTypeCode <> 'Self'
          AND EXISTS (SELECT 1 FROM ResponseHeaders RH WHERE RH.RateePersonelCode = RA.RateePersonelCode AND RH.RaterPersonelCode = RA.RaterPersonelCode AND RH.Status = 'Completed' AND RH.CycleID = (SELECT TOP 1 CycleID FROM dbo.EvaluationCycles WHERE IsActive = 1))
        GROUP BY RT.Description, RT.RaterTypeCode
    ),
    TotalWeightD AS (SELECT SUM(RawWeight) as TotalW FROM BaseWeightsD),
    GroupedWeightsD AS (
        SELECT 
            CASE 
                WHEN Rol LIKE '%Ekip Arkadaşı%' OR Rol LIKE '%Ekip Arkadaşları%' THEN 'Ekip Arkadaşları'
                WHEN Rol LIKE '%Ortak İş%' THEN 'Ortak İş Yürütülenler'
                ELSE Rol 
            END AS Rol, 
            SUM(KisiSayisi) AS KisiSayisi, 
            SUM(RawWeight) AS TotalGroupWeight
        FROM BaseWeightsD 
        WHERE RawWeight > 0
        GROUP BY 
            CASE 
                WHEN Rol LIKE '%Ekip Arkadaşı%' OR Rol LIKE '%Ekip Arkadaşları%' THEN 'Ekip Arkadaşları'
                WHEN Rol LIKE '%Ortak İş%' THEN 'Ortak İş Yürütülenler'
                ELSE Rol 
            END
    )
    SELECT Rol, KisiSayisi, 
           CAST(ROUND((TotalGroupWeight * 100.0) / NULLIF((SELECT TotalW FROM TotalWeightD), 0), 0) AS INT) AS EtkiYuzdesi
    FROM GroupedWeightsD;
";

					using (SqlCommand cmd = new SqlCommand(sql, conn))
					{
						cmd.CommandTimeout = 120;
						cmd.Parameters.AddWithValue("@code", sicilNo);
						cmd.Parameters.AddWithValue("@title", title);
						using (SqlDataAdapter da = new SqlDataAdapter(cmd))
						{
							DataSet ds = new DataSet();
							da.Fill(ds);

							if (ds.Tables.Count == 4)
							{
								_dtAnaYetkinlikler = ds.Tables[0];
								_dtSorular = ds.Tables[1];
								_dtEgitimler = ds.Tables[2];
								_dtAgirliklar = ds.Tables[3];
							}
						}
					}

					chartRadar.Series.Clear();
					chartGap.Series.Clear();
					chartDonut.Series.Clear();

					// RADAR GRAFİĞİ AYARLARI
					Series sGeneral = new Series("Genel") { ChartType = SeriesChartType.Radar, Color = Color.FromArgb(46, 204, 113) };
					sGeneral.BorderWidth = 4;
					sGeneral.BorderDashStyle = ChartDashStyle.Dash;
					sGeneral["RadarDrawingStyle"] = "Line";
					sGeneral.MarkerStyle = MarkerStyle.Circle;
					sGeneral.MarkerSize = 10;
					sGeneral.MarkerColor = Color.FromArgb(46, 204, 113);

					Series sManager = new Series("Üst") { ChartType = SeriesChartType.Radar, Color = Color.FromArgb(200, 231, 76, 60) };
					sManager.BorderWidth = 3;
					sManager["RadarDrawingStyle"] = "Line";
					sManager.MarkerStyle = MarkerStyle.Square;
					sManager.MarkerSize = 7;
					sManager.MarkerColor = Color.FromArgb(231, 76, 60);

					Series sSelf = new Series("Kendi") { ChartType = SeriesChartType.Radar, Color = Color.FromArgb(200, 52, 152, 219) };
					sSelf.BorderWidth = 3;
					sSelf["RadarDrawingStyle"] = "Line";
					sSelf.MarkerStyle = MarkerStyle.Diamond;
					sSelf.MarkerSize = 8;
					sSelf.MarkerColor = Color.FromArgb(52, 152, 219);

					chartRadar.Series.Add(sGeneral);
					chartRadar.Series.Add(sManager);
					chartRadar.Series.Add(sSelf);

					double sumSelf = 0;
					double sumWeighted = 0;
					int rowCount = 0;

					foreach (DataRow dr in _dtAnaYetkinlikler.Rows)
					{
						string comp = dr["CompetencyName"].ToString();

						string displayComp = comp;
						if (comp.Length > 18)
						{
							int spaceIndex = comp.IndexOf(' ', 12);
							if (spaceIndex > 0) displayComp = comp.Substring(0, spaceIndex) + "\n" + comp.Substring(spaceIndex + 1);
						}

						double self = Convert.ToDouble(dr["SelfScore"]);
						double mgr = Convert.ToDouble(dr["ManagerScore"]);
						double wScore = Convert.ToDouble(dr["WeightedScore"]);

						sGeneral.Points.AddXY(displayComp, wScore);
						sManager.Points.AddXY(displayComp, mgr);
						sSelf.Points.AddXY(displayComp, self);

						sumSelf += self;
						sumWeighted += wScore;
						rowCount++;
					}

					if (rowCount > 0)
					{
						_overallScore = sumWeighted / rowCount;
						double avgSelf = sumSelf / rowCount;
						double avgGeneral = sumWeighted / rowCount;

						Series sSummary = new Series("Ozet") { ChartType = SeriesChartType.Bar };
						sSummary["PointWidth"] = "0.5";
						sSummary.IsValueShownAsLabel = true;
						sSummary.Font = new Font("Segoe UI", 12, FontStyle.Bold);
						sSummary.LabelForeColor = Color.Black;
						sSummary.IsVisibleInLegend = false;

						int idxGen = sSummary.Points.AddXY("Genel Ortalama", avgGeneral);
						sSummary.Points[idxGen].Color = Color.FromArgb(0, 174, 239);
						sSummary.Points[idxGen].Label = avgGeneral.ToString("0.0");

						int idxSelf = sSummary.Points.AddXY("Siz", avgSelf);
						sSummary.Points[idxSelf].Color = Color.FromArgb(238, 50, 36);
						sSummary.Points[idxSelf].Label = avgSelf.ToString("0.0");

						chartGap.Series.Add(sSummary);

						chartGap.ChartAreas[0].AxisX.MajorGrid.Enabled = false;
						chartGap.ChartAreas[0].AxisY.MajorGrid.LineColor = Color.LightGray;
						chartGap.ChartAreas[0].AxisY.MajorGrid.LineDashStyle = ChartDashStyle.Dash;
						chartGap.ChartAreas[0].AxisX.LabelStyle.Font = new Font("Segoe UI", 11, FontStyle.Bold);
						chartGap.ChartAreas[0].AxisX.Interval = 1;
						chartGap.ChartAreas[0].AxisY.Minimum = 0;
						chartGap.ChartAreas[0].AxisY.Maximum = 5;
						chartGap.ChartAreas[0].AxisY.Interval = 1;

						chartGap.Titles.Clear();
						chartGap.Titles.Add(new Title("FARK SKORU (GENEL ORTALAMA)", Docking.Top, new Font("Segoe UI", 14, FontStyle.Bold), Color.Black));
					}

					// ====================================================================
					// DONUT CHART (HALKA GRAFİĞİ) DÜZELTİLMİŞ KISMI
					// ====================================================================
					Series sDonut = new Series("Etki") { ChartType = SeriesChartType.Doughnut };
					sDonut["PieLabelStyle"] = "Outside";
					sDonut["DoughnutRadius"] = "40";

					Color[] palette = new Color[] { Color.FromArgb(41, 128, 185), Color.FromArgb(46, 204, 113), Color.FromArgb(241, 196, 15), Color.FromArgb(155, 89, 182), Color.Gray };

					// --- YÜZDELİK %100 TAMAMLAMA GARANTİSİ ---
					var donutLabels = new List<string>();
					var donutValues = new List<double>();
					double totalEtki = 0;

					foreach (DataRow r in _dtAgirliklar.Rows)
					{
						string rol = r["Rol"].ToString();
						if (rol == "Kendi") continue;

						double etki = r["EtkiYuzdesi"] != DBNull.Value ? Convert.ToDouble(r["EtkiYuzdesi"]) : 0;
						if (etki > 0)
						{
							donutLabels.Add(rol);
							donutValues.Add(etki);
							totalEtki += etki;
						}
					}

					// SQL'den gelen küsuratlı değerler (örn: 33+33+33=99) nedeniyle grafiğin bozulmasını engeller
					// Toplam 100 değilse, eksik/fazla olan o puanı en büyük paya sahip gruba ekler/çıkarır.
					if (donutValues.Count > 0 && totalEtki > 0 && totalEtki != 100)
					{
						double diff = 100 - totalEtki;
						double maxVal = donutValues.Max();
						int maxIndex = donutValues.IndexOf(maxVal);
						donutValues[maxIndex] += diff;
					}

					// Grafiği düzeltilmiş net verilerle çiz
					int colorIndex = 0;
					for (int i = 0; i < donutLabels.Count; i++)
					{
						if (donutValues[i] <= 0) continue; // 0 veya eksi olanları çizme

						int ptIdx = sDonut.Points.AddXY(donutLabels[i], donutValues[i]);
						sDonut.Points[ptIdx].Label = $"{donutLabels[i]}\n%{donutValues[i]}";
						sDonut.Points[ptIdx].Color = palette[colorIndex % palette.Length];
						sDonut.Points[ptIdx].Font = new Font("Segoe UI", 10, FontStyle.Bold);
						colorIndex++;
					}

					chartDonut.Series.Add(sDonut);

					StyleRadarChart(chartRadar);
				}
				catch (Exception ex)
				{
					MessageBox.Show("Hata: " + ex.Message);
				}
			}
		}

		private static string GetDepartmentName(SqlConnection openConn, string personelCode)
		{
			if (openConn == null) return "";
			if (openConn.State != ConnectionState.Open) return "";
			if (string.IsNullOrWhiteSpace(personelCode)) return "";

			try
			{
				string q = @"
SELECT TOP 1
    ISNULL(NULLIF(LTRIM(RTRIM(E.Birim)), ''), '')
FROM Employees_Tablo E
WHERE E.PersonelCode = @code;";

				using (SqlCommand cmd = new SqlCommand(q, openConn))
				{
					cmd.Parameters.AddWithValue("@code", personelCode);
					object o = cmd.ExecuteScalar();
					string birim = (o == null || o == DBNull.Value) ? "" : (o.ToString() ?? "");
					birim = birim.Trim();
					if (!string.IsNullOrWhiteSpace(birim)) return birim;
				}
			}
			catch { }

			try
			{
				string q2 = @"
SELECT TOP 1
    ISNULL(d.DepartmentName, '')
FROM Employees e
LEFT JOIN Departments d ON e.DepartmentID = d.DepartmentID
WHERE e.PersonelCode = @code;";

				using (SqlCommand cmd2 = new SqlCommand(q2, openConn))
				{
					cmd2.Parameters.AddWithValue("@code", personelCode);
					object o2 = cmd2.ExecuteScalar();
					return (o2 == null || o2 == DBNull.Value) ? "" : (o2.ToString() ?? "").Trim();
				}
			}
			catch { return ""; }
		}

		private void StyleGapChart(Chart c)
		{
			c.ChartAreas[0].AxisX.Interval = 1;
			c.ChartAreas[0].AxisX.MajorGrid.Enabled = false;
			c.ChartAreas[0].AxisX.LabelStyle.Font = new Font("Segoe UI", 9);
			c.ChartAreas[0].AxisY.MajorGrid.LineColor = Color.LightGray;
			c.ChartAreas[0].AxisY.MajorGrid.LineDashStyle = ChartDashStyle.Dash;
			c.ChartAreas[0].AxisY.LabelStyle.Format = "0.0";
			c.ChartAreas[0].AxisY.Crossing = 0;
			c.ChartAreas[0].AxisY.MajorGrid.Enabled = true;
		}

		private void StyleRadarChart(Chart chart)
		{
			chart.BackColor = Color.Transparent;
			chart.ChartAreas[0].BackColor = Color.Transparent;
			chart.ChartAreas[0].AxisY.LabelStyle.Enabled = false;
			chart.ChartAreas[0].AxisY.MajorGrid.LineColor = Color.LightGray;
			chart.ChartAreas[0].AxisX.MajorGrid.LineColor = Color.LightGray;

			chart.ChartAreas[0].Position.Auto = true;
			chart.ChartAreas[0].AxisX.LabelStyle.Font = new Font("Segoe UI", 10, FontStyle.Regular);

			chart.ChartAreas[0].AxisY.Minimum = 0;
			chart.ChartAreas[0].AxisY.Maximum = 5;
			chart.ChartAreas[0].AxisY.Interval = 1;

			if (chart.Series.Count >= 3)
			{
				chart.Series["Genel"].MarkerStyle = MarkerStyle.Circle; chart.Series["Genel"].MarkerSize = 6;
				chart.Series["Üst"].MarkerStyle = MarkerStyle.Circle; chart.Series["Üst"].MarkerSize = 6;
				chart.Series["Kendi"].MarkerStyle = MarkerStyle.Circle; chart.Series["Kendi"].MarkerSize = 8;
			}
		}

		private void SetupChart(Chart chart, SeriesChartType type)
		{
			chart.ChartAreas.Clear();
			ChartArea area = new ChartArea { BackColor = Color.Transparent };
			chart.ChartAreas.Add(area);
			chart.Legends.Clear();
			chart.Legends.Add(new Legend { Docking = Docking.Bottom, Alignment = StringAlignment.Center, BackColor = Color.Transparent, Font = new Font("Segoe UI", 10, FontStyle.Regular) });
		}

		private async void BtnPdf_Click(object sender, EventArgs e)
		{
			if (_dtAnaYetkinlikler == null || _dtAnaYetkinlikler.Rows.Count == 0)
			{
				MessageBox.Show("Lütfen önce raporu oluşturulacak personeli seçin.");
				return;
			}

			SaveFileDialog sfd = new SaveFileDialog { Filter = "PDF|*.pdf", FileName = $"{_selectedPersonName}_Rapor.pdf" };

			if (sfd.ShowDialog() == DialogResult.OK)
			{
				try
				{
					btnCreateReport.Enabled = false;
					btnCreateReport.Text = "⏳ HAZIRLANIYOR...";

					string titleDeptLine = TitleDepartmentParser.FormatTitleDepartment(_selectedPersonTitle, _selectedPersonDepartment);
					string coverDepartmentLine = string.IsNullOrWhiteSpace(_selectedPersonDepartment) ? "" : _selectedPersonDepartment.Trim();
					string coverTitleLine = string.IsNullOrWhiteSpace(_selectedPersonTitle) ? "" : $"Unvan: {_selectedPersonTitle}".Trim();
					string coverDeptAndTitle = string.Join("\n", new[] { coverDepartmentLine, coverTitleLine }.Where(s => !string.IsNullOrWhiteSpace(s)));

					var parsed = TitleDepartmentParser.ParseTitleDepartmentLine(titleDeptLine);
					string unvan = parsed.Title;
					string birim = parsed.Department;

					var compNames = _dtAnaYetkinlikler.AsEnumerable()
					.Select(r => r.Table.Columns.Contains("CompetencyName") ? (r["CompetencyName"]?.ToString() ?? "") : "")
					.Select(s => (s ?? "").Trim())
					.Where(s => !string.IsNullOrWhiteSpace(s))
					.Distinct()
					.ToList();

					// =========================================================================
					// BURASI TAMAMEN DEĞİŞTİRİLDİ: OLLAMA/QWEN SİLİNDİ, GEMINI EKLENDİ
					// =========================================================================
					string apiKey = WindowsFormsApp1.AI.Core.AiConfig.Get("OPENROUTER_API_KEY", "");
					var scoring = new PerformanceScoringService();

					// 1. GEMINI İLE AĞIRLIKLARI HESAPLA
					var geminiWeightService = new WindowsFormsApp1.AI.GeminiWeightService(apiKey);
					var weights = await geminiWeightService.CalculateWeightsAsync(birim, compNames);

					// 2. NİHAİ PUANI HESAPLA
					double finalScore5 = scoring.ComputeFinalScore5(_dtAnaYetkinlikler, weights, _overallScore);

					// 3. GEMINI İLE RAPOR YORUMUNU OLUŞTUR
					var geminiNarrativeService = new WindowsFormsApp1.AI.GeminiNarrativeService(apiKey);
					// isEmployeeView=false → İK/Yönetim Danışmanı dili, yöneticiye 3. tekil şahısla rapor
					string aiCommentary = await geminiNarrativeService.GenerateNarrativeAsync(
						_selectedPersonTitle, _selectedPersonDepartment,
						_dtAnaYetkinlikler, _dtSorular, finalScore5,
						isEmployeeView: false,
						employeeName: _selectedPersonName);
					// =========================================================================

					PdfReportHelper.CreateReport(
					sfd.FileName,
					_selectedPersonName,
					_selectedPersonTitle,
					coverDeptAndTitle,
					chartGap,
					chartDonut,
					_dtAnaYetkinlikler,
					_dtSorular,
					_dtEgitimler,
					_dtAgirliklar,
					finalScore5,
					aiCommentary,
					isEmployeeView: false   // yönetici görüntülüyor → 3. tekil şahıs
					);

					MessageBox.Show("Rapor başarıyla oluşturuldu!");
					System.Diagnostics.Process.Start(sfd.FileName);
				}
				catch (Exception ex) { MessageBox.Show("Hata: " + ex.Message); }
				finally
				{
					btnCreateReport.Enabled = true;
					btnCreateReport.Text = "📄 PDF OLUŞTUR";
				}
			}
		}

		private void ReportForm_Load(object sender, EventArgs e) { }

		private void ReportForm_Load_1(object sender, EventArgs e)
		{

		}
	}
}