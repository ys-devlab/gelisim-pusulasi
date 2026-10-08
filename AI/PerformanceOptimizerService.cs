using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using WindowsFormsApp1.AI.Core;

namespace WindowsFormsApp1.AI
{
	public sealed class PerformanceOptimizerService
	{
		public static readonly string[] Competencies =
		{
			"Arıza Tespiti",
			"Duygusal Zeka ve Stres Yönetimi",
			"Dürüstlük-İş Ahlakı ve Sorumluluk",
			"İSG Uyumu",
			"Kalite ve Sürekli Gelişim Odaklılık",
			"Takım Çalışması ve İşbirliği",
			"Teknik Beceri"
		};

		private readonly Func<OllamaClient> _ollamaClientFactory;

		public PerformanceOptimizerService(Func<OllamaClient> ollamaClientFactory = null)
		{
			_ollamaClientFactory = ollamaClientFactory ?? (() => new OllamaClient("http://localhost:11434"));
		}

		public async Task<PerformanceOptimizationResult> OptimizeAsync(
			string birim,
			string unvan,
			IReadOnlyDictionary<string, double> genelOrtalamaScores1To5,
			string isiHaritasiVerisi,
			CancellationToken cancellationToken)
		{
			if (string.IsNullOrWhiteSpace(birim)) throw new ArgumentException("Birim boş olamaz.", nameof(birim));
			if (string.IsNullOrWhiteSpace(unvan)) throw new ArgumentException("Unvan boş olamaz.", nameof(unvan));
			if (genelOrtalamaScores1To5 == null) throw new ArgumentNullException(nameof(genelOrtalamaScores1To5));

			birim = birim.Trim();
			unvan = unvan.Trim();
			isiHaritasiVerisi = (isiHaritasiVerisi ?? "").Trim();

			var scores = NormalizeAndCompleteScores(genelOrtalamaScores1To5);

			Stage1WeightsResponse weights;
			try
			{
				weights = await Stage1_GetWeightsAsync(birim, unvan, cancellationToken).ConfigureAwait(false);
			}
			catch
			{
				weights = Stage1_MockWeights();
			}

			var stage2 = Stage2_Compute(scores, weights);

			string isiHaritasiYorumu;
			try
			{
				isiHaritasiYorumu = await Stage3_GenerateHeatmapCommentaryAsync(
					birim: birim,
					unvan: unvan,
					isiHaritasiVerisi: isiHaritasiVerisi,
					genelPuan5: stage2.AgirlikliGenelPuan5,
					enGucluYetkinlik: stage2.EnGucluYetkinlik,
					gucuPuan: stage2.EnGucluYetkinlikPuan,
					kaldiracYetkinlik: stage2.KaldiracYetkinligi,
					kaldiracPuan: stage2.KaldiracYetkinligiPuan,
					cancellationToken: cancellationToken).ConfigureAwait(false);
			}
			catch
			{
				isiHaritasiYorumu = Stage3_MockCommentary(birim, unvan, stage2);
			}

			return new PerformanceOptimizationResult
			{
				Birim = birim,
				Unvan = unvan,
				Agirliklar = weights,
				AgirlikliGenelPuan5 = stage2.AgirlikliGenelPuan5,
				EnGucluYetkinlik = stage2.EnGucluYetkinlik,
				KaldiracYetkinligi = stage2.KaldiracYetkinligi,
				IsiHaritasiYorumu = isiHaritasiYorumu
			};
		}

		private async Task<Stage1WeightsResponse> Stage1_GetWeightsAsync(string birim, string unvan, CancellationToken cancellationToken)
		{
			string prompt = $@"Sen savunma sanayisi (TUSAŞ) İK stratejistisin. {Escape(birim)} birimindeki bir {Escape(unvan)} için elimizdeki 7 yetkinliğin etki ağırlığını toplam 100 olacak şekilde dağıt. Sadece JSON dön.

Yetkinlikler (aynı isimlerle anahtar üret):
{string.Join(", ", Competencies.Select(Escape))}

Çıktı şeması (sadece JSON):
{{
  ""Agirliklar"": {{
    ""{Escape(Competencies[0])}"": 0,
    ""{Escape(Competencies[1])}"": 0,
    ""{Escape(Competencies[2])}"": 0,
    ""{Escape(Competencies[3])}"": 0,
    ""{Escape(Competencies[4])}"": 0,
    ""{Escape(Competencies[5])}"": 0,
    ""{Escape(Competencies[6])}"": 0
  }}
}}";

			string json = (await AiGateway.GenerateTextAsync(
				AiPurpose.Optimizer,
				new AiPrompt { User = prompt, RequireJson = true, Seed = 42, Temperature = 0.0 },
				contextKey: birim + " | " + unvan,
				cancellationToken: cancellationToken).ConfigureAwait(false) ?? "").Trim();

			var parsed = JsonConvert.DeserializeObject<Stage1WeightsResponse>(json);
			if (parsed == null || parsed.Agirliklar == null || parsed.Agirliklar.Count == 0)
				throw new InvalidOperationException("Ağırlık JSON parse edilemedi.");

			var normalized = new Dictionary<string, int>(StringComparer.Ordinal);
			foreach (string c in Competencies)
			{
				if (parsed.Agirliklar.TryGetValue(c, out int w)) normalized[c] = Math.Max(0, w);
				else normalized[c] = 0;
			}

			int sum = normalized.Values.Sum();
			if (sum != 100) normalized = NormalizeTo100(normalized);

			return new Stage1WeightsResponse { Agirliklar = normalized };
		}

		private static Stage1WeightsResponse Stage1_MockWeights()
		{
			var w = new Dictionary<string, int>(StringComparer.Ordinal);
			int baseW = 100 / Competencies.Length;
			int rem = 100 - (baseW * Competencies.Length);
			for (int i = 0; i < Competencies.Length; i++)
			{
				w[Competencies[i]] = baseW + (i == 0 ? rem : 0);
			}
			return new Stage1WeightsResponse { Agirliklar = w };
		}

		private static Stage2Result Stage2_Compute(IReadOnlyDictionary<string, double> scores1To5, Stage1WeightsResponse weights)
		{
			var w = (weights?.Agirliklar) ?? new Dictionary<string, int>(StringComparer.Ordinal);
			double weightedSum = 0.0;
			foreach (string c in Competencies)
			{
				double score = scores1To5.ContainsKey(c) ? scores1To5[c] : 0.0;
				int weight = w.ContainsKey(c) ? w[c] : 0;
				weightedSum += score * weight;
			}

			double genelPuan5 = Clamp(weightedSum / 100.0, 0, 5);
			string strongest = Competencies.OrderByDescending(c => scores1To5[c]).First();
			double strongestScore = scores1To5[strongest];

			string leverage = Competencies
				.Select(c => new { Competency = c, Roi = (5.0 - scores1To5[c]) * (w.ContainsKey(c) ? w[c] : 0) })
				.OrderByDescending(x => x.Roi).First().Competency;
			double leverageScore = scores1To5[leverage];

			return new Stage2Result
			{
				AgirlikliGenelPuan5 = genelPuan5,
				EnGucluYetkinlik = strongest,
				EnGucluYetkinlikPuan = strongestScore,
				KaldiracYetkinligi = leverage,
				KaldiracYetkinligiPuan = leverageScore
			};
		}

		private async Task<string> Stage3_GenerateHeatmapCommentaryAsync(
			string birim, string unvan, string isiHaritasiVerisi, double genelPuan5,
			string enGucluYetkinlik, double gucuPuan, string kaldiracYetkinlik, double kaldiracPuan, CancellationToken cancellationToken)
		{
			string prompt = $@"<|im_start|>system
Sen TUSAŞ (savunma sanayisi) için çalışan vizyoner, destekleyici ve kıdemli bir Organizasyonel Gelişim Stratejistisin.
Amacın, çalışanın performans verilerine bakarak akıcı, motive edici, kurumsal ve tek bir paragraftan oluşan bir yönetici özeti yazmaktır.
ASLA alt başlık, madde imi veya liste kullanma.<|im_end|>
<|im_start|>user
Şu örneği incele ve benimsemem gereken yazım tarzını anla:
'Üretim ekibinin değerli bir üyesi olarak, 'Takım Çalışması' yetkinliğindeki yüksek performansınız kurum kültürümüzle tam bir uyum içindedir ve 4.1/5'lik genel performans puanınızın temelini oluşturmaktadır. Sürekli gelişim vizyonumuz doğrultusunda, mevcut başarınızı daha da yukarı taşımak için 'Teknik Beceri' alanına odaklanmanızı öneriyoruz. Mevcut iletişim gücünüzü ve uyumunuzu bu teknik gelişimle desteklediğinizde, hem bireysel kariyer yolculuğunuzda hem de departman hedeflerimizde çok daha büyük ve kalıcı bir etki yaratacağınıza inanıyoruz.'

Şimdi GÖREVİN: Yukarıdaki destekleyici ve profesyonel üslubu baz alarak şu çalışan için tamamen özgün bir metin üret:
Çalışan Unvanı / Birimi: {Escape(unvan)} / {Escape(birim)}
Ağırlıklı Genel Puanı: {genelPuan5.ToString("0.0", CultureInfo.InvariantCulture)}/5
En Güçlü Yönü: {Escape(enGucluYetkinlik)} ({gucuPuan.ToString("0.0", CultureInfo.InvariantCulture)})
Kaldıraç Noktası (Gelişim İhtiyacı): {Escape(kaldiracYetkinlik)} ({kaldiracPuan.ToString("0.0", CultureInfo.InvariantCulture)})
Çalışanın Isı Haritası (Ham Puanları): {Escape(isiHaritasiVerisi)}

Lütfen çalışanın güçlü yönünü takdir eden ve kaldıraç noktasındaki (gelişim alanı) ilerlemenin yaratacağı etkiyi vurgulayan tek bir paragraf üret.<|im_end|>
<|im_start|>assistant
";

			string resp = await AiGateway.GenerateTextAsync(
				AiPurpose.Optimizer,
				new AiPrompt { User = prompt, RequireJson = false, Temperature = 0.4 },
				contextKey: birim + " | " + unvan,
				cancellationToken: cancellationToken).ConfigureAwait(false);
			return NormalizeParagraph(resp);
		}

		private static string Stage3_MockCommentary(string birim, string unvan, Stage2Result stage2)
		{
			return NormalizeParagraph($"Savunma sanayisinde {birim} gibi sıfır hata toleransının ve izlenebilirliğin belirleyici olduğu bir ekosistemde {unvan} rolünüzün en güçlü dayanağı {stage2.EnGucluYetkinlik} alanındaki istikrarınız olurken, operasyonel etki payı yüksek olan kaldıraç noktanız {stage2.KaldiracYetkinligi} tarafındaki gelişim ihtiyacı bu gücün sahaya yansımasını sınırlayabilir; algoritma, genel performansınızı {stage2.AgirlikliGenelPuan5.ToString("0.0", new CultureInfo("tr-TR"))}/5 seviyesinde normalize eder.");
		}

		private static IReadOnlyDictionary<string, double> NormalizeAndCompleteScores(IReadOnlyDictionary<string, double> input)
		{
			var dict = new Dictionary<string, double>(StringComparer.Ordinal);
			foreach (var kv in input)
			{
				string k = (kv.Key ?? "").Trim();
				if (string.IsNullOrWhiteSpace(k)) continue;
				dict[k] = Clamp(kv.Value, 0, 5);
			}
			foreach (string c in Competencies)
			{
				if (!dict.ContainsKey(c)) dict[c] = 0.0;
			}
			return dict;
		}

		private static Dictionary<string, int> NormalizeTo100(Dictionary<string, int> weights)
		{
			int sum = weights.Values.Sum();
			if (sum <= 0) return Stage1_MockWeights().Agirliklar;

			double factor = 100.0 / sum;
			var scaled = new Dictionary<string, int>(StringComparer.Ordinal);
			foreach (var kv in weights) scaled[kv.Key] = (int)Math.Round(kv.Value * factor, MidpointRounding.AwayFromZero);

			int newSum = scaled.Values.Sum();
			int diff = 100 - newSum;
			if (diff != 0 && scaled.Count > 0)
			{
				string key = scaled.OrderByDescending(x => x.Value).First().Key;
				scaled[key] = Math.Max(0, scaled[key] + diff);
			}
			return scaled;
		}

		private static double Clamp(double v, double min, double max)
		{
			if (double.IsNaN(v) || double.IsInfinity(v)) return min;
			if (v < min) return min;
			if (v > max) return max;
			return v;
		}

		private static string Escape(string s) => (s ?? "").Replace("\r", " ").Replace("\n", " ").Trim();

		private static string NormalizeParagraph(string s)
		{
			if (string.IsNullOrWhiteSpace(s)) return "";
			string t = s.Replace("\r\n", "\n").Replace("\r", "\n").Trim();
			t = string.Join(" ", t.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()));
			return t.Trim();
		}
	}

	public sealed class PerformanceOptimizationResult
	{
		public string Birim { get; set; }
		public string Unvan { get; set; }
		public Stage1WeightsResponse Agirliklar { get; set; }
		public double AgirlikliGenelPuan5 { get; set; }
		public string EnGucluYetkinlik { get; set; }
		public string KaldiracYetkinligi { get; set; }
		public string IsiHaritasiYorumu { get; set; }
	}

	public sealed class Stage1WeightsResponse
	{
		[JsonProperty("Agirliklar")]
		public Dictionary<string, int> Agirliklar { get; set; }
	}

	internal sealed class Stage2Result
	{
		public double AgirlikliGenelPuan5 { get; set; }
		public string EnGucluYetkinlik { get; set; }
		public double EnGucluYetkinlikPuan { get; set; }
		public string KaldiracYetkinligi { get; set; }
		public double KaldiracYetkinligiPuan { get; set; }
	}
}