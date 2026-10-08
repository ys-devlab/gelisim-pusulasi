using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows.Forms.DataVisualization.Charting;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;

using PDF = iTextSharp.text;
using PDFIO = iTextSharp.text.pdf;
using PDFDraw = iTextSharp.text.pdf.draw;

namespace WindowsFormsApp1
{
	public class PdfReportHelper
	{
		// --- TEMEL RENKLER ---
		private static PDF.BaseColor clrPrimary = new PDF.BaseColor(41, 128, 185);
		private static PDF.BaseColor clrTextDark = new PDF.BaseColor(40, 40, 40);
		private static PDF.BaseColor clrLightGray = new PDF.BaseColor(240, 240, 240);
		private static PDF.BaseColor clrBoxBg = new PDF.BaseColor(252, 253, 255);
		private static PDF.BaseColor clrBoxBorder = new PDF.BaseColor(220, 220, 220);

		private static PDF.BaseColor clrGenel = new PDF.BaseColor(46, 204, 113);
		private static PDF.BaseColor clrSelf = new PDF.BaseColor(52, 152, 219);

		// --- YENİ SOFT RENKLER VE ÖZEL KART RENKLERİ ---
		private static PDF.BaseColor clrSoftGreen = new PDF.BaseColor(212, 237, 218);
		private static PDF.BaseColor clrDarkGreenText = new PDF.BaseColor(21, 87, 36);

		private static PDF.BaseColor clrSoftOrange = new PDF.BaseColor(255, 228, 205);
		private static PDF.BaseColor clrDarkOrangeText = new PDF.BaseColor(211, 84, 0);
		private static PDF.BaseColor clrSoftRed = new PDF.BaseColor(248, 215, 218);
		private static PDF.BaseColor clrDarkRedText = new PDF.BaseColor(114, 28, 36);

		// --- FONTLAR ---
		private static string fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");
		private static PDFIO.BaseFont baseFont = PDFIO.BaseFont.CreateFont(fontPath, PDFIO.BaseFont.IDENTITY_H, PDFIO.BaseFont.EMBEDDED);

		private static PDF.Font fTitle = new PDF.Font(baseFont, 22, PDF.Font.BOLD, clrPrimary);
		private static PDF.Font fHeader1 = new PDF.Font(baseFont, 11, PDF.Font.BOLD, clrPrimary);
		private static PDF.Font fNormal = new PDF.Font(baseFont, 9, PDF.Font.NORMAL, clrTextDark);
		private static PDF.Font fBold = new PDF.Font(baseFont, 9, PDF.Font.BOLD, clrTextDark);
		private static PDF.Font fSmall = new PDF.Font(baseFont, 8, PDF.Font.ITALIC, PDF.BaseColor.GRAY);
		private static PDF.Font fBigNumber = new PDF.Font(baseFont, 12, PDF.Font.BOLD, clrTextDark);
		private static PDF.Font fIconText = new PDF.Font(baseFont, 9, PDF.Font.BOLD, PDF.BaseColor.WHITE);

		private static PDF.Font fHeatmapNormal = new PDF.Font(baseFont, 8, PDF.Font.NORMAL, clrTextDark);
		private static PDF.Font fHeatmapBold = new PDF.Font(baseFont, 8, PDF.Font.BOLD, clrTextDark);

		private static CultureInfo culture = new CultureInfo("tr-TR");

		/// <param name="isEmployeeView">
		/// true  → Raporu çalışanın kendisi görüntülüyor: 2. tekil şahıs ("siz", "güçlü yönleriniz").
		/// false → Raporu yönetici görüntülüyor: 3. tekil şahıs ("çalışanınız", "güçlü yönleri").
		/// </param>
		public static void CreateReport(
			string filePath,
			string pName,
			string pTitle,
			string pTitleDepartmentLine,
			Chart gap,
			Chart donut,
			DataTable dtAnaYetkinlikler,
			DataTable dtSorular,
			DataTable dtEgitimler,
			DataTable dtAgirliklar,
			double overallScore,
			string heatmapAiCommentary,
			bool isEmployeeView = false)
		{
			PDF.Document doc = new PDF.Document(PDF.PageSize.A4, 40, 40, 50, 50);

			try
			{
				PDFIO.PdfWriter writer = PDFIO.PdfWriter.GetInstance(doc, new FileStream(filePath, FileMode.Create));
				writer.PageEvent = new FooterEvent();
				doc.Open();

				DrawCover(doc, pName, pTitleDepartmentLine);
				doc.NewPage();

				DrawIntroductionAndEcosystem(doc, dtAgirliklar, donut, isEmployeeView);
				doc.NewPage();

				DrawOverview(doc, dtAnaYetkinlikler, pTitle, isEmployeeView);
				doc.NewPage();

				DrawHeatmap(doc, dtSorular, pTitle, isEmployeeView);
				doc.NewPage();

				double avgSelf = 0;
				if (dtAnaYetkinlikler.Rows.Count > 0 && dtAnaYetkinlikler.Columns.Contains("SelfScore"))
				{
					avgSelf = dtAnaYetkinlikler.AsEnumerable().Average(r => Convert.ToDouble(r["SelfScore"]));
				}
				DrawCompetencyAndAwareness(doc, gap, avgSelf, overallScore, dtAnaYetkinlikler, isEmployeeView);
				doc.NewPage();

				DrawHeatmapAICommentary(doc, heatmapAiCommentary, dtAnaYetkinlikler);
				doc.NewPage();

				DrawDevelopmentPlan(doc, dtEgitimler);
			}
			catch (Exception ex) { throw new Exception("PDF Hatası: " + ex.Message); }
			finally { if (doc.IsOpen()) doc.Close(); }
		}

		// ===================================================================================
		// YENİDEN YAZILAN KISIM: BAŞLIK VE MADDE İŞARETİ (BULLET POINT) YAKALAMA MANTIĞI EKLENDİ
		// ===================================================================================
		private static void DrawHeatmapAICommentary(PDF.Document doc, string text, DataTable dtAnaYetkinlikler = null)
		{
			if (string.IsNullOrWhiteSpace(text)) return;

			AddHeader(doc, "GENEL DEĞERLENDİRME");

			// Yapay zeka yorumunu destekleyen kompakt görsel özet (güçlü yönler / gelişim alanları + mini bar)
			DrawAIVisualSummary(doc, dtAnaYetkinlikler);

			string[] lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

			// Aynı bölüm başlığının (örn. "ŞİRKET İÇİ KONUMLANMA") birden fazla kez basılmasını engeller
			HashSet<string> renderedHeaders = new HashSet<string>();

			List<string> aiBasliklar = new List<string> {
				"BİRİMDEKİ ROLÜNÜZ VE BAŞARILARINIZ",
				"KRİTİK YETKİNLİK ANALİZİ",
				"ODAKLANILACAK GELİŞİM ALANLARI",
				"SİZİN İÇİN GELİŞİM FIRSATLARI",
				"GENEL PERFORMANS ÖZETİ",
				"ÖNE ÇIKAN GÜÇLÜ YÖNLER",
				"ŞİRKET İÇİ KONUMLANMA",
				"GELİŞİM YOL HARİTASI",
				"KARİYER YOLCULUĞUNUZDA YANINIZDAYIZ",
				"KARİYER YOLCULUĞUNDA YANINDAYIZ",
				"YÖNETİCİYE TAVSİYELER"
			};

			List<string[]> tableRows = new List<string[]>();
			PDF.Paragraph pendingHeader = null;

			Action flushPendingHeader = () => {
				if (pendingHeader != null) {
					doc.Add(pendingHeader);
					pendingHeader = null;
				}
			};

			Action renderTable = () => {
				if (tableRows.Count == 0) return;

				int colCount = tableRows[0].Length;
				PDFIO.PdfPTable table = new PDFIO.PdfPTable(colCount);
				table.WidthPercentage = 100;
				table.SpacingBefore = pendingHeader != null ? 0f : 10f;
				table.SpacingAfter = 15f;
				
				if (pendingHeader != null) {
					table.HeaderRows = 2;
					PDFIO.PdfPCell hCell = new PDFIO.PdfPCell();
					hCell.Colspan = colCount;
					hCell.Border = PDF.Rectangle.NO_BORDER;
					pendingHeader.SpacingBefore = 0f;
					hCell.AddElement(pendingHeader);
					hCell.PaddingTop = 15f;
					hCell.PaddingBottom = 5f;
					hCell.PaddingLeft = 0f;
					hCell.PaddingRight = 0f;
					table.AddCell(hCell);
					pendingHeader = null;
				} else {
					table.HeaderRows = 1;
				}
				table.KeepTogether = true; // Tablonun bölünmesini engeller

				for (int i = 0; i < tableRows.Count; i++)
				{
					bool isHeader = (i == 0);
					foreach (string cellText in tableRows[i])
					{
						PDF.Font fCell = isHeader ? fBold : fNormal;
						PDFIO.PdfPCell cell = new PDFIO.PdfPCell(new PDF.Phrase(cellText.Trim(), fCell));
						cell.Padding = 8f;
						cell.BackgroundColor = isHeader ? clrLightGray : PDF.BaseColor.WHITE;
						cell.BorderColor = PDF.BaseColor.LIGHT_GRAY;
						table.AddCell(cell);
					}
				}
				doc.Add(table);
				tableRows.Clear();
			};

			Func<string, string, PDF.Paragraph> createParagraphWithBoldColon = (lineText, prefix) => {
				PDF.Paragraph p = new PDF.Paragraph();
				p.Alignment = PDF.Element.ALIGN_JUSTIFIED;
				p.SetLeading(0, 1.4f);

				if (!string.IsNullOrEmpty(prefix))
				{
					p.Add(new PDF.Chunk(prefix, fNormal));
				}

				int colonIdx = lineText.IndexOf(':');
				if (colonIdx > 0 && colonIdx < lineText.Length - 1)
				{
					string boldPart = lineText.Substring(0, colonIdx + 1);
					string normalPart = lineText.Substring(colonIdx + 1);
					p.Add(new PDF.Chunk(boldPart, fBold));
					p.Add(new PDF.Chunk(normalPart, fNormal));
				}
				else
				{
					p.Add(new PDF.Chunk(lineText, fNormal));
				}
				return p;
			};

			foreach (string line in lines)
			{
				string cleanLine = line.Trim();
				if (string.IsNullOrWhiteSpace(cleanLine)) continue;

				if (cleanLine.StartsWith("[TABLO]"))
				{
					string rowText = cleanLine.Substring(7).Trim();
					string[] parts = rowText.Split('|');
					tableRows.Add(parts);
					continue;
				}

				renderTable();

				string headerMatch = aiBasliklar.FirstOrDefault(b => cleanLine.Contains(b));
				if (headerMatch != null)
				{
					// Aynı başlık daha önce basıldıysa tekrarını atla (ör. çift "ŞİRKET İÇİ KONUMLANMA")
					if (renderedHeaders.Contains(headerMatch)) continue;
					renderedHeaders.Add(headerMatch);

					flushPendingHeader();
					PDF.Font fAiHeader = new PDF.Font(baseFont, 10, PDF.Font.BOLD, clrPrimary);
					PDF.Paragraph pTitle = new PDF.Paragraph(cleanLine, fAiHeader);
					pTitle.SpacingBefore = 15f;
					pTitle.SpacingAfter = 5f;

					// iTextSharp'ta Paragraph için KeepWithNext özelliği bulunmamaktadır.
					// Bunun yerine başlığın sayfa sonunda tek başına kalıp bölünmesini engellemek için KeepTogether kullanılır.
					pTitle.KeepTogether = true;

					pendingHeader = pTitle;
				}
				else if (cleanLine.StartsWith("-"))
				{
					flushPendingHeader();
					string bulletText = cleanLine.Substring(1).Trim();
					PDF.Paragraph pBullet = createParagraphWithBoldColon(bulletText, "   • ");
					pBullet.SpacingAfter = 6f;
					doc.Add(pBullet);
				}
				else
				{
					flushPendingHeader();
					PDF.Paragraph pText = createParagraphWithBoldColon(cleanLine, "");
					pText.SpacingAfter = 10f;
					doc.Add(pText);
				}
			}

			renderTable();
			flushPendingHeader();
		}
		// ===================================================================================

		// Yapay zeka yorumunu görsel olarak destekleyen kompakt kart:
		// solda güçlü yönler (yeşil), sağda gelişim alanları (turuncu), her biri mini skor barıyla.
		private static void DrawAIVisualSummary(PDF.Document doc, DataTable dt)
		{
			if (dt == null || !dt.Columns.Contains("CompetencyName") || !dt.Columns.Contains("WeightedScore") || dt.Rows.Count == 0)
				return;

			var comps = dt.AsEnumerable()
				.Select(r => Tuple.Create((r["CompetencyName"]?.ToString() ?? "").Trim(), SafeDouble(r["WeightedScore"])))
				.Where(t => !string.IsNullOrWhiteSpace(t.Item1))
				.ToList();
			if (comps.Count == 0) return;

			var strengths = comps.OrderByDescending(c => c.Item2).Take(3).ToList();
			var devs      = comps.OrderBy(c => c.Item2).Take(3).ToList();

			PDFIO.PdfPTable card = new PDFIO.PdfPTable(2);
			card.WidthPercentage = 100;
			card.SetWidths(new float[] { 50, 50 });
			card.SpacingBefore = 6f;
			card.SpacingAfter = 16f;
			card.KeepTogether = true;

			card.AddCell(BuildVizColumn("▲  ÖNE ÇIKAN GÜÇLÜ YÖNLER", clrSoftGreen, clrDarkGreenText, strengths));
			card.AddCell(BuildVizColumn("▼  ODAKLANILACAK ALANLAR", clrSoftOrange, clrDarkOrangeText, devs));

			doc.Add(card);
		}

		private static PDFIO.PdfPCell BuildVizColumn(string title, PDF.BaseColor headBg, PDF.BaseColor headText, List<Tuple<string, double>> items)
		{
			PDFIO.PdfPTable inner = new PDFIO.PdfPTable(1);
			inner.WidthPercentage = 100;

			PDFIO.PdfPCell head = new PDFIO.PdfPCell(new PDF.Phrase(title, new PDF.Font(baseFont, 9, PDF.Font.BOLD, headText)));
			head.BackgroundColor = headBg;
			head.Border = PDF.Rectangle.NO_BORDER;
			head.Padding = 6f;
			head.HorizontalAlignment = PDF.Element.ALIGN_CENTER;
			inner.AddCell(head);

			foreach (var it in items)
			{
				PDFIO.PdfPCell nameCell = new PDFIO.PdfPCell(new PDF.Phrase(it.Item1, fBold));
				nameCell.Border = PDF.Rectangle.NO_BORDER;
				nameCell.PaddingTop = 6f; nameCell.PaddingBottom = 1f; nameCell.PaddingLeft = 6f; nameCell.PaddingRight = 6f;
				inner.AddCell(nameCell);

				PDFIO.PdfPTable barRow = new PDFIO.PdfPTable(2);
				barRow.SetWidths(new float[] { 72, 28 });
				barRow.WidthPercentage = 100;

				PDF.Image bar = CreateScoreBarImage(it.Item2);
				bar.ScaleToFit(120f, 14f);
				PDFIO.PdfPCell barCell = new PDFIO.PdfPCell(bar, false);
				barCell.Border = PDF.Rectangle.NO_BORDER;
				barCell.VerticalAlignment = PDF.Element.ALIGN_MIDDLE;
				barCell.PaddingLeft = 6f;
				barRow.AddCell(barCell);

				PDFIO.PdfPCell valCell = new PDFIO.PdfPCell(new PDF.Phrase(it.Item2.ToString("0.00", culture), fBold));
				valCell.Border = PDF.Rectangle.NO_BORDER;
				valCell.HorizontalAlignment = PDF.Element.ALIGN_RIGHT;
				valCell.VerticalAlignment = PDF.Element.ALIGN_MIDDLE;
				valCell.PaddingRight = 6f;
				barRow.AddCell(valCell);

				PDFIO.PdfPCell barWrap = new PDFIO.PdfPCell(barRow);
				barWrap.Border = PDF.Rectangle.NO_BORDER;
				barWrap.PaddingBottom = 4f;
				inner.AddCell(barWrap);
			}

			PDFIO.PdfPCell container = new PDFIO.PdfPCell(inner);
			container.Border = PDF.Rectangle.BOX;
			container.BorderColor = clrBoxBorder;
			container.BorderWidth = 1f;
			container.BackgroundColor = clrBoxBg;
			container.Padding = 4f;
			return container;
		}

		private static double SafeDouble(object o)
		{
			try { return o == null || o == DBNull.Value ? 0 : Convert.ToDouble(o); }
			catch { return 0; }
		}

		private static void DrawCover(PDF.Document doc, string name, string title)
		{
			doc.Add(new PDF.Paragraph("\n\n\n"));

			// ── Kurumsal logo — merkeze hizalı, ana başlığın hemen üstünde ──
			try
			{
				string logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "tusas_logo.png");
				if (File.Exists(logoPath))
				{
					PDF.Image logo = PDF.Image.GetInstance(logoPath);
					logo.ScaleToFit(215f, 90f);
					logo.Alignment = PDF.Image.ALIGN_CENTER;
					doc.Add(logo);
				}
			}
			catch { /* logo yüklenemezse kapak logosuz devam etsin */ }

			doc.Add(new PDF.Paragraph("\n\n\n"));

			// ── Ana başlık: GELİŞİM PUSULASI (kurumsal mavi, kalın, harf aralıklı) ──
			PDF.Chunk chTitle = new PDF.Chunk("GELİŞİM PUSULASI", new PDF.Font(baseFont, 34, PDF.Font.BOLD, clrPrimary));
			chTitle.SetCharacterSpacing(3f);
			PDF.Paragraph pTitle = new PDF.Paragraph(chTitle);
			pTitle.Alignment = PDF.Element.ALIGN_CENTER;
			doc.Add(pTitle);
			doc.Add(new PDF.Paragraph("\n"));

			// İnce, zarif ayraç
			PDFDraw.LineSeparator line = new PDFDraw.LineSeparator(1.2f, 32f, clrPrimary, PDF.Element.ALIGN_CENTER, -4);
			doc.Add(line);
			doc.Add(new PDF.Paragraph("\n\n"));

			// ── Alt başlık (rapor adı) — hafif, harf aralıklı, kurumsal ──
			PDF.Chunk chSub = new PDF.Chunk("BİREYSEL GERİ BİLDİRİM VE DEĞERLENDİRME RAPORU",
				new PDF.Font(baseFont, 11, PDF.Font.NORMAL, clrTextDark));
			chSub.SetCharacterSpacing(2f);
			PDF.Paragraph pReportTitle = new PDF.Paragraph(chSub);
			pReportTitle.Alignment = PDF.Element.ALIGN_CENTER;
			pReportTitle.SetLeading(0, 1.4f);
			doc.Add(pReportTitle);
			doc.Add(new PDF.Paragraph("\n\n\n\n\n"));

			// ── Değerlendirilen personel kartı ──
			PDFIO.PdfPTable t = new PDFIO.PdfPTable(1);
			t.WidthPercentage = 62;
			PDFIO.PdfPCell c = new PDFIO.PdfPCell();
			c.Border = PDF.Rectangle.TOP_BORDER | PDF.Rectangle.BOTTOM_BORDER;
			c.BorderColor = clrPrimary;
			c.BorderWidth = 1.5f;
			c.PaddingTop = 26f;
			c.PaddingBottom = 26f;
			c.BackgroundColor = new PDF.BaseColor(252, 253, 255);

			PDF.Chunk chLabel = new PDF.Chunk("DEĞERLENDİRİLEN", new PDF.Font(baseFont, 8, PDF.Font.NORMAL, PDF.BaseColor.GRAY));
			chLabel.SetCharacterSpacing(2.5f);
			c.AddElement(new PDF.Paragraph(chLabel) { Alignment = PDF.Element.ALIGN_CENTER });
			c.AddElement(new PDF.Paragraph("\n", new PDF.Font(baseFont, 4)));
			c.AddElement(new PDF.Paragraph(name.ToUpper(), new PDF.Font(baseFont, 17, PDF.Font.BOLD, clrPrimary)) { Alignment = PDF.Element.ALIGN_CENTER });
			if (!string.IsNullOrWhiteSpace(title))
				c.AddElement(new PDF.Paragraph(title, new PDF.Font(baseFont, 10, PDF.Font.NORMAL, clrTextDark)) { Alignment = PDF.Element.ALIGN_CENTER, SpacingBefore = 4f });
			t.AddCell(c);
			doc.Add(t);

			doc.Add(new PDF.Paragraph($"\n\n\n\nRapor Tarihi: {DateTime.Now:dd MMMM yyyy}", new PDF.Font(baseFont, 10, PDF.Font.NORMAL, PDF.BaseColor.GRAY)) { Alignment = PDF.Element.ALIGN_CENTER });
		}

		private static void DrawIntroductionAndEcosystem(PDF.Document doc, DataTable dtAgirliklar, Chart donutChart, bool isEmployeeView = false)
		{
			AddHeader(doc, "1. RAPOR HAKKINDA VE DEĞERLENDİRME BAREMİ");

			string introText = isEmployeeView
				?	// ── Çalışana doğrudan hitap (2. tekil şahıs) ─────────────────
					"Bu Bireysel Gelişim Raporu, profesyonel yolculuğunda sana rehberlik etmek ve sürekli gelişimini desteklemek amacıyla özel olarak hazırlanmıştır.\n" +
					"Raporun temel amacı; yetkinliklerinin ve iş ortamındaki yaklaşımlarının şeffaf bir şekilde ortaya konmasıdır. Bu rapor, kendi değerlendirmelerin ile birlikte, kurum içindeki pozisyonuna uygun olarak senin için tanımlanan değerlendiricilerin geri bildirimleri harmanlanarak oluşturulmuştur. Bu çalışma, bir performans notlandırması değil; aksine, güçlü yönlerini pekiştirmen ve potansiyelini en üst düzeye çıkarman için tasarlanmış kişisel bir yol haritasıdır.\n" +
					"Raporun sonunda yer alan 'Bireysel Gelişim Planı' bölümündeki bulguları dikkatle incelemen ve bu adımları yöneticinle iş birliği içinde hayata geçirmen, kariyer vizyonun ve kurum içindeki başarın açısından büyük önem taşımaktadır.\n\n"
				:	// ── Yöneticiye çalışan hakkında rapor (3. tekil şahıs) ─────────
					"Bu Bireysel Gelişim Raporu, çalışanınızın profesyonel yolculuğuna rehberlik etmek ve sürekli gelişimini desteklemek amacıyla hazırlanmıştır.\n" +
					"Raporun temel amacı; çalışanın yetkinliklerinin ve iş ortamındaki yaklaşımlarının şeffaf bir şekilde ortaya konmasıdır. Bu rapor, çalışanın kendi değerlendirmeleri ile birlikte, kurum içindeki pozisyonuna uygun olarak tanımlanan değerlendiricilerin geri bildirimleri harmanlanarak oluşturulmuştur. Bu çalışma, bir performans notlandırması değil; aksine, çalışanınızın güçlü yönlerini pekiştirmesi ve potansiyelini en üst düzeye çıkarması için tasarlanmış kişisel bir yol haritasıdır.\n" +
					"Raporun sonunda yer alan 'Bireysel Gelişim Planı' bölümündeki bulguları dikkatle incelemeniz ve bu adımları çalışanınızla iş birliği içinde hayata geçirmeniz, çalışanın kariyer vizyonu ve kurum içindeki başarısı açısından büyük önem taşımaktadır.\n\n";

			string introText2 =
				"Değerlendirme Skalası (1-5 Puan)\n\n" +
				"1 - Gelişim Alanı: Bu alanda desteklenmeye ve belirgin bir gelişime ihtiyaç duyulmaktadır.\n" +
				"2 - Beklentilerin Altında: Beklenen standartları tam olarak karşılamak için ek çaba ve odaklanma gerekmektedir.\n" +
				"3 - Beklentileri Karşılıyor: İlgili yetkinlik ve görev tanımlarından beklenen standartlar, eksiksiz ve istikrarlı bir şekilde yerine getirilmektedir.\n" +
				"4 - Beklentileri Aşıyor: Beklentilerin ötesine geçilerek, yüksek kalitede sonuçlar üretilmekte ve iş süreçlerine değer katılmaktadır.\n" +
				"5 - Rol Model: Bu alanda üstün bir performans sergilenmekte ve kurum genelinde tüm çalışanlara örnek teşkil edilmektedir.\n\n";

			PDF.Paragraph pIntro = new PDF.Paragraph(introText + introText2, fNormal);
			pIntro.Alignment = PDF.Element.ALIGN_JUSTIFIED;
			pIntro.SetLeading(0, 1.4f);
			doc.Add(pIntro);

			DrawSkalaBoxes(doc);

			doc.Add(new PDF.Paragraph("\n\n"));
			AddHeader(doc, "2. DEĞERLENDİREN EKOSİSTEMİ VE AĞIRLIKLAR");

			string ekoDesc = isEmployeeView
				? "Geri bildirim sürecinde, öz değerlendirme puanların kişisel farkındalığını yansıtmak amacıyla rapora dahil edilmiş olup, genel ortalama hesaplamasına etki etmemektedir. Sürece katılım sağlayan diğer değerlendirici grupların nihai puana etki oranları aşağıdaki grafikte özetlenmiştir.\n\n"
				: "Geri bildirim sürecinde, çalışanın öz değerlendirme puanları kişisel farkındalığı yansıtmak amacıyla rapora dahil edilmiş olup, genel ortalama hesaplamasına etki etmemektedir. Sürece katılım sağlayan diğer değerlendirici grupların nihai puana etki oranları aşağıdaki grafikte özetlenmiştir.\n\n";
			PDF.Paragraph pDesc = new PDF.Paragraph(ekoDesc, fNormal);
			pDesc.Alignment = PDF.Element.ALIGN_JUSTIFIED;
			doc.Add(pDesc);

			PDFIO.PdfPTable layout = new PDFIO.PdfPTable(2);
			layout.WidthPercentage = 100;
			layout.SetWidths(new float[] { 40, 60 });

			PDFIO.PdfPTable tableGrp = new PDFIO.PdfPTable(2);
			tableGrp.WidthPercentage = 100;
			tableGrp.HeaderRows = 1;
			AddGroupHeaderCell(tableGrp, "Değerlendirme Grubu", PDF.Element.ALIGN_LEFT);
			AddGroupHeaderCell(tableGrp, "Kişi Sayısı", PDF.Element.ALIGN_CENTER);

			// BİRLEŞTİRİLMİŞ İSİMLERE GÖRE TERCİH EDİLEN SIRALAMA
			string[] preferredOrder = { "1. Yönetici", "2. Yönetici", "Yönetici", "Ekip Arkadaşları", "Ortak İş Yürütülenler", "Astlar" };

			var sortedRows = dtAgirliklar.AsEnumerable()
				.OrderBy(r => {
					string rolName = r.Table.Columns.Contains("Rol") ? r["Rol"].ToString() : "";
					int index = Array.IndexOf(preferredOrder, rolName);
					return index == -1 ? 99 : index;
				}).ToList();

			foreach (var r in sortedRows)
			{
				string rolName = r.Table.Columns.Contains("Rol") ? r["Rol"].ToString() : "";
				int count = r.Table.Columns.Contains("KisiSayisi") ? Convert.ToInt32(r["KisiSayisi"]) : 0;
				AddGroupRow(tableGrp, rolName, count);
			}
			AddGroupRow(tableGrp, "Kendisi", 1);

			PDFIO.PdfPCell leftCell = new PDFIO.PdfPCell(tableGrp);
			leftCell.Border = PDF.Rectangle.NO_BORDER;
			leftCell.VerticalAlignment = PDF.Element.ALIGN_MIDDLE;
			layout.AddCell(leftCell);

			using (MemoryStream ms = new MemoryStream())
			{
				donutChart.SaveImage(ms, ChartImageFormat.Png);
				PDF.Image imgDonut = PDF.Image.GetInstance(ms.GetBuffer());
				imgDonut.ScalePercent(60f);
				imgDonut.Alignment = PDF.Element.ALIGN_CENTER;

				PDFIO.PdfPCell rightCell = new PDFIO.PdfPCell(imgDonut, true);
				rightCell.Border = PDF.Rectangle.NO_BORDER;
				rightCell.VerticalAlignment = PDF.Element.ALIGN_MIDDLE;
				layout.AddCell(rightCell);
			}

			doc.Add(layout);
		}

		private static void DrawOverview(PDF.Document doc, DataTable dt, string title, bool isEmployeeView = false)
		{
			AddHeader(doc, "3. ANA YETKİNLİKLER VE GENEL BAKIŞ");

			string descText = isEmployeeView
				? "Aşağıdaki tabloda, değerlendirildiğin temel yetkinliklerdeki genel performansın ve değerlendirici gruplarına göre puan kırılımları özetlenmektedir. Bu tablo, güçlü olduğun ve gelişime açık olan yetkinliklerini detaylı bir şekilde incelemene olanak sağlar.\n\n"
				: "Aşağıdaki tabloda, çalışanınızın temel yetkinliklerdeki genel performansı ve değerlendirici gruplarına göre puan kırılımları özetlenmektedir. Bu tablo, çalışanınızın güçlü olduğu ve gelişime açık olan yetkinliklerini detaylı bir şekilde incelemenize olanak sağlar.\n\n";

			PDF.Paragraph pDesc = new PDF.Paragraph(descText, fNormal);
			pDesc.Alignment = PDF.Element.ALIGN_JUSTIFIED;
			pDesc.SetLeading(0, 1.5f);
			doc.Add(pDesc);

			if (dt != null && dt.Rows.Count > 0)
			{
				PDFIO.PdfPTable table = new PDFIO.PdfPTable(5);
				table.WidthPercentage = 100;
				table.SetWidths(new float[] { 28, 28, 10, 14, 20 });
				table.SpacingBefore = 10f;
				table.HeaderRows = 1;

				string formattedTitle = culture.TextInfo.ToTitleCase(title.ToLower(culture));

				AddGrayHeader(table, "Yetkinlikler");
				AddGrayHeader(table, "");
				AddGrayHeader(table, "Siz");
				AddGrayHeader(table, "Genel Ortalama");
				AddGrayHeader(table, $"Şirket İçi\n{formattedTitle} Ortalaması");

				foreach (DataRow row in dt.Rows)
				{
					string compName = row.Table.Columns.Contains("CompetencyName") ? row["CompetencyName"].ToString() : "";
					AddCellBasic(table, compName, PDF.Element.ALIGN_LEFT);

					double selfScore = 0;
					if (row.Table.Columns.Contains("SelfScore") && row["SelfScore"] != DBNull.Value) selfScore = Convert.ToDouble(row["SelfScore"]);

					double wScore = 0;
					if (row.Table.Columns.Contains("WeightedScore") && row["WeightedScore"] != DBNull.Value) wScore = Convert.ToDouble(row["WeightedScore"]);

					double compTitleAvg = 0;
					if (row.Table.Columns.Contains("CompanyTitleAvg") && row["CompanyTitleAvg"] != DBNull.Value) compTitleAvg = Convert.ToDouble(row["CompanyTitleAvg"]);

					double sliderVal = wScore > 0 ? wScore : 1.0;

					PDF.Image imgBar = CreateScoreBarImage(sliderVal);
					PDFIO.PdfPCell cellBar = new PDFIO.PdfPCell(imgBar, true);
					cellBar.BorderColor = PDF.BaseColor.LIGHT_GRAY;
					cellBar.VerticalAlignment = PDF.Element.ALIGN_MIDDLE;
					cellBar.Padding = 6f;
					table.AddCell(cellBar);

					AddCellBasic(table, selfScore > 0 ? selfScore.ToString("0.0", culture) : "-", PDF.Element.ALIGN_CENTER);
					AddCellBasic(table, wScore > 0 ? wScore.ToString("0.0", culture) : "-", PDF.Element.ALIGN_CENTER);
					AddCellBasic(table, compTitleAvg > 0 ? compTitleAvg.ToString("0.0", culture) : "-", PDF.Element.ALIGN_CENTER);
				}
				doc.Add(table);

				doc.Add(new PDF.Paragraph("\n"));

				PDF.Paragraph pBelowDesc1 = new PDF.Paragraph($"Şirket İçi {formattedTitle} Ortalaması", fBold);
				string desc1Text = isEmployeeView
					? "Şirketimizde seninle aynı unvanda görev yapan tüm çalışanların ilgili yetkinlikten aldığı puanların genel ortalamasıdır. Bu değer, yetkinlik seviyeni aynı unvanı paylaşan diğer çalışanların genel durumuyla karşılaştırabilmeni sağlar.\n\n"
					: "Şirketimizde çalışanınızla aynı unvanda görev yapan tüm personelin ilgili yetkinlikten aldığı puanların genel ortalamasıdır. Bu değer, çalışanın yetkinlik seviyesini aynı unvanı paylaşan diğer çalışanların genel durumuyla karşılaştırabilmenizi sağlar.\n\n";
				PDF.Paragraph pBelowDesc1Text = new PDF.Paragraph(desc1Text, fNormal);
				pBelowDesc1.Alignment = PDF.Element.ALIGN_JUSTIFIED;
				pBelowDesc1Text.Alignment = PDF.Element.ALIGN_JUSTIFIED;

				PDF.Paragraph pBelowDesc2 = new PDF.Paragraph("Genel Ortalama", fBold);
				string desc2Text = isEmployeeView
					? "Kendi değerlendirmen (Öz Değerlendirme) hariç tutularak, hakkında yapılan tüm değerlendirmelerin ağırlıklı ortalamasıdır. Bu değer, iş ortamındaki yetkinliklerinin ve davranışlarının seni değerlendiren kişiler tarafından nasıl gözlemlendiğini yansıtır.\n\n"
					: "Çalışanın kendi değerlendirmesi (Öz Değerlendirme) hariç tutularak, hakkında yapılan tüm değerlendirmelerin ağırlıklı ortalamasıdır. Bu değer, çalışanın iş ortamındaki yetkinliklerinin ve davranışlarının değerlendiriciler tarafından nasıl gözlemlendiğini yansıtır.\n\n";
				PDF.Paragraph pBelowDesc2Text = new PDF.Paragraph(desc2Text, fNormal);
				pBelowDesc2.Alignment = PDF.Element.ALIGN_JUSTIFIED;
				pBelowDesc2Text.Alignment = PDF.Element.ALIGN_JUSTIFIED;

				doc.Add(pBelowDesc1);
				doc.Add(pBelowDesc1Text);
				doc.Add(pBelowDesc2);
				doc.Add(pBelowDesc2Text);
			}
		}

		private static void DrawHeatmap(PDF.Document doc, DataTable dtSorular, string title, bool isEmployeeView = false)
		{
			AddHeader(doc, "4. DAVRANIŞSAL GÖSTERGELER ANALİZİ (ISI HARİTASI)");

			string heatDesc = isEmployeeView
				? "Aşağıdaki tabloda her bir davranış göstergesi için aldığın detaylı puanlar yer almaktadır. Hücre renkleri, gelişim alanlarından yüksek performans alanlarına doğru 5'li değerlendirme skalasını görselleştirecek şekilde yapılandırılmıştır.\n\n"
				: "Aşağıdaki tabloda çalışanınızın her bir davranış göstergesi için aldığı detaylı puanlar yer almaktadır. Hücre renkleri, gelişim alanlarından yüksek performans alanlarına doğru 5'li değerlendirme skalasını görselleştirecek şekilde yapılandırılmıştır.\n\n";
			PDF.Paragraph pDesc = new PDF.Paragraph(heatDesc, fNormal);
			pDesc.Alignment = PDF.Element.ALIGN_JUSTIFIED;
			doc.Add(pDesc);

			PDFIO.PdfPTable table = new PDFIO.PdfPTable(4);
			table.WidthPercentage = 100;
			table.SetWidths(new float[] { 55, 15, 15, 15 });
			table.HeaderRows = 1;

			string yaka = "Beyaz";
			if (dtSorular != null && dtSorular.Columns.Contains("Yaka") && dtSorular.Rows.Count > 0)
			{
				yaka = dtSorular.Rows[0]["Yaka"].ToString();
			}

			string formattedTitle = culture.TextInfo.ToTitleCase(title.ToLower(culture));

			AddHeatmapHeader(table, "Davranış Göstergesi (Soru)");
			AddHeatmapHeader(table, "Siz");

			if (yaka == "Mavi")
			{
				AddHeatmapHeader(table, "Yöneticiler Ortalaması");
			}
			else
			{
				AddHeatmapHeader(table, "Genel Ortalama");
			}

			AddHeatmapHeader(table, $"Şirket İçi\n{formattedTitle} Ortalaması");

			string lastCategory = "";

			if (dtSorular != null)
			{
				foreach (DataRow row in dtSorular.Rows)
				{
					string category = row.Table.Columns.Contains("CompetencyName") ? row["CompetencyName"].ToString() : "";
					string question = row.Table.Columns.Contains("QuestionText") ? row["QuestionText"].ToString() : "";

					double self = 0;
					if (row.Table.Columns.Contains("SelfScore") && row["SelfScore"] != DBNull.Value) self = Convert.ToDouble(row["SelfScore"]);

					double compTitleAvg = 0;
					if (row.Table.Columns.Contains("CompanyTitleAvg") && row["CompanyTitleAvg"] != DBNull.Value) compTitleAvg = Convert.ToDouble(row["CompanyTitleAvg"]);

					double midScore = 0;
					if (yaka == "Mavi")
					{
						if (row.Table.Columns.Contains("ManagerScore") && row["ManagerScore"] != DBNull.Value) midScore = Convert.ToDouble(row["ManagerScore"]);
					}
					else
					{
						if (row.Table.Columns.Contains("WeightedScore") && row["WeightedScore"] != DBNull.Value) midScore = Convert.ToDouble(row["WeightedScore"]);
					}

					if (category != lastCategory)
					{
						PDFIO.PdfPCell catCell = new PDFIO.PdfPCell(new PDF.Phrase(category, fHeatmapBold));
						catCell.Colspan = 4;
						catCell.BackgroundColor = clrLightGray;
						catCell.Padding = 5f;
						table.AddCell(catCell);
						lastCategory = category;
					}

					PDFIO.PdfPCell qCell = new PDFIO.PdfPCell(new PDF.Phrase(question, fHeatmapNormal));
					qCell.Padding = 5f;
					qCell.BorderColor = PDF.BaseColor.LIGHT_GRAY;
					table.AddCell(qCell);

					table.AddCell(CreateHeatmapCell(self > 0 ? self.ToString("0.0", culture) : "-", GetHeatmapColor(self), fHeatmapNormal));
					table.AddCell(CreateHeatmapCell(midScore > 0 ? midScore.ToString("0.0", culture) : "-", GetHeatmapColor(midScore), fHeatmapNormal));
					table.AddCell(CreateHeatmapCell(compTitleAvg > 0 ? compTitleAvg.ToString("0.0", culture) : "-", GetHeatmapColor(compTitleAvg), fHeatmapBold));
				}
			}
			doc.Add(table);
		}

		private static void DrawCompetencyAndAwareness(PDF.Document doc, Chart gapChart, double selfScore, double generalScore, DataTable dt, bool isEmployeeView = false)
		{
			AddHeader(doc, "5. YETKİNLİK PROFİLİ VE FARKINDALIK ANALİZİ");

			string compDesc = isEmployeeView
				? "Bu bölümde en güçlü olduğun yetkinlikler ile gelişime açık alanların özetlenmiş; aynı zamanda kendi öz değerlendirmen ile çevrenden gelen değerlendirmeler arasındaki farklar (Johari Penceresi) mantıksal bir çerçevede birleştirilmiştir.\n\n"
				: "Bu bölümde çalışanınızın en güçlü olduğu yetkinlikler ile gelişime açık alanları özetlenmiş; aynı zamanda çalışanın öz değerlendirmesi ile değerlendirici çevresinin algısı arasındaki farklar (Johari Penceresi) mantıksal bir çerçevede birleştirilmiştir.\n\n";
			PDF.Paragraph pDesc = new PDF.Paragraph(compDesc, fNormal);
			pDesc.Alignment = PDF.Element.ALIGN_JUSTIFIED;
			doc.Add(pDesc);

			if (gapChart != null)
			{
				using (MemoryStream ms = new MemoryStream())
				{
					int originalWidth = gapChart.Width;
					int originalHeight = gapChart.Height;

					gapChart.Size = new Size(2200, 650);

					gapChart.SaveImage(ms, ChartImageFormat.Png);

					gapChart.Size = new Size(originalWidth, originalHeight);

					PDF.Image imgGap = PDF.Image.GetInstance(ms.GetBuffer());
					float availableWidth = doc.PageSize.Width - doc.LeftMargin - doc.RightMargin;
					imgGap.ScaleToFit(availableWidth, 300f);
					imgGap.Alignment = PDF.Element.ALIGN_CENTER;
					doc.Add(imgGap);
				}
			}

			doc.Add(new PDF.Paragraph("\n"));

			PDFIO.PdfPTable summaryLayout = new PDFIO.PdfPTable(5);
			summaryLayout.WidthPercentage = 100;
			summaryLayout.SetWidths(new float[] { 32, 2, 32, 2, 32 });

			summaryLayout.AddCell(CreateIconBox("G", "Genel Ortalama", generalScore.ToString("0.0", culture), clrGenel));
			summaryLayout.AddCell(CreateSpacer());

			summaryLayout.AddCell(CreateIconBox("S", "Siz", selfScore.ToString("0.0", culture), clrSelf));
			summaryLayout.AddCell(CreateSpacer());

			double diff = generalScore - selfScore;
			summaryLayout.AddCell(CreateSimpleBox("Fark", diff.ToString("+0.0;-0.0;0.0", culture)));

			doc.Add(summaryLayout);
			doc.Add(new PDF.Paragraph("\n\n"));

			List<string> topRows = new List<string>();
			List<string> bottomRows = new List<string>();

			if (dt != null && dt.Columns.Contains("WeightedScore"))
			{
				DataView dv = dt.DefaultView;
				dv.Sort = "WeightedScore DESC";
				DataTable sortedDt = dv.ToTable();

				topRows = sortedDt.AsEnumerable().Take(3)
					.Select(r => $"{r["CompetencyName"]} ({Convert.ToDouble(r["WeightedScore"]).ToString("0.0", culture)})").ToList();

				bottomRows = sortedDt.AsEnumerable().OrderBy(r => Convert.ToDouble(r["WeightedScore"])).Take(3)
					.Select(r => $"{r["CompetencyName"]} ({Convert.ToDouble(r["WeightedScore"]).ToString("0.0", culture)})").ToList();
			}

			PDFIO.PdfPTable grid = new PDFIO.PdfPTable(2);
			grid.WidthPercentage = 100;
			grid.SetWidths(new float[] { 50, 50 });
			grid.SpacingBefore = 10f;

			PDFIO.PdfPCell leftColumn = new PDFIO.PdfPCell();
			leftColumn.Border = PDF.Rectangle.NO_BORDER;
			leftColumn.PaddingRight = 5f;

			leftColumn.AddElement(CreateListCell(
				isEmployeeView ? "En Güçlü Yönlerin" : "En Güçlü Yönleri",
				isEmployeeView
					? "Hakkında yapılan değerlendirmelerin genel sonucuna göre en başarılı bulunduğun ve öne çıkan alanlar:"
					: "Hakkında yapılan değerlendirmelerin genel sonucuna göre en başarılı bulunduğu ve öne çıkan alanlar:",
				topRows,
				clrSoftGreen,
				clrDarkGreenText));

			PDFIO.PdfPCell rightColumn = new PDFIO.PdfPCell();
			rightColumn.Border = PDF.Rectangle.NO_BORDER;
			rightColumn.PaddingLeft = 5f;

			rightColumn.AddElement(CreateListCell(
				isEmployeeView ? "Gelişim Önceliklerin" : "Gelişim Öncelikleri",
				isEmployeeView
					? "Gelecek dönemde odaklanmanın ve üzerinde çalışmanın profesyonel gelişimine en çok katkı sağlayacağı alanlar:"
					: "Gelecek dönemde çalışanınızın odaklanması ve üzerinde çalışmasının profesyonel gelişimine en çok katkı sağlayacağı alanlar:",
				bottomRows,
				clrSoftOrange,
				clrDarkOrangeText));

			grid.AddCell(leftColumn);
			grid.AddCell(rightColumn);

			doc.Add(grid);
		}

		private static PDFIO.PdfPTable CreateListCell(string title, string desc, List<string> items, PDF.BaseColor bgColor, PDF.BaseColor titleColor)
		{
			PDFIO.PdfPTable table = new PDFIO.PdfPTable(1);
			table.WidthPercentage = 100;

			PDFIO.PdfPCell cell = new PDFIO.PdfPCell();
			cell.Border = PDF.Rectangle.BOX;
			cell.BorderColor = clrBoxBorder;
			cell.Padding = 15f;
			cell.BackgroundColor = bgColor;

			cell.AddElement(new PDF.Paragraph(title, new PDF.Font(baseFont, 11, PDF.Font.BOLD, titleColor)));

			PDF.Paragraph pDesc = new PDF.Paragraph(desc + "\n\n", fNormal);
			pDesc.SetLeading(0, 1.3f);
			cell.AddElement(pDesc);

			if (items.Count > 0)
			{
				foreach (var item in items)
				{
					cell.AddElement(new PDF.Paragraph("• " + item, fBold));
				}
			}
			else
			{
				cell.AddElement(new PDF.Paragraph("Bu kategoride belirgin bir tespit bulunmamaktadır.", fSmall));
			}
			table.AddCell(cell);
			return table;
		}

		private static void DrawDevelopmentPlan(PDF.Document doc, DataTable dtEgitimler)
		{
			AddHeader(doc, "6. BİREYSEL GELİŞİM PLANI VE EĞİTİM YOL HARİTASI");

			PDF.Paragraph pDesc = new PDF.Paragraph("Performans değerlendirme sonuçlarınıza dayanarak sistem tarafından atanmış gelişim aksiyonları ve eğitim modülleri aşağıda listelenmiştir. İlgili eğitim materyallerine ulaşmak için bağlantılara tıklayabilirsiniz.\n\n", fNormal);
			pDesc.Alignment = PDF.Element.ALIGN_JUSTIFIED;
			doc.Add(pDesc);

			PDF.Paragraph pLegendIntro = new PDF.Paragraph("Gelişim Planı Seviyeleri ve Puan Aralıkları:", fBold);
			pLegendIntro.SpacingAfter = 5f;
			doc.Add(pLegendIntro);

			PDFIO.PdfPTable tLegend = new PDFIO.PdfPTable(3);
			tLegend.WidthPercentage = 100;
			tLegend.SpacingAfter = 15f;

			tLegend.AddCell(CreateLegendCell("Düşük Seviye (1.00 - 2.99)", "Temel yetkinlik kazandırmaya ve eksiklikleri gidermeye yönelik aksiyonlar.", clrSoftRed, clrDarkRedText));
			tLegend.AddCell(CreateLegendCell("Orta Seviye (3.00 - 4.00)", "Mevcut yetkinliği pekiştirmeye ve ileri seviye pratiğe dönüştürmeye yönelik aksiyonlar.", clrBoxBg, clrPrimary));
			tLegend.AddCell(CreateLegendCell("Yüksek Seviye (4.01 - 5.00)", "Uzmanlık seviyesini korumaya ve deneyimi organizasyona yaymaya yönelik ileri düzey çalışmalar.", clrSoftGreen, clrDarkGreenText));
			doc.Add(tLegend);

			if (dtEgitimler != null)
			{
				foreach (DataRow row in dtEgitimler.Rows)
				{
					string yetkinlik = row.Table.Columns.Contains("CompetencyName") ? row["CompetencyName"].ToString() : "";
					string egitim = row.Table.Columns.Contains("TrainingName") ? row["TrainingName"].ToString() : "";
					string detay = row.Table.Columns.Contains("TrainingDescription") ? row["TrainingDescription"].ToString() : "";
					string seviye = row.Table.Columns.Contains("Seviye") ? row["Seviye"].ToString() : "";
					string link = row.Table.Columns.Contains("TrainingLink") ? row["TrainingLink"].ToString() : "";
					string tur = row.Table.Columns.Contains("TrainingType") ? row["TrainingType"].ToString() : "";

					PDFIO.PdfPTable tPlan = new PDFIO.PdfPTable(1);
					tPlan.WidthPercentage = 100;
					tPlan.SpacingAfter = 12f;
					tPlan.KeepTogether = true;

					PDFIO.PdfPCell hCell = new PDFIO.PdfPCell(new PDF.Phrase($"{yetkinlik} (Mevcut Seviye: {seviye})", fBold));
					hCell.BackgroundColor = clrLightGray;
					hCell.BorderColor = clrBoxBorder;
					hCell.Padding = 8f;
					tPlan.AddCell(hCell);

					PDF.Paragraph pText = new PDF.Paragraph();

					string actionPrefix = string.IsNullOrWhiteSpace(tur) ? "Önerilen Gelişim Aksiyonu:" : $"Önerilen Gelişim Aksiyonu ({tur.ToUpper()}):";

					pText.Add(new PDF.Chunk(actionPrefix + " ", new PDF.Font(baseFont, 9, PDF.Font.BOLD, clrPrimary)));
					pText.Add(new PDF.Chunk($"{egitim}\n\n", fBold));
					pText.Add(new PDF.Chunk($"{detay}\n", fNormal));

					if (!string.IsNullOrWhiteSpace(link))
					{
						PDF.Font linkFont = new PDF.Font(baseFont, 8, PDF.Font.UNDERLINE, new PDF.BaseColor(0, 102, 204));
						PDF.Chunk linkChunk = new PDF.Chunk("Eğitim materyali ve detaylı içerik için tıklayınız.", linkFont);
						linkChunk.SetAnchor(link);
						pText.Add(new PDF.Chunk("\n", fNormal));
						pText.Add(linkChunk);
					}

					PDFIO.PdfPCell bCell = new PDFIO.PdfPCell(pText);
					bCell.BorderColor = clrBoxBorder;
					bCell.Padding = 10f;
					tPlan.AddCell(bCell);

					doc.Add(tPlan);
				}
			}
		}

		private static PDFIO.PdfPCell CreateLegendCell(string title, string desc, PDF.BaseColor bgColor, PDF.BaseColor titleColor)
		{
			PDFIO.PdfPCell cell = new PDFIO.PdfPCell();
			cell.Border = PDF.Rectangle.BOX;
			cell.BorderColor = clrBoxBorder;
			cell.BackgroundColor = bgColor;
			cell.Padding = 8f;

			cell.AddElement(new PDF.Paragraph(title, new PDF.Font(baseFont, 9, PDF.Font.BOLD, titleColor)) { Alignment = PDF.Element.ALIGN_CENTER });
			cell.AddElement(new PDF.Paragraph(desc, fSmall) { Alignment = PDF.Element.ALIGN_CENTER });

			return cell;
		}

		private static PDF.BaseColor GetHeatmapColor(double score)
		{
			if (score == 0) return PDF.BaseColor.WHITE;

			if (score >= 4.5) return new PDF.BaseColor(169, 223, 191);
			if (score >= 3.5) return new PDF.BaseColor(213, 245, 227);
			if (score >= 2.5) return new PDF.BaseColor(252, 243, 207);
			if (score >= 1.5) return new PDF.BaseColor(250, 219, 216);
			return new PDF.BaseColor(241, 148, 138);
		}

		private static void AddHeader(PDF.Document doc, string text)
		{
			PDFIO.PdfPTable t = new PDFIO.PdfPTable(1); t.WidthPercentage = 100; t.SpacingBefore = 10; t.SpacingAfter = 10;
			PDFIO.PdfPCell c = new PDFIO.PdfPCell(new PDF.Phrase(text, fHeader1));
			c.Border = PDF.Rectangle.BOTTOM_BORDER; c.BorderColor = clrPrimary; c.BorderWidthBottom = 2f; c.PaddingBottom = 6;
			t.AddCell(c); doc.Add(t);
		}

		private static void AddGrayHeader(PDFIO.PdfPTable table, string text)
		{
			PDFIO.PdfPCell c = new PDFIO.PdfPCell(new PDF.Phrase(text, fBold));
			c.BackgroundColor = new PDF.BaseColor(250, 250, 250);
			c.BorderColor = PDF.BaseColor.LIGHT_GRAY;
			c.Padding = 8f; c.HorizontalAlignment = PDF.Element.ALIGN_CENTER; c.VerticalAlignment = PDF.Element.ALIGN_MIDDLE;
			table.AddCell(c);
		}

		private static void AddCellBasic(PDFIO.PdfPTable table, string text, int align)
		{
			PDFIO.PdfPCell c = new PDFIO.PdfPCell(new PDF.Phrase(text, fNormal));
			c.BorderColor = PDF.BaseColor.LIGHT_GRAY;
			c.Padding = 8f; c.HorizontalAlignment = align; c.VerticalAlignment = PDF.Element.ALIGN_MIDDLE;
			table.AddCell(c);
		}

		private static void AddGroupHeaderCell(PDFIO.PdfPTable table, string text, int align)
		{
			PDFIO.PdfPCell cell = new PDFIO.PdfPCell(new PDF.Phrase(text, fBold));
			cell.Border = PDF.Rectangle.BOTTOM_BORDER; cell.BorderWidthBottom = 1.5f; cell.BorderColorBottom = PDF.BaseColor.LIGHT_GRAY;
			cell.HorizontalAlignment = align; cell.PaddingBottom = 8f;
			table.AddCell(cell);
		}

		private static void AddGroupRow(PDFIO.PdfPTable table, string groupName, int count)
		{
			PDFIO.PdfPCell cName = new PDFIO.PdfPCell(new PDF.Phrase(groupName, fNormal));
			cName.Border = PDF.Rectangle.BOTTOM_BORDER; cName.BorderColorBottom = clrLightGray; cName.Padding = 6f;
			table.AddCell(cName);
			PDFIO.PdfPCell cCount = new PDFIO.PdfPCell(new PDF.Phrase(count.ToString(), fNormal));
			cCount.Border = PDF.Rectangle.BOTTOM_BORDER; cCount.BorderColorBottom = clrLightGray; cCount.HorizontalAlignment = PDF.Element.ALIGN_CENTER; cCount.Padding = 6f;
			table.AddCell(cCount);
		}

		private static void AddHeatmapHeader(PDFIO.PdfPTable table, string text)
		{
			PDFIO.PdfPCell c = new PDFIO.PdfPCell(new PDF.Phrase(text, fHeatmapBold));
			c.BackgroundColor = new PDF.BaseColor(250, 250, 250);
			c.BorderColor = PDF.BaseColor.LIGHT_GRAY;
			c.Padding = 6f;
			c.HorizontalAlignment = PDF.Element.ALIGN_CENTER;
			c.VerticalAlignment = PDF.Element.ALIGN_MIDDLE;
			table.AddCell(c);
		}

		private static PDFIO.PdfPCell CreateHeatmapCell(string text, PDF.BaseColor bgColor, PDF.Font font)
		{
			PDFIO.PdfPCell cell = new PDFIO.PdfPCell(new PDF.Phrase(text, font));
			cell.BackgroundColor = bgColor;
			cell.HorizontalAlignment = PDF.Element.ALIGN_CENTER;
			cell.VerticalAlignment = PDF.Element.ALIGN_MIDDLE;
			cell.BorderColor = PDF.BaseColor.LIGHT_GRAY;
			cell.Padding = 5f;
			return cell;
		}

		private static void DrawSkalaBoxes(PDF.Document doc)
		{
			PDFIO.PdfPTable table = new PDFIO.PdfPTable(5); table.WidthPercentage = 100; table.SpacingBefore = 5f; table.DefaultCell.Border = PDF.Rectangle.NO_BORDER;
			string[] labels = { "Gelişim Alanı", "Beklentilerin Altında", "Beklentileri Karşılıyor", "Beklentileri Aşıyor", "Rol Model" };

			// Barem renk skalası: 1 kırmızı → 5 yeşil (geçişli)
			PDF.BaseColor[] bgColors = {
				new PDF.BaseColor(231, 76, 60),   // 1 kırmızı
				new PDF.BaseColor(230, 126, 34),  // 2 turuncu
				new PDF.BaseColor(241, 196, 15),  // 3 sarı
				new PDF.BaseColor(125, 206, 130), // 4 açık yeşil
				new PDF.BaseColor(39, 174, 96)    // 5 belirgin yeşil
			};
			// Açık zeminlerde (sarı, açık yeşil) koyu yazı; koyu zeminlerde beyaz yazı okunaklı olur
			PDF.BaseColor[] textColors = {
				PDF.BaseColor.WHITE,            // 1
				PDF.BaseColor.WHITE,            // 2
				new PDF.BaseColor(70, 55, 0),   // 3 sarı → koyu
				new PDF.BaseColor(20, 70, 30),  // 4 açık yeşil → koyu
				PDF.BaseColor.WHITE             // 5
			};

			for (int i = 1; i <= 5; i++)
			{
				PDF.BaseColor txt = textColors[i - 1];
				PDFIO.PdfPCell container = new PDFIO.PdfPCell(); container.Border = PDF.Rectangle.NO_BORDER; container.Padding = 5f;
				PDFIO.PdfPTable boxTable = new PDFIO.PdfPTable(1);
				PDFIO.PdfPCell box = new PDFIO.PdfPCell(); box.FixedHeight = 70f; box.Border = PDF.Rectangle.BOX; box.BorderColor = PDF.BaseColor.LIGHT_GRAY; box.VerticalAlignment = PDF.Element.ALIGN_MIDDLE;
				box.BackgroundColor = bgColors[i - 1];
				PDF.Paragraph pNum = new PDF.Paragraph(i.ToString(), new PDF.Font(baseFont, 12, PDF.Font.BOLD, txt)); pNum.Alignment = PDF.Element.ALIGN_CENTER; box.AddElement(pNum);
				PDF.Paragraph pText = new PDF.Paragraph(labels[i - 1], new PDF.Font(baseFont, 8, PDF.Font.BOLD, txt)); pText.Alignment = PDF.Element.ALIGN_CENTER; box.AddElement(pText);
				boxTable.AddCell(box); container.AddElement(boxTable); table.AddCell(container);
			}
			doc.Add(table);
		}

		private static PDFIO.PdfPCell CreateIconBox(string letter, string label, string value, PDF.BaseColor color)
		{
			PDFIO.PdfPTable inner = new PDFIO.PdfPTable(3);
			inner.SetWidths(new float[] { 15, 55, 30 });
			PDFIO.PdfPCell iconCell = new PDFIO.PdfPCell(new PDF.Phrase(letter, fIconText));
			iconCell.BackgroundColor = color; iconCell.HorizontalAlignment = PDF.Element.ALIGN_CENTER; iconCell.VerticalAlignment = PDF.Element.ALIGN_MIDDLE; iconCell.Border = PDF.Rectangle.NO_BORDER; iconCell.FixedHeight = 20f;
			inner.AddCell(iconCell);
			PDFIO.PdfPCell labelCell = new PDFIO.PdfPCell(new PDF.Phrase(" " + label, fBold));
			labelCell.Border = PDF.Rectangle.NO_BORDER; labelCell.VerticalAlignment = PDF.Element.ALIGN_MIDDLE;
			inner.AddCell(labelCell);
			PDFIO.PdfPCell valCell = new PDFIO.PdfPCell(new PDF.Phrase(value, fBigNumber));
			valCell.Border = PDF.Rectangle.NO_BORDER; valCell.HorizontalAlignment = PDF.Element.ALIGN_RIGHT; valCell.VerticalAlignment = PDF.Element.ALIGN_MIDDLE;
			inner.AddCell(valCell);
			PDFIO.PdfPCell container = new PDFIO.PdfPCell(inner);
			container.Border = PDF.Rectangle.BOX; container.BorderWidth = 1f; container.BorderColor = clrBoxBorder; container.BackgroundColor = clrBoxBg; container.Padding = 8f;
			return container;
		}

		private static PDFIO.PdfPCell CreateSimpleBox(string label, string value)
		{
			PDFIO.PdfPTable inner = new PDFIO.PdfPTable(2);
			inner.SetWidths(new float[] { 60, 40 });
			PDFIO.PdfPCell labelCell = new PDFIO.PdfPCell(new PDF.Phrase(label, fBold));
			labelCell.Border = PDF.Rectangle.NO_BORDER; labelCell.VerticalAlignment = PDF.Element.ALIGN_MIDDLE;
			inner.AddCell(labelCell);
			PDFIO.PdfPCell valCell = new PDFIO.PdfPCell(new PDF.Phrase(value, fBigNumber));
			valCell.Border = PDF.Rectangle.NO_BORDER; valCell.HorizontalAlignment = PDF.Element.ALIGN_RIGHT; valCell.VerticalAlignment = PDF.Element.ALIGN_MIDDLE;
			inner.AddCell(valCell);
			PDFIO.PdfPCell container = new PDFIO.PdfPCell(inner);
			container.Border = PDF.Rectangle.BOX; container.BorderWidth = 1f; container.BorderColor = clrBoxBorder; container.BackgroundColor = clrBoxBg; container.Padding = 8f;
			return container;
		}

		private static PDFIO.PdfPCell CreateSpacer()
		{
			return new PDFIO.PdfPCell() { Border = PDF.Rectangle.NO_BORDER };
		}

		private static PDF.Image CreateScoreBarImage(double score)
		{
			int width = 300; int height = 30; int margin = 5;
			using (Bitmap bmp = new Bitmap(width, height))
			{
				using (Graphics g = Graphics.FromImage(bmp))
				{
					g.SmoothingMode = SmoothingMode.AntiAlias;
					g.Clear(Color.White);
					Rectangle rectBar = new Rectangle(margin, 6, width - (margin * 2), 18);
					GraphicsPath path = new GraphicsPath();
					int radius = 18;
					path.AddArc(rectBar.X, rectBar.Y, radius, radius, 180, 90);
					path.AddArc(rectBar.Right - radius, rectBar.Y, radius, radius, 270, 90);
					path.AddArc(rectBar.Right - radius, rectBar.Bottom - radius, radius, radius, 0, 90);
					path.AddArc(rectBar.X, rectBar.Bottom - radius, radius, radius, 90, 90);
					path.CloseFigure();
					using (LinearGradientBrush brush = new LinearGradientBrush(rectBar, Color.Red, Color.Green, LinearGradientMode.Horizontal))
					{
						ColorBlend cb = new ColorBlend();
						cb.Positions = new float[] { 0, 0.5f, 1 };
						cb.Colors = new Color[] { Color.FromArgb(231, 76, 60), Color.FromArgb(241, 196, 15), Color.FromArgb(46, 204, 113) };
						brush.InterpolationColors = cb;
						g.FillPath(brush, path);
					}
					double ratio = (Math.Max(1, Math.Min(5, score)) - 1) / 4.0;
					int workableWidth = rectBar.Width;
					int circleX = rectBar.X + (int)(ratio * workableWidth);
					int circleSize = 22;
					if (circleX < rectBar.X + (circleSize / 2)) circleX = rectBar.X + (circleSize / 2);
					if (circleX > rectBar.Right - (circleSize / 2)) circleX = rectBar.Right - (circleSize / 2);
					SolidBrush blueBrush = new SolidBrush(Color.FromArgb(41, 128, 185));
					g.FillEllipse(blueBrush, circleX - (circleSize / 2), (height / 2) - (circleSize / 2), circleSize, circleSize);
					g.DrawEllipse(new Pen(Color.White, 3), circleX - (circleSize / 2), (height / 2) - (circleSize / 2), circleSize, circleSize);
				}
				using (MemoryStream ms = new MemoryStream())
				{
					bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
					return PDF.Image.GetInstance(ms.GetBuffer());
				}
			}
		}

		class FooterEvent : PDFIO.PdfPageEventHelper
		{
			public override void OnEndPage(PDFIO.PdfWriter writer, PDF.Document document)
			{
				PDFIO.PdfContentByte cb = writer.DirectContent;
				PDFIO.ColumnText.ShowTextAligned(cb, PDF.Element.ALIGN_CENTER, new PDF.Phrase($"Sayfa {writer.PageNumber}", fSmall), (document.Right - document.Left) / 2 + document.LeftMargin, document.Bottom - 10, 0);
			}
		}
	}
}