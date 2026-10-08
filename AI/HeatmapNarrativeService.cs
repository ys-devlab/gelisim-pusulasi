using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using WindowsFormsApp1.AI.Core;

namespace WindowsFormsApp1.AI
{
	// ─────────────────────────────────────────────────────────────────────────────
	// BÖLÜM 1 — Birim Bazlı Otomatik Ağırlık Çözümleyici
	// ─────────────────────────────────────────────────────────────────────────────
	public sealed class DepartmentWeightResolverService
	{
		private readonly Func<OllamaClient> _clientFactory;
		public string ModelName { get; }
		public int Seed { get; }
		public double Temperature { get; }

		private readonly Dictionary<string, IReadOnlyDictionary<string, double>> _cache
			= new Dictionary<string, IReadOnlyDictionary<string, double>>(StringComparer.OrdinalIgnoreCase);

		public DepartmentWeightResolverService(
			Func<OllamaClient> clientFactory,
			string modelName = "qwen2.5:7b",
			int seed = 42,
			double temperature = 0.0)
		{
			_clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
			ModelName = string.IsNullOrWhiteSpace(modelName) ? "qwen2.5:7b" : modelName;
			Seed = seed;
			Temperature = temperature;
		}

		public async Task<IReadOnlyDictionary<string, double>> ResolveAsync(
			string department,
			IReadOnlyList<string> competencyNames,
			CancellationToken cancellationToken)
		{
			if (competencyNames == null || competencyNames.Count == 0)
				return new Dictionary<string, double>();

			string cacheKey = BuildCacheKey(department, competencyNames);
			if (_cache.TryGetValue(cacheKey, out var cached))
				return cached;

			string prompt = BuildWeightPrompt(department, competencyNames);

			string responseJson = await AiGateway.GenerateTextAsync(
				AiPurpose.DepartmentWeights,
				new AiPrompt { User = prompt, RequireJson = true },
				contextKey: department,
				cancellationToken: cancellationToken).ConfigureAwait(false);

			var weights = ParseAndNormalizeWeights(responseJson, competencyNames);
			_cache[cacheKey] = weights;
			return weights;
		}

		private static string BuildWeightPrompt(string department, IReadOnlyList<string> names)
		{
			string namesJson = "[" + string.Join(", ", names.Select(n => "\"" + n.Replace("\"", "") + "\"")) + "]";
			string dept = string.IsNullOrWhiteSpace(department) ? "Belirtilmemiş" : department.Trim();

			return $@"<|im_start|>system
Sen bir İK analiz motorusun. Görevin: verilen birim adı ve yetkinlik listesi için, o birimin operasyonel öncelikleriyle uyumlu ağırlık puanları atamak.

KURALLAR:
1. Çıktı SADECE geçerli bir JSON nesnesi olmalıdır. Markdown, açıklama veya ek metin YASAK.
2. Her yetkinlik listedeki tam adıyla anahtar olarak yer almalıdır.
3. Tüm değerler pozitif sayı olmalı ve toplamları TAM OLARAK 1.0 olmalıdır.
4. Birim ile doğrudan ilgili yetkinliklere daha yüksek ağırlık ver.
<|im_end|>
<|im_start|>user
Birim: {dept}
Yetkinlikler: {namesJson}

Sadece JSON döndür. Örnek format:
{{""Yetkinlik A"": 0.35, ""Yetkinlik B"": 0.40, ""Yetkinlik C"": 0.25}}
<|im_end|>
<|im_start|>assistant
";
		}

		private static IReadOnlyDictionary<string, double> ParseAndNormalizeWeights(
			string json, IReadOnlyList<string> competencyNames)
		{
			var result = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

			try
			{
				string clean = ExtractFirstJsonObject(json);
				var jobj = JObject.Parse(clean);

				foreach (var prop in jobj.Properties())
				{
					string key = prop.Name.Trim();
					string matched = competencyNames.FirstOrDefault(
						n => string.Equals(n, key, StringComparison.OrdinalIgnoreCase)) ?? key;

					if (double.TryParse(prop.Value.ToString(), NumberStyles.Any,
						CultureInfo.InvariantCulture, out double w) && w > 0)
					{
						result[matched] = w;
					}
				}
			}
			catch { }

			foreach (var name in competencyNames)
			{
				if (!result.ContainsKey(name))
					result[name] = 0.01;
			}

			double total = result.Values.Sum();
			if (total <= 0) total = 1;
			var keys = result.Keys.ToList();
			foreach (var k in keys)
				result[k] = result[k] / total;

			return result;
		}

		private static string ExtractFirstJsonObject(string s)
		{
			s = (s ?? "").Trim();
			int first = s.IndexOf('{');
			int last = s.LastIndexOf('}');
			if (first >= 0 && last > first) return s.Substring(first, last - first + 1);
			return "{}";
		}

		private static string BuildCacheKey(string department, IReadOnlyList<string> names)
		{
			return (department ?? "").Trim().ToLowerInvariant()
				+ "|"
				+ string.Join(",", names.OrderBy(n => n, StringComparer.Ordinal));
		}
	}

	// ─────────────────────────────────────────────────────────────────────────────
	// BÖLÜM 2 — Ana Heatmap Narrative Service (Tamamen Kusursuzlaştırıldı)
	// ─────────────────────────────────────────────────────────────────────────────
	public sealed class HeatmapNarrativeService
	{
		private readonly Func<OllamaClient> _clientFactory;
		private readonly AINarrativeRepository _narrativeRepo;
		public string ModelName { get; }
		public int Seed { get; }
		public double Temperature { get; }

		public HeatmapNarrativeService(
			Func<OllamaClient> clientFactory,
			string modelName = "qwen2.5:7b",
			int seed = 42,
			double temperature = 0.0,
			AINarrativeRepository narrativeRepo = null)
		{
			_clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
			ModelName = string.IsNullOrWhiteSpace(modelName) ? "qwen2.5:7b" : modelName;
			Seed = seed;
			Temperature = temperature;
			_narrativeRepo = narrativeRepo;
		}

		public async Task<string> GenerateNarrativeAsync(
			string title, // (Bu parametreyi modele göndermeyeceğiz, kodda dursun bozmamak için)
			string department,
			DataTable dtAnaYetkinlikler,
			DataTable dtSorular,
			IReadOnlyDictionary<string, double> competencyWeights01,
			double finalScore5,
			DataTable dtSirketOrtalama,
			double sirketGenelOrtalama5,
			CancellationToken cancellationToken)
		{
			department = (department ?? "").Trim();

			// 1. Ağırlıkları çöz
			IReadOnlyDictionary<string, double> resolvedWeights = competencyWeights01;
			if (resolvedWeights == null || resolvedWeights.Count == 0)
			{
				var competencyNames = ExtractCompetencyNames(dtAnaYetkinlikler);
				if (competencyNames.Count > 0)
				{
					var resolver = new DepartmentWeightResolverService(_clientFactory, ModelName, Seed, Temperature);
					resolvedWeights = await resolver.ResolveAsync(department, competencyNames, cancellationToken)
						.ConfigureAwait(false);
				}
			}

			// 2. Karşılaştırma Bloğu
			string comparisonBlock = BuildComparisonBlock(dtAnaYetkinlikler, dtSirketOrtalama, finalScore5, sirketGenelOrtalama5);

			// 3. Prompt üret
			string prompt = BuildProfessionalPrompt(department, finalScore5, dtSorular, comparisonBlock);

			// 4. Model/seed/temperature tek noktadan (ai.env + AiGateway) çözülür; cache anahtarı bununla senkron.
			var target = AiGateway.Resolve(AiPurpose.Heatmap);
			string model = target.Model;
			int seed = target.Seed;
			double temperature = target.Temperature;

			// 5. Önbellek kontrolü
			if (_narrativeRepo != null)
			{
				_narrativeRepo.EnsureSchema();
				string cached = _narrativeRepo.TryGetNarrativeJson("GizliUnvan", department, model, seed, temperature, prompt);
				if (!string.IsNullOrWhiteSpace(cached))
					return ExtractCommentary(cached, finalScore5);
			}

			// 6. Model çağrısı (AiGateway üzerinden — config + loglama)
			string responseJson = await AiGateway.GenerateTextAsync(
				AiPurpose.Heatmap,
				new AiPrompt { User = prompt, RequireJson = true, Seed = seed, Temperature = temperature },
				contextKey: department,
				cancellationToken: cancellationToken).ConfigureAwait(false);

			if (_narrativeRepo != null)
				_narrativeRepo.UpsertNarrativeJson("GizliUnvan", department, model, seed, temperature, prompt, responseJson);

			return ExtractCommentary(responseJson, finalScore5);
		}

		private static IReadOnlyList<string> ExtractCompetencyNames(DataTable dt)
		{
			var names = new List<string>();
			if (dt == null || !dt.Columns.Contains("CompetencyName")) return names;
			foreach (DataRow r in dt.Rows)
			{
				string n = (r["CompetencyName"] == DBNull.Value ? "" : r["CompetencyName"].ToString()).Trim();
				if (!string.IsNullOrWhiteSpace(n) && !names.Contains(n, StringComparer.OrdinalIgnoreCase))
					names.Add(n);
			}
			return names;
		}

		private static string BuildComparisonBlock(DataTable dtBirey, DataTable dtSirket, double finalScore5, double sirketGenelOrtalama5)
		{
			var sb = new StringBuilder();
			if (sirketGenelOrtalama5 > 0)
			{
				double diff = finalScore5 - sirketGenelOrtalama5;
				string diffStr = diff >= 0 ? $"+{diff:0.00} puan üzerinde" : $"{diff:0.00} puan altında";
				sb.AppendLine($"GENEL PUAN KARŞILAŞTIRMASI: Çalışan {finalScore5:0.00}/5 — Şirket Ortalaması {sirketGenelOrtalama5:0.00}/5 ({diffStr})");
			}
			return sb.ToString().Trim();
		}

		// ── YENİ: KUSURSUZ VE ROBOTİK OLMAYAN PROMPT ──────────────────────────────
		private static string BuildProfessionalPrompt(
			string department,
			double finalScore5,
			DataTable dtSorular,
			string comparisonBlock)
		{
			var allComps = new List<Tuple<string, double>>();
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
			}

			var strengthsList = allComps.Where(c => c.Item2 >= 4.0).OrderByDescending(c => c.Item2).ToList();
			var devList = allComps.Where(c => c.Item2 < 4.0).OrderBy(c => c.Item2).ToList();

			string strengthsStr = strengthsList.Any()
				? string.Join(", ", strengthsList.Select(c => c.Item1))
				: "Tüm yetkinliklerde dengeli performans";

			var devDetails = new List<string>();
			foreach (var c in devList)
			{
				string weakQuestions = GetWeakestQuestions(dtSorular, c.Item1);
				string detail = string.IsNullOrWhiteSpace(weakQuestions) ? "" : $" (Zayıf kalınan davranışlar: {weakQuestions})";
				devDetails.Add($"Yetkinlik: {c.Item1} | Puan: {c.Item2:0.00}{detail}");
			}
			string devStr = devDetails.Any() ? string.Join("\n", devDetails) : "Tüm alanlarda standartlar karşılanmaktadır.";

			string safeFinalScore = SanitizeForPrompt(finalScore5.ToString("0.00", CultureInfo.InvariantCulture));
			string safeDept = SanitizeForPrompt(string.IsNullOrWhiteSpace(department) ? "İlgili Birim" : department);

			// DİKKAT: Prompt içerisinden "Unvan" kelimesi tamamen silindi.
			// İlk cümle kesin olarak dikte edildi. Alakasız tavsiyeleri engelleyen kural eklendi.
			return $@"<|im_start|>system
Sen savunma sanayii ve kurumsal firmalarda çalışan uzman bir İK Değerlendirme Yöneticisisin. 
Görevin, bir çalışana performans sonuçlarını doğrudan, resmi, akıcı ve son derece mantıklı bir dille sunmaktır.

İHLAL EDİLEMEZ KURALLAR:
1. GİRİŞ CÜMLESİ ZORUNLULUĞU: 'BİRİMDEKİ ROLÜNÜZ VE BAŞARILARINIZ' bölümüne KESİNLİKLE şu cümleyle başlayacaksın: ""{safeDept} birimindeki 360 derece değerlendirmeleriniz neticesinde, genel performans endeksiniz {safeFinalScore}/5 olarak gerçekleşmiştir."" ASLA 'Siz', 'Unvanınız', 'Çalışanımız' gibi kelimelerle cümleye başlama.
2. MANTIKLI VE ALAKALI TAVSİYELER VER: 'SİZİN İÇİN GELİŞİM FIRSATLARI' kısmında yapay zeka halüsinasyonu yapma! Eğer yetkinlik 'Duygusal Zeka', 'İletişim' veya 'Stres Yönetimi' ise onlara iletişim ve empati eğitimleri öner; GİDİP DE alet güvenliği, makine bakımı gibi TEKNİK tavsiyeler VERME. Tavsiye, yetkinliğin kendi konusuyla birebir alakalı olmalıdır.
3. TÜM GELİŞİM ALANLARI: Sana aşağıda verilen 'GELİŞİM ALANLARI' listesindeki tüm yetkinlikleri tek tek değerlendir.
4. FORMAT: Metinlerinde madde işareti (-, *, vb.) kullanma. Akıcı, düz paragraflar yaz.

ÇIKTI FORMATI (Aşağıdaki JSON şemasını birebir kullan):
{{
  ""commentary"": {{
    ""BİRİMDEKİ ROLÜNÜZ VE BAŞARILARINIZ"": ""... (Dikte edilen cümleyle başla ve güçlü yönleri kurumsal bir dille bağla.)"",
    ""KRİTİK YETKİNLİK ANALİZİ"": ""... (Güçlü yetkinliklerin iş süreçlerine olumlu etkisini akıcı bir dille yaz.)"",
    ""ŞİRKET KARŞILAŞTIRMASI VE KONUMLANMA"": ""... (Şirket ortalaması ile olan durumu objektif olarak özetle.)"",
    ""ODAKLANILACAK GELİŞİM ALANLARI"": ""... (Listedeki TÜM gelişim alanlarını ayrıntılı paragraflar halinde açıkla. Davranışsal sorunları belirt.)"",
    ""SİZİN İÇİN GELİŞİM FIRSATLARI"": ""... (Yukarıdaki gelişim alanları için MANTIKLI ve yetkinlikle birebir uyumlu gelişim adımları öner.)"",
    ""KİŞİSEL KAPANIŞ NOTU"": ""... (Profesyonel, teşvik edici bir kapanış cümlesi.)""
  }}
}}
<|im_end|>
<|im_start|>user
Birim: {safeDept}
Genel Performans Puanı: {safeFinalScore} / 5

GÜÇLÜ YÖNLER (Bu konularda yüksek standart sağlandı): 
{strengthsStr}

GELİŞİM ALANLARI VE ZAYIF DAVRANIŞLAR (Listedeki tüm yetkinlikler için uygun ve mantıklı aksiyonlar düşün): 
{devStr}
<|im_end|>
<|im_start|>assistant
";
		}

		private static string GetQuestionText(DataRow row)
		{
			string[] possibleCols = { "Soru", "SoruMetni", "Question", "QuestionText", "Soru_Metni", "Gosterge", "AltYetkinlik", "Icerik" };
			foreach (var c in possibleCols)
			{
				if (row.Table.Columns.Contains(c) && row[c] != DBNull.Value)
					return row[c].ToString().Trim();
			}
			return "";
		}

		private static string GetWeakestQuestions(DataTable dtSorular, string competencyName)
		{
			if (dtSorular == null) return "";
			var list = new List<Tuple<string, double>>();
			foreach (DataRow r in dtSorular.Rows)
			{
				string comp = r.Table.Columns.Contains("CompetencyName") ? r["CompetencyName"]?.ToString()?.Trim() : "";
				if (!string.Equals(comp, competencyName, StringComparison.OrdinalIgnoreCase)) continue;

				double score = ToDoubleSafe(r.Table.Columns.Contains("WeightedScore") ? r["WeightedScore"] : null);
				string qText = GetQuestionText(r);
				if (!string.IsNullOrWhiteSpace(qText))
					list.Add(Tuple.Create(qText, score));
			}
			return string.Join("; ", list.OrderBy(x => x.Item2).Take(2).Select(x => x.Item1));
		}

		private static string ExtractCommentary(string json, double score)
		{
			try
			{
				string candidate = ExtractJsonObject(json);
				string reportText = TryExtractReportText(candidate);
				if (!string.IsNullOrWhiteSpace(reportText)) return reportText.Trim();

				var response = DeserializeJson<NarrativeResponseV3>(candidate);
				if (response != null && !string.IsNullOrWhiteSpace(response.Commentary))
					return response.Commentary.Trim();
			}
			catch { }

			string fallback = (json ?? "").Trim();
			return fallback.Length > 50 ? fallback : $"{score}/5 performans endeksi ile değerlendirme tamamlanmıştır.";
		}

		private static string TryExtractReportText(string candidateJson)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(candidateJson)) return null;
				var root = JObject.Parse(candidateJson);
				var commentary = root["commentary"];
				if (commentary == null) return null;

				if (commentary.Type == JTokenType.String)
				{
					return NormalizeParagraph(commentary.ToString());
				}

				if (commentary.Type == JTokenType.Object)
				{
					var obj = (JObject)commentary;
					string[] orderedKeys = {
						"BİRİMDEKİ ROLÜNÜZ VE BAŞARILARINIZ",
						"KRİTİK YETKİNLİK ANALİZİ",
						"ŞİRKET KARŞILAŞTIRMASI VE KONUMLANMA",
						"ODAKLANILACAK GELİŞİM ALANLARI",
						"SİZİN İÇİN GELİŞİM FIRSATLARI",
						"KİŞİSEL KAPANIŞ NOTU"
					};

					var sb = new StringBuilder();
					foreach (string k in orderedKeys)
					{
						var matchingKey = ((IDictionary<string, JToken>)obj).Keys
							.FirstOrDefault(key => key.Replace("*", "").Trim().Equals(k, StringComparison.OrdinalIgnoreCase));

						string val = matchingKey != null ? (obj[matchingKey]?.ToString() ?? "").Trim() : "";
						if (string.IsNullOrWhiteSpace(val)) continue;

						if (sb.Length > 0) sb.Append("\n\n");
						sb.Append(k).Append("\n");
						sb.Append(NormalizeParagraph(val));
					}
					return sb.ToString().Trim();
				}
				return NormalizeParagraph(commentary.ToString());
			}
			catch { return null; }
		}

		private static string ExtractJsonObject(string s)
		{
			s = (s ?? "").Trim();
			if (string.IsNullOrWhiteSpace(s)) return "{}";
			int first = s.IndexOf('{');
			int last = s.LastIndexOf('}');
			return (first >= 0 && last > first) ? s.Substring(first, last - first + 1) : s;
		}

		private static string SanitizeForPrompt(string input)
		{
			if (string.IsNullOrWhiteSpace(input)) return string.Empty;
			return input.Replace("<|im_start|>", "").Replace("<|im_end|>", "").Replace("<|", "").Trim();
		}

		private static string NormalizeParagraph(string s)
		{
			s = (s ?? "").Replace("\r\n", "\n").Replace("\r", "\n");
			while (s.Contains("\n\n\n")) s = s.Replace("\n\n\n", "\n\n");
			// Model inatla madde işareti koyarsa diye temizlik
			return s.Replace("- ", "").Trim();
		}

		private static double ToDoubleSafe(object o)
		{
			try
			{
				if (o == null || o == DBNull.Value) return 0;
				if (o is double d) return d;
				string s = o.ToString();
				if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out double inv)) return inv;
				if (double.TryParse(s, NumberStyles.Any, new CultureInfo("tr-TR"), out double tr)) return tr;
				return 0;
			}
			catch { return 0; }
		}

		private static T DeserializeJson<T>(string json)
		{
			var ser = new DataContractJsonSerializer(typeof(T), new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true });
			using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(json ?? "")))
				return (T)ser.ReadObject(ms);
		}

		[DataContract]
		private sealed class NarrativeResponseV3
		{
			[DataMember(Name = "commentary")]
			public string Commentary { get; set; }
		}
	}
}