using System;
using System.Globalization;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace WindowsFormsApp1.AI.Core
{
    /// <summary>
    /// OpenRouter'ın public <c>/api/v1/models</c> uç noktasından model fiyatlarını çeker ve
    /// <see cref="AiModelPricingRepository"/> tablosuna (yalnızca eksik olanları) doldurur.
    /// OpenRouter fiyatları token başına USD'dir; 1M token başına fiyata çevrilir (× 1.000.000).
    /// </summary>
    public sealed class OpenRouterPricingService
    {
        private const string DefaultModelsUrl = "https://openrouter.ai/api/v1/models";
        private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        /// <summary>
        /// Tüm OpenRouter modellerinin fiyatlarını çeker ve eksik olanları tabloya ekler.
        /// Eklenen model sayısını döner. Hata olursa istisna fırlatır (çağıran best-effort sarar).
        /// </summary>
        public async Task<int> RefreshAsync(AiModelPricingRepository repo, string apiKey, CancellationToken cancellationToken)
        {
            if (repo == null) throw new ArgumentNullException(nameof(repo));

            string url = AiConfig.Get("OPENROUTER_MODELS_URL", DefaultModelsUrl);

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (!string.IsNullOrWhiteSpace(apiKey))
                request.Headers.Add("Authorization", "Bearer " + apiKey.Trim());

            HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            JObject root = JObject.Parse(body);
            JArray data = root["data"] as JArray;
            if (data == null) return 0;

            int count = 0;
            foreach (JToken item in data)
            {
                string id = item["id"]?.ToString();
                if (string.IsNullOrWhiteSpace(id)) continue;

                JToken pricing = item["pricing"];
                if (pricing == null) continue;

                decimal inputPer1M = PerMillion(pricing["prompt"]);
                decimal outputPer1M = PerMillion(pricing["completion"]);

                repo.InsertIfMissing(id, inputPer1M, outputPer1M);
                count++;
            }

            return count;
        }

        // OpenRouter: token başına USD (string). 1M token başına = değer × 1.000.000.
        private static decimal PerMillion(JToken token)
        {
            if (token == null) return 0m;
            string s = token.ToString();
            if (string.IsNullOrWhiteSpace(s)) return 0m;
            if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal perToken))
                return perToken * 1000000m;
            return 0m;
        }
    }
}
