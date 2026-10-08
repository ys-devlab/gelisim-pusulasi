using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WindowsFormsApp1.AI.Core;

namespace WindowsFormsApp1.AI
{
    public sealed class QwenCompetencyWeightService
    {
        private readonly AICompetencyWeightRepository _repo;
        private readonly Func<OllamaClient> _clientFactory;

        public string ModelName { get; }
        public int Seed { get; }
        public double Temperature { get; }

        public QwenCompetencyWeightService(
            AICompetencyWeightRepository repo,
            Func<OllamaClient> clientFactory,
            string modelName = "qwen2.5:7b",
            int seed = 42,
            double temperature = 0.0)
        {
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));
            _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
            ModelName = string.IsNullOrWhiteSpace(modelName) ? "qwen2.5:7b" : modelName;
            Seed = seed;
            Temperature = temperature;
        }

        public async Task<IReadOnlyDictionary<string, double>> GetOrCreateWeightsAsync(
            string title,
            string department,
            IEnumerable<string> competencyNames,
            CancellationToken cancellationToken)
        {
            title = (title ?? "").Trim();
            department = (department ?? "").Trim();
            if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Unvan boş olamaz.", nameof(title));
            if (string.IsNullOrWhiteSpace(department)) throw new ArgumentException("Birim boş olamaz.", nameof(department));

            var comps = (competencyNames ?? Enumerable.Empty<string>())
                .Select(x => (x ?? "").Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (comps.Count == 0) return new Dictionary<string, double>();

            _repo.EnsureSchema();

            // Model/seed/temperature artık tek noktadan (ai.env + AiGateway) çözülür.
            // Cache anahtarı, gerçekte kullanılacak modelle senkron kalsın diye çözülmüş değerleri kullanır.
            var target = AiGateway.Resolve(AiPurpose.Weights);
            string model = target.Model;
            int seed = target.Seed;
            double temperature = target.Temperature;

            string cachedJson = _repo.TryGetWeightsJson(title, department, model, seed, temperature);
            if (!string.IsNullOrWhiteSpace(cachedJson))
            {
                var cached = TryParseWeights(cachedJson);
                if (cached != null && cached.Count > 0)
                {
                    return NormalizeAndFill(cached, comps);
                }
            }

            string prompt = BuildWeightPrompt(title, department, comps);
            string responseJson = await AiGateway.GenerateTextAsync(
                AiPurpose.Weights,
                new AiPrompt { User = prompt, RequireJson = true, Seed = seed, Temperature = temperature },
                contextKey: title + " | " + department,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            var weights = TryParseWeights(responseJson);
            if (weights == null || weights.Count == 0)
            {
                throw new InvalidOperationException("Ağırlıklar üretildi fakat parse edilemedi.");
            }

            var normalized = NormalizeAndFill(weights, comps);

            // Cache'e normalize edilmiş ağırlıkları yazıyoruz (yeniden çağırmadan birebir aynı kalsın)
            string normalizedJson = SerializeJson(new WeightsResponse { Weights = normalized.ToDictionary(k => k.Key, v => v.Value) });
            _repo.UpsertWeightsJson(title, department, model, seed, temperature, normalizedJson);

            return normalized;
        }

        private static string BuildWeightPrompt(string title, string department, List<string> competencyNames)
        {
            string compList = string.Join("\n", competencyNames.Select(c => "- " + c));
            return
@"Sen bir İK performans değerlendirme sisteminde deterministik ağırlık atayan bir yardımcı modelsin.

Görev: Aşağıdaki yetkinliklerin her biri için 0 ile 1 arasında (dahil) ondalıklı bir ağırlık katsayısı üret.

Kritik kurallar:
- ÇIKTI SADECE JSON olacak. Açıklama, Markdown, önsöz, arka söz yok.
- JSON şeması tam olarak şu olacak:
  { ""weights"": { ""YetkinlikAdı1"": 0.00, ""YetkinlikAdı2"": 0.00, ... } }
- weights altında SADECE aşağıdaki yetkinlik adları yer alacak; ek anahtar üretme.
- Her yetkinliğin değeri 0 ile 1 arasında olacak.

Bağlam:
- Unvan: " + EscapeForPrompt(title) + @"
- Birim: " + EscapeForPrompt(department) + @"

Yetkinlikler:
" + compList + @"
";
        }

        private static string EscapeForPrompt(string s)
        {
            return (s ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
        }

        private static Dictionary<string, double> NormalizeAndFill(Dictionary<string, double> raw, List<string> expectedCompetencies)
        {
            var result = new Dictionary<string, double>(StringComparer.Ordinal);

            foreach (string c in expectedCompetencies)
            {
                if (raw.TryGetValue(c, out double v))
                {
                    result[c] = Clamp01(v);
                }
                else
                {
                    result[c] = 0.5; // deterministic fallback for missing keys
                }
            }

            return result;
        }

        private static double Clamp01(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return 0;
            if (v < 0) return 0;
            if (v > 1) return 1;
            return v;
        }

        private static Dictionary<string, double> TryParseWeights(string json)
        {
            try
            {
                var parsed = DeserializeJson<WeightsResponse>(json);
                if (parsed == null || parsed.Weights == null) return null;

                // DataContractJsonSerializer dictionary parsing can be sensitive; normalize keys here.
                var dict = new Dictionary<string, double>(StringComparer.Ordinal);
                foreach (var kv in parsed.Weights)
                {
                    string k = (kv.Key ?? "").Trim();
                    if (string.IsNullOrWhiteSpace(k)) continue;
                    dict[k] = kv.Value;
                }
                return dict;
            }
            catch
            {
                return null;
            }
        }

        private static T DeserializeJson<T>(string json)
        {
            var ser = new DataContractJsonSerializer(typeof(T), new DataContractJsonSerializerSettings
            {
                UseSimpleDictionaryFormat = true
            });
            using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(json ?? "")))
            {
                return (T)ser.ReadObject(ms);
            }
        }

        private static string SerializeJson<T>(T obj)
        {
            var ser = new DataContractJsonSerializer(typeof(T), new DataContractJsonSerializerSettings
            {
                UseSimpleDictionaryFormat = true
            });
            using (var ms = new MemoryStream())
            {
                ser.WriteObject(ms, obj);
                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }

        [DataContract]
        private sealed class WeightsResponse
        {
            [DataMember(Name = "weights", IsRequired = false)]
            public Dictionary<string, double> Weights { get; set; }
        }
    }
}

