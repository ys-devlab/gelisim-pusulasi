using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WindowsFormsApp1.AI.Core;

namespace WindowsFormsApp1.AI
{
	// OpenRouter HTTP iş mantığı AiGateway/OpenRouterProvider'a taşındı.
	// Bu sınıf geriye dönük uyumluluk için ince bir adaptör olarak korunuyor:
	// model/anahtar/endpoint ve loglama artık tek noktadan (ai.env + AiGateway) yönetilir.
	public class GeminiClient : IDisposable
	{
		private readonly AiPurpose _purpose;

		// apiKey parametresi geriye dönük uyumluluk için korunur ancak ARTIK KULLANILMAZ;
		// anahtar ai.env içindeki OPENROUTER_API_KEY'den okunur.
		public GeminiClient(string apiKey = null, AiPurpose purpose = AiPurpose.Chat)
		{
			_purpose = purpose;
		}

		public async Task<string> GenerateAsync(
			string prompt,
			bool requireJson = false,
			string systemInstructionText = null,
			Action<string> statusLogger = null)
		{
			string effectiveSystemInstruction = string.IsNullOrWhiteSpace(systemInstructionText)
				? "Sen uzman bir İK yöneticisisin. Yıldız (*) veya diyez (#) gibi markdown işaretleri kesinlikle kullanma. Çalışana doğrudan 'Siz' diliyle hitap et."
				: systemInstructionText.Trim();

			return await AiGateway.GenerateTextAsync(
				_purpose,
				new AiPrompt { User = prompt, System = effectiveSystemInstruction, RequireJson = requireJson },
				contextKey: null,
				log: statusLogger).ConfigureAwait(false);
		}

		public void Dispose() { }
	}

	public class GeminiWeightService
	{
		private readonly string _apiKey;
		public GeminiWeightService(string apiKey) { _apiKey = apiKey; }

		public async Task<Dictionary<string, double>> CalculateWeightsAsync(string department, List<string> competencyNames)
		{
			string namesJson = "[" + string.Join(", ", competencyNames.Select(n => "\"" + n + "\"")) + "]";
			string safeDept = string.IsNullOrWhiteSpace(department) ? "Genel" : department;

			string prompt = $@"Sen bir İK analiz motorusun. Görevin: verilen birim adı ve yetkinlik listesi için, o birimin operasyonel öncelikleriyle uyumlu ağırlık puanları atamak.
Birim: {safeDept}
Yetkinlikler: {namesJson}

Kurallar:
1. Her yetkinlik tam adıyla anahtar olmalı.
2. Değerler pozitif ondalık sayı olmalı ve toplamları tam 1.0 olmalı.
3. Birimle daha alakalı yetkinliklere yüksek ağırlık ver.
Örnek Format: {{""Yetkinlik A"": 0.40, ""Yetkinlik B"": 0.60}}";

			{
				string response = await AiGateway.GenerateTextAsync(
					AiPurpose.Weights,
					new AiPrompt { User = prompt, RequireJson = true },
					contextKey: department).ConfigureAwait(false);
				var result = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
				try
				{
					var jobj = JObject.Parse(response);
					foreach (var prop in jobj.Properties())
					{
						if (double.TryParse(prop.Value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double w))
						{
							result[prop.Name.Trim()] = w;
						}
					}
				}
				catch { }

				foreach (var name in competencyNames)
				{
					if (!result.ContainsKey(name)) result[name] = 0.01;
				}
				double total = result.Values.Sum();
				if (total <= 0) total = 1;
				foreach (var k in result.Keys.ToList()) result[k] = result[k] / total;

				return result;
			}
		}
	}

	/// <summary>Rapor istemi: modele gidecek system + user metni (çağrı yapılmadan).</summary>
	public sealed class NarrativePromptParts
	{
		public string System { get; set; }
		public string User { get; set; }
	}

	public class GeminiNarrativeService
	{
		private readonly string _apiKey;
		public GeminiNarrativeService(string apiKey) { _apiKey = apiKey; }

		/// <summary>
		/// Rapor istemini (system + user) kurar — LLM çağrısı YAPMAZ. İstem-oluşturma mantığı
		/// burada toplanır; hem üretim (<see cref="GenerateNarrativeAsync"/>) hem de deney harness'i
		/// aynı istemi birebir yeniden kullanabilsin diye ayrılmıştır. Saf/yan-etkisiz.
		/// </summary>
		/// <param name="isEmployeeView">
		/// true  → Kariyer Koçu perspektifi: çalışana doğrudan 2. tekil şahısla hitap (Sen, Güçlü yönlerin...).
		/// false → İK/Yönetim Danışmanı perspektifi: yöneticiye 3. tekil şahısla rapor (Çalışan, Güçlü yönleri...).
		/// </param>
		public NarrativePromptParts BuildNarrativePrompt(
			string title, string department,
			DataTable dtAnaYetkinlikler, DataTable dtSorular,
			double finalScore5,
			bool isEmployeeView = false,
			string employeeName = "")
		{
			var allComps = new List<Tuple<string, double>>();
			double avgSelfScore = 0;
			double companyAvgSum = 0;
			int companyAvgCount = 0;

			if (dtAnaYetkinlikler != null && dtAnaYetkinlikler.Columns.Contains("CompetencyName") && dtAnaYetkinlikler.Columns.Contains("WeightedScore"))
			{
				foreach (DataRow row in dtAnaYetkinlikler.Rows)
				{
					if (dtAnaYetkinlikler.Columns.Contains("CompanyTitleAvg"))
					{
						double cAvg = ToDoubleSafe(row["CompanyTitleAvg"]);
						if (cAvg > 0)
						{
							companyAvgSum += cAvg;
							companyAvgCount++;
						}
					}
				}
			}

			double companyGeneralAvg = companyAvgCount > 0 ? companyAvgSum / companyAvgCount : 0;

			if (dtSorular != null && dtSorular.Columns.Contains("CompetencyName") && dtSorular.Columns.Contains("WeightedScore"))
			{
				var grouped = dtSorular.AsEnumerable()
					.GroupBy(r => r["CompetencyName"]?.ToString()?.Trim() ?? "")
					.Where(g => !string.IsNullOrWhiteSpace(g.Key));

				foreach (var g in grouped)
				{
					double avgScore = g.Average(r => ToDoubleSafe(r["WeightedScore"]));
					allComps.Add(Tuple.Create(g.Key, avgScore));
				}

				if (dtSorular.Columns.Contains("SelfScore"))
				{
					var validRows = dtSorular.AsEnumerable().Where(r => ToDoubleSafe(r["SelfScore"]) > 0);
					if (validRows.Any())
					{
						avgSelfScore = validRows.Average(r => ToDoubleSafe(r["SelfScore"]));
					}
				}
			}

			var strengthsList = allComps.OrderByDescending(c => c.Item2).Take(3).ToList();
			var devList = allComps.OrderBy(c => c.Item2).Take(3).ToList();

			// Eğer gelişim listesi ve güçlü yönler çakışıyorsa (çok az yetkinlik varsa), gelişim alanlarını en düşük 1-2'si ile sınırla
			if (allComps.Count <= 3) {
				devList = allComps.OrderBy(c => c.Item2).Take(1).ToList();
			}

			string strengthsStr = strengthsList.Any()
				? string.Join(", ", strengthsList.Select(c => c.Item1))
				: "Belirgin bir güçlü yön verisi bulunamadı.";

			string devStr = devList.Any() 
				? string.Join("\n", devList.Select(c => $"- {c.Item1} (Puan: {c.Item2:0.00})")) 
				: "Belirgin bir gelişim alanı verisi bulunamadı.";

			string safeFinalScore = finalScore5.ToString("0.00", CultureInfo.InvariantCulture);
			string safeTitle = string.IsNullOrWhiteSpace(title) ? "Çalışan" : title;

			// Birim adını "... birimindeki" şeklinde formatla — birim boşsa veya zaten "birim" içeriyorsa eki düzgün ekle
			string rawDept = (department ?? "").Trim();
			string birimEki; // "X birimindeki" veya "X'deki" veya boş
			if (string.IsNullOrWhiteSpace(rawDept))
			{
				birimEki = ""; // birim yoksa kullanma
			}
			else
			{
				// Türkçe büyük-küçük harf duyarsız "birim" kontrolü
				bool deptHasBirim = rawDept.ToUpper(System.Globalization.CultureInfo.GetCultureInfo("tr-TR")).Contains("BİRİM")
				                 || rawDept.ToUpper().Contains("BIRIM");
				birimEki = deptHasBirim ? $"{rawDept}'deki " : $"{rawDept} birimindeki ";
			}
			string safeDept = string.IsNullOrWhiteSpace(rawDept) ? "şirketimizde" : rawDept;

			double gap = avgSelfScore - finalScore5;

			// ── Perspektife göre değişen ifadeler ──────────────────────────
			string girisMetni;
			string gapInfo;
			string comparisonInfo = "";
			string kapanisBolumu;
			string perspektifTalimati;

			if (isEmployeeView)
			{
				// ── Çalışanın kendisi görüntülüyor (Kariyer Koçu dili) ─────
				girisMetni = $"🚀 {birimEki}360 derece değerlendirmelerin sonucunda genel performans endeksin {safeFinalScore}/5 olarak gerçekleşti.";

				gapInfo = gap >= 1.0
					? "KÖR NOKTA UYARISI: Kendinize verdiğin puanlar ile çevrenden gelen puanlar arasında belirgin bir fark var. Bu farkı bir öğrenme fırsatı olarak değerlendir."
					: "";

				if (companyGeneralAvg > 0)
				{
					double diff = finalScore5 - companyGeneralAvg;
					string status = diff >= 0 ? "üzerinde" : "altında";
					comparisonInfo = $"Şirket içi {safeTitle} unvanı ortalaması {companyGeneralAvg:0.00}'dir. Senin puanın bu ortalamanın {Math.Abs(diff):0.00} puan {status} seyrediyor.";
				}

				kapanisBolumu = "✨ KARİYER YOLCULUĞUNDA YANINDAYIZ (Sana doğrudan hitap eden, sıcak ve motive edici bir kapanış yaz. 'Sen' ve 'senin' kullan.)";
				perspektifTalimati = "Sen bir Kariyer Koçusun. Raporu çalışanın kendisi okuyacak. Tüm yorum paragraflarında 2. tekil şahıs kullan: 'Sen', 'Senin güçlü yönlerin', 'Gelişim fırsatın', 'Puanın', 'Başarın'. Sıcak, cesaretlendirici ve doğrudan hitap et.";
			}
			else
			{
				// ── Yönetici ekip raporundan görüntülüyor (İK/Yönetim Danışmanı dili) ─
				string calisan = !string.IsNullOrWhiteSpace(employeeName)
					? employeeName
					: "çalışanınız";
				string birimVurgu = string.IsNullOrWhiteSpace(rawDept) ? "" : $"{birimEki.TrimEnd()}nde görev yapan ";
				girisMetni = $"🚀 {birimVurgu}{calisan}'a ait 360 derece değerlendirmeler sonucunda genel performans endeksi {safeFinalScore}/5 olarak gerçekleşmiştir.";

				gapInfo = gap >= 1.0
					? "KÖR NOKTA UYARISI: Çalışanın öz değerlendirme puanları ile değerlendirici çevresinden gelen puanlar arasında belirgin bir fark tespit edilmiştir. Bu durum yönetici desteğiyle ele alınmalıdır."
					: "";

				if (companyGeneralAvg > 0)
				{
					double diff = finalScore5 - companyGeneralAvg;
					string status = diff >= 0 ? "üzerinde" : "altında";
					comparisonInfo = $"Şirket içi {safeTitle} unvanı ortalaması {companyGeneralAvg:0.00}'dir. Çalışanın puanı bu ortalamanın {Math.Abs(diff):0.00} puan {status} seyretmektedir.";
				}

				kapanisBolumu = "✨ YÖNETİCİYE TAVSİYELER (Yöneticiye hitap eden, çalışanı destekleme konusunda somut ve pratik öneriler içeren bir kapanış yaz. 'Çalışan', 'Bu çalışan', 'Ona' gibi 3. tekil şahıs kullan.)";
				perspektifTalimati = "Sen bir İK ve Yönetim Danışmanısın. Raporu çalışanın yöneticisi okuyacak. Tüm yorum paragraflarında 3. tekil şahıs kullan: 'Çalışan', 'Güçlü yönleri', 'Gelişim alanları', 'Puanı', 'Başarısı'. Nesnel, analitik ve yöneticiye yol gösterici bir dil kullan.";
			}

			// ── Rol bazlı üslup (her iki perspektifte de geçerli) ──────────
			bool isManager      = safeTitle.ToUpper().Contains("YÖNETİCİ") || safeTitle.ToUpper().Contains("MÜDÜR") || safeTitle.ToUpper().Contains("ŞEF") || safeTitle.ToUpper().Contains("LİDER");
			bool isProfessional = safeTitle.ToUpper().Contains("MÜHENDİS") || safeTitle.ToUpper().Contains("UZMAN") || safeTitle.ToUpper().Contains("PİLOT");
			bool isSimpleRole   = safeTitle.ToUpper().Contains("TEKNİSYEN") || safeTitle.ToUpper().Contains("HİZMET") || safeTitle.ToUpper().Contains("OPERATÖR");

			string toneInstruction = "Profesyonel, motive edici, gerçekçi ve yapıcı bir dil kullan.";
			if (isManager)      toneInstruction = "Liderlik odaklı, stratejik, vizyoner ve yönetici yetkinliklerini vurgulayan bir dil kullan. Gelişim alanlarını bir 'liderlik fırsatı' olarak sun.";
			else if (isProfessional) toneInstruction = "Analitik, teknik derinliği olan, detaylı ve yetkinlik bazlı bir dil kullan.";
			else if (isSimpleRole)   toneInstruction = "Yalın, anlaşılır, net ve doğrudan bir dil kullan. Çok akademik jargon kullanma.";

			string lengthInstruction = isProfessional ? "Rapor detaylı ve kapsamlı olsun." : (isSimpleRole ? "Rapor öz ve anlaşılır olsun." : "Rapor dengeli bir uzunlukta olsun.");

			string prompt = $@"Aşağıdaki verilere dayanarak profesyonel bir 360 derece değerlendirme raporu yaz.

PERSPEKTİF TALİMATI (EN ÖNEMLİ KURAL):
{perspektifTalimati}
Bu perspektif kuralını rapordaki TÜM yorum paragraflarına uygula. Başlıkları, emoji'leri ve [TABLO] formatını kesinlikle değiştirme.

BAĞLAM:
Çalışan Unvanı: {safeTitle}
Birim: {safeDept}
Genel Puan: {safeFinalScore}/5
{comparisonInfo}

GÜÇLÜ YÖNLER: {strengthsStr}
GELİŞİM ALANLARI: {devStr}

ÖZEL TALİMATLAR:
1. ÜSLUP: {toneInstruction} {lengthInstruction} Eksiklikleri nazik ama net bir şekilde dile getir.
2. GİRİŞ: Kesinlikle şu cümleyle başla: '{girisMetni}'
3. ŞİRKET KIYASI: 'ŞİRKET İÇİ KONUMLANMA' başlığı altında ({comparisonInfo}) perspektif kuralına uygun şekilde yorumla.
4. BAŞLIKLAR VE ESTETİK: Her bölüm için uygun emojiler kullan. Bölümleri net ayır.
5. TABLO FORMATI (KRİTİK): '🛠️ GELİŞİM YOL HARİTASI' başlığı altındaki önerileri KESİNLİKLE tablo formatında ver. Standart markdown tablosu KULLANMA. Her satırın en başına [TABLO] yaz, sütunları | ile ayır. İlk satır mutlaka başlıklar olmalıdır.
   Tablo sütunları: Yetkinlik | Gelişim Önerisi | Beklenen Katkı
   Örnek format:
   [TABLO] Yetkinlik | Gelişim Önerisi | Beklenen Katkı
   [TABLO] İletişim | Empati eğitimi al | Ekip içi uyum artışı

   GELİŞİM ÖNERİSİ YAZIM KURALLARI (ZORUNLU):
   - Her yetkinlik için farklı ve özgün bir yöntem öner. Aynı kalıp eğitim al önerisini tekrarlama.
   - Şu yöntemlerden uygun olanları seç ve somutlaştır: mentorluk programı, job shadowing,
     günlük refleksiyon günlüğü, geri bildirim döngüsü, vaka analizi, mikro-öğrenme,
     proje liderliği üstlenme, simülasyon, çapraz birim iş birliği, konferans/webinar takibi,
     podcast serisi, 30 günlük uygulama meydan okuması, buddy sistemi.
   - Öneri kısa ama somut olsun: ne yapılacağı ve nasıl uygulanacağı tek cümlede anlaşılsın.
   - Beklenen Katkı kısmında KESİNLİKLE yüzde veya kesin sayısal ifade kullanma (%20 artar, 2 kat gelişir gibi). Bunun yerine gözlemlenebilir davranışsal veya niteliksel bir çıktı yaz (örn: ekip içi iletişim akışkanlığı fark edilir biçimde artar, karar süreçlerinde özgüven gelişir).
6. JOHARI PENCERESİ: {(string.IsNullOrEmpty(gapInfo) ? "" : gapInfo)}

RAPOR YAPISI (başlıkları değiştirme, sadece içeriklerin dilini perspektife göre ayarla):
📊 GENEL PERFORMANS ÖZETİ
🏆 ÖNE ÇIKAN GÜÇLÜ YÖNLER
📈 ŞİRKET İÇİ KONUMLANMA
⚠️ ODAKLANILACAK GELİŞİM ALANLARI
🛠️ GELİŞİM YOL HARİTASI (Tablo burada olacak)
{kapanisBolumu}";

			return new NarrativePromptParts
			{
				User = prompt,
				System =
					"Kurumsal kültüre (TUSAŞ) hâkim, üst düzey ve profesyonel bir performans koçusun. Görevin, çalışanın 360 derece değerlendirme sonuçlarını yorumlamaktır. " +
					"Dil ve Üslup: Raporu okuyan kişi üst düzey bir yönetici veya kritik bir uzman olabilir. Bu nedenle dilin son derece saygılı, seviyeli, olgun ve doğrudan gelişime odaklı olmalıdır. Karmaşık İK jargonu kullanmadan, net ve anlaşılır bir Türkçe kullan. " +
					"Gerçekçilik ve Yapıcı Eleştiri: Puanlar düşükse veya gelişim alanları bariz ise, kesinlikle yapay bir şekilde aşırı motive edici ('Harikasın, bunu da yaparsın') bir dil kullanma. Gelişim alanlarını objektif bir şekilde belirt ve doğrudan yapıcı eleştiriler (profesyonel aksiyon önerileri) sun. " +
					"Denge: Yüksek puanlı yetkinlikleri takdir ederken profesyonelliği koru, düşük puanlı yetkinlikler içinse suçlayıcı olmadan gelişim fırsatlarına odaklan. " +
					"Yıldız (*) veya diyez (#) gibi markdown işaretleri kesinlikle kullanma."
			};
		}

		/// <summary>
		/// Raporun "Genel Değerlendirme / AI yorumu" metnini üretir. İstem-oluşturma
		/// <see cref="BuildNarrativePrompt"/>'a taşındı; bu metot yalnızca onu çağırır,
		/// LLM'i çalıştırır ve çıktıyı temizler. Davranış birebir korunmuştur.
		/// </summary>
		public async Task<string> GenerateNarrativeAsync(
			string title, string department,
			DataTable dtAnaYetkinlikler, DataTable dtSorular,
			double finalScore5,
			bool isEmployeeView = false,
			string employeeName = "")
		{
			NarrativePromptParts parts = BuildNarrativePrompt(
				title, department, dtAnaYetkinlikler, dtSorular, finalScore5, isEmployeeView, employeeName);

			string result = await AiGateway.GenerateTextAsync(
				AiPurpose.Narrative,
				new AiPrompt { User = parts.User, System = parts.System, RequireJson = false },
				contextKey: string.IsNullOrWhiteSpace(employeeName) ? department : employeeName).ConfigureAwait(false);
			return result.Replace("*", "").Replace("#", "").Trim();
		}

		private double ToDoubleSafe(object o)
		{
			try { return o == null || o == DBNull.Value ? 0 : Convert.ToDouble(o); }
			catch { return 0; }
		}
	}
}