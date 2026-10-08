using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace WindowsFormsApp1.AI.Core
{
    /// <summary>Desteklenen AI sağlayıcıları.</summary>
    public enum AiProvider
    {
        OpenRouter,
        Ollama
    }

    /// <summary>
    /// Sistemdeki AI çağrı türleri. Her tür için .env üzerinden ayrı sağlayıcı/model override edilebilir
    /// (örn. AI_NARRATIVE_PROVIDER=ollama, AI_WEIGHTS_MODEL=llama3.1:8b).
    /// </summary>
    public enum AiPurpose
    {
        Weights,
        Narrative,
        Chat,
        ReminderMail,
        Optimizer,
        Heatmap,
        DepartmentWeights
    }

    /// <summary>
    /// Tek bir AI çağrısı için çözümlenmiş hedef: hangi sağlayıcı, hangi model, hangi endpoint.
    /// </summary>
    public sealed class ResolvedAiTarget
    {
        public AiProvider Provider { get; set; }
        public string ProviderName { get; set; }   // "openrouter" | "ollama" (loglama için)
        public string Model { get; set; }
        public string Endpoint { get; set; }
        public string ApiKey { get; set; }          // yalnızca OpenRouter için
        public int Seed { get; set; }
        public double Temperature { get; set; }
    }

    /// <summary>
    /// Basit .env tarzı yapılandırma okuyucu. Uygulama klasöründeki <c>ai.env</c> dosyasını
    /// (bulunamazsa ortam değişkenlerini) okur. Tüm AI sağlayıcı/model/endpoint seçimi buradan yönetilir.
    /// </summary>
    public static class AiConfig
    {
        public const string EnvFileName = "ai.env";

        private static readonly object _lock = new object();
        private static Dictionary<string, string> _values;

        private static Dictionary<string, string> Values
        {
            get
            {
                if (_values == null)
                {
                    lock (_lock)
                    {
                        if (_values == null) _values = Load();
                    }
                }
                return _values;
            }
        }

        /// <summary>ai.env dosyasını yeniden okur (çalışma anında değiştirilirse).</summary>
        public static void Reload()
        {
            lock (_lock) { _values = Load(); }
        }

        private static Dictionary<string, string> Load()
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string path in CandidatePaths())
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) continue;

                try
                {
                    foreach (string raw in File.ReadAllLines(path))
                    {
                        string line = (raw ?? "").Trim();
                        if (line.Length == 0 || line.StartsWith("#")) continue;

                        int eq = line.IndexOf('=');
                        if (eq <= 0) continue;

                        string key = line.Substring(0, eq).Trim();
                        string val = line.Substring(eq + 1).Trim();

                        // Çevreleyen tırnakları temizle
                        if (val.Length >= 2 &&
                            ((val[0] == '"' && val[val.Length - 1] == '"') ||
                             (val[0] == '\'' && val[val.Length - 1] == '\'')))
                        {
                            val = val.Substring(1, val.Length - 2);
                        }

                        if (key.Length > 0) dict[key] = val;
                    }
                }
                catch
                {
                    // Bozuk/erişilemeyen dosya: sessizce ortam değişkenlerine düş
                }

                break; // İlk bulunan ai.env kazanır
            }
            return dict;
        }

        private static List<string> CandidatePaths()
        {
            var list = new List<string>();
            string baseDir = AppDomain.CurrentDomain.BaseDirectory ?? "";

            if (!string.IsNullOrWhiteSpace(baseDir))
            {
                list.Add(Path.Combine(baseDir, EnvFileName));
                // Proje kökü: bin\Debug -> ..\..
                try { list.Add(Path.GetFullPath(Path.Combine(baseDir, "..", "..", EnvFileName))); }
                catch { /* yoksay */ }
            }

            try { list.Add(Path.Combine(Directory.GetCurrentDirectory(), EnvFileName)); }
            catch { /* yoksay */ }

            return list;
        }

        public static string Get(string key, string fallback = null)
        {
            if (string.IsNullOrWhiteSpace(key)) return fallback;

            if (Values.TryGetValue(key, out string v) && !string.IsNullOrWhiteSpace(v))
                return v;

            string env = Environment.GetEnvironmentVariable(key);
            return string.IsNullOrWhiteSpace(env) ? fallback : env;
        }

        public static int GetInt(string key, int fallback)
        {
            string s = Get(key, null);
            if (!string.IsNullOrWhiteSpace(s) &&
                int.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v))
            {
                return v;
            }
            return fallback;
        }

        public static double GetDouble(string key, double fallback)
        {
            string s = Get(key, null);
            if (!string.IsNullOrWhiteSpace(s) &&
                double.TryParse(s.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double v))
            {
                return v;
            }
            return fallback;
        }
    }

    /// <summary>
    /// <see cref="AiConfig"/> üzerine kurulu, türü güçlü ayar görünümü. Asıl iş <see cref="Resolve"/>:
    /// global sağlayıcı + isteğe bağlı görev-bazlı override'ı birleştirip tek bir hedef üretir.
    /// </summary>
    public static class AiSettings
    {
        private const string DefaultOpenRouterModel = "google/gemini-2.5-flash";
        private const string DefaultOpenRouterUrl = "https://openrouter.ai/api/v1/chat/completions";
        private const string DefaultOllamaModel = "qwen2.5:7b";
        private const string DefaultOllamaUrl = "http://localhost:11434";

        public static string OpenRouterApiKey => AiConfig.Get("OPENROUTER_API_KEY", "");
        public static string OpenRouterBaseUrl => AiConfig.Get("OPENROUTER_BASE_URL", DefaultOpenRouterUrl);
        public static string OpenRouterModel => AiConfig.Get("OPENROUTER_MODEL", DefaultOpenRouterModel);
        public static string OllamaBaseUrl => AiConfig.Get("OLLAMA_BASE_URL", DefaultOllamaUrl);
        public static string OllamaModel => AiConfig.Get("OLLAMA_MODEL", DefaultOllamaModel);
        public static int Seed => AiConfig.GetInt("AI_SEED", 42);
        public static double Temperature => AiConfig.GetDouble("AI_TEMPERATURE", 0.0);

        /// <summary>Genel (global) sağlayıcı; görev override'ı yoksa bu kullanılır.</summary>
        public static AiProvider GlobalProvider => ParseProvider(AiConfig.Get("AI_PROVIDER", "openrouter"));

        public static ResolvedAiTarget Resolve(AiPurpose purpose)
        {
            string purposeToken = purpose.ToString().ToUpperInvariant(); // WEIGHTS, NARRATIVE, ...

            // Sağlayıcı: önce görev override (AI_<PURPOSE>_PROVIDER), yoksa global
            string providerOverride = AiConfig.Get("AI_" + purposeToken + "_PROVIDER", null);
            AiProvider provider = string.IsNullOrWhiteSpace(providerOverride)
                ? GlobalProvider
                : ParseProvider(providerOverride);

            // Model: önce görev override (AI_<PURPOSE>_MODEL), yoksa sağlayıcı varsayılanı
            string modelOverride = AiConfig.Get("AI_" + purposeToken + "_MODEL", null);

            var target = new ResolvedAiTarget
            {
                Provider = provider,
                Seed = Seed,
                Temperature = Temperature
            };

            if (provider == AiProvider.Ollama)
            {
                target.ProviderName = "ollama";
                target.Endpoint = OllamaBaseUrl;
                target.Model = string.IsNullOrWhiteSpace(modelOverride) ? OllamaModel : modelOverride.Trim();
                target.ApiKey = null;
            }
            else
            {
                target.ProviderName = "openrouter";
                target.Endpoint = OpenRouterBaseUrl;
                target.Model = string.IsNullOrWhiteSpace(modelOverride) ? OpenRouterModel : modelOverride.Trim();
                target.ApiKey = OpenRouterApiKey;
            }

            return target;
        }

        private static AiProvider ParseProvider(string s)
        {
            s = (s ?? "").Trim().ToLowerInvariant();
            if (s == "ollama" || s == "local") return AiProvider.Ollama;
            return AiProvider.OpenRouter;
        }
    }
}
