using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace WindowsFormsApp1.AI.Core
{
    /// <summary>Sağlayıcıya gönderilen istek.</summary>
    public sealed class AiPrompt
    {
        public string User { get; set; }
        public string System { get; set; }
        public bool RequireJson { get; set; }
        public int? Seed { get; set; }
        public double? Temperature { get; set; }
    }

    /// <summary>Sağlayıcıdan dönen sonuç + kullanım metrikleri.</summary>
    public sealed class AiResult
    {
        public string Text { get; set; }
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        public int TotalTokens { get; set; }
        public bool Success { get; set; }
        public string Error { get; set; }
    }

    public interface IAiProvider
    {
        Task<AiResult> GenerateAsync(ResolvedAiTarget target, AiPrompt prompt, Action<string> log, CancellationToken cancellationToken);
    }

    /// <summary>
    /// OpenRouter (OpenAI uyumlu /chat/completions) sağlayıcısı. Yanıttaki <c>usage</c> alanından
    /// token sayılarını okur. Geçici hatalarda (5xx/429) üstel geri çekilmeyle yeniden dener.
    /// </summary>
    public sealed class OpenRouterProvider : IAiProvider
    {
        private static readonly HttpClient _httpClient = CreateClient();

        private static HttpClient CreateClient()
        {
            var c = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            return c;
        }

        public async Task<AiResult> GenerateAsync(ResolvedAiTarget target, AiPrompt prompt, Action<string> log, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(target.ApiKey))
                throw new InvalidOperationException("OpenRouter API anahtarı yapılandırılmamış. ai.env içindeki OPENROUTER_API_KEY değerini doldurun.");

            var messages = new List<object>();
            if (!string.IsNullOrWhiteSpace(prompt.System))
                messages.Add(new { role = "system", content = prompt.System });
            messages.Add(new { role = "user", content = prompt.User ?? "" });

            var requestBody = new
            {
                model = target.Model,
                messages = messages,
                temperature = prompt.RequireJson ? 0.0 : (prompt.Temperature ?? 0.2),
                response_format = prompt.RequireJson ? new { type = "json_object" } : null
            };

            string json = JsonConvert.SerializeObject(requestBody, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

            int maxRetries = 3;
            int delayMs = 2000;

            for (int i = 0; i < maxRetries; i++)
            {
                log?.Invoke($"OpenRouter isteği gönderiliyor ({target.Model}, deneme {i + 1})...");

                var request = new HttpRequestMessage(HttpMethod.Post, target.Endpoint);
                request.Headers.Add("Authorization", "Bearer " + target.ApiKey);
                request.Headers.Add("HTTP-Referer", "https://github.com/google-deepmind/antigravity");
                request.Headers.Add("X-OpenRouter-Title", "LiftUp 360 Windows App");
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                string responseString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    JObject resultObj;
                    try { resultObj = JObject.Parse(responseString); }
                    catch (JsonException) { throw new Exception("OpenRouter yanıtı JSON olarak çözümlenemedi. Yanıt: " + responseString); }

                    JToken contentToken = resultObj.SelectToken("$.choices[0].message.content");
                    if (contentToken != null)
                    {
                        var result = new AiResult
                        {
                            Text = contentToken.ToString(),
                            Success = true
                        };
                        ReadUsage(resultObj, result);
                        return result;
                    }

                    JToken errorToken = resultObj.SelectToken("$.error.message") ?? resultObj.SelectToken("$.error");
                    if (errorToken != null)
                    {
                        if (i == maxRetries - 1) throw new Exception("OpenRouter Hatası: " + errorToken);
                        log?.Invoke("OpenRouter Hatası: " + errorToken + ". Tekrar deneniyor...");
                        await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
                        delayMs *= 2;
                        continue;
                    }

                    throw new Exception("OpenRouter yanıtında beklenen veri bulunamadı. Yanıt: " + responseString);
                }

                int statusCode = (int)response.StatusCode;
                if (statusCode == 503 || statusCode == 429 || statusCode == 504 || statusCode == 502 || statusCode == 500)
                {
                    if (i == maxRetries - 1)
                        throw new Exception("OpenRouter geçici hata kalıcı oldu. HTTP " + statusCode + ". DETAY: " + responseString);

                    log?.Invoke("Geçici API hatası (HTTP " + statusCode + "), " +
                                (delayMs / 1000.0).ToString("0.0", CultureInfo.InvariantCulture) + " sn sonra tekrar denenecek.");
                    await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
                    delayMs *= 2;
                    continue;
                }

                throw new Exception("OpenRouter API Hatası (" + statusCode + "): " + responseString);
            }

            return new AiResult { Text = string.Empty, Success = false, Error = "Yanıt alınamadı." };
        }

        private static void ReadUsage(JObject root, AiResult result)
        {
            try
            {
                JToken usage = root.SelectToken("$.usage");
                if (usage == null) return;
                result.PromptTokens = (int?)usage.SelectToken("prompt_tokens") ?? 0;
                result.CompletionTokens = (int?)usage.SelectToken("completion_tokens") ?? 0;
                result.TotalTokens = (int?)usage.SelectToken("total_tokens")
                                     ?? (result.PromptTokens + result.CompletionTokens);
            }
            catch { /* token metrikleri zorunlu değil */ }
        }
    }

    /// <summary>
    /// Ollama (yerel) sağlayıcısı; <c>/api/generate</c> uç noktasını kullanır. Yanıttaki
    /// <c>prompt_eval_count</c>/<c>eval_count</c> alanlarından token sayılarını okur.
    /// </summary>
    public sealed class OllamaProvider : IAiProvider
    {
        public async Task<AiResult> GenerateAsync(ResolvedAiTarget target, AiPrompt prompt, Action<string> log, CancellationToken cancellationToken)
        {
            string fullPrompt = string.IsNullOrWhiteSpace(prompt.System)
                ? (prompt.User ?? "")
                : prompt.System.Trim() + "\n\n" + (prompt.User ?? "");

            var body = new
            {
                model = target.Model,
                prompt = fullPrompt,
                stream = false,
                format = prompt.RequireJson ? "json" : null,
                options = new
                {
                    temperature = prompt.Temperature ?? target.Temperature,
                    seed = prompt.Seed ?? target.Seed
                }
            };

            string json = JsonConvert.SerializeObject(body, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

            using (var http = new HttpClient { BaseAddress = new Uri(target.Endpoint), Timeout = TimeSpan.FromMinutes(10) })
            {
                log?.Invoke($"Ollama isteği gönderiliyor ({target.Model})...");

                var content = new StringContent(json, Encoding.UTF8, "application/json");
                HttpResponseMessage response = await http.PostAsync("/api/generate", content, cancellationToken).ConfigureAwait(false);
                string responseString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                    throw new Exception("Ollama API Hatası (" + (int)response.StatusCode + "): " + responseString);

                JObject root;
                try { root = JObject.Parse(responseString); }
                catch (JsonException) { throw new Exception("Ollama yanıtı JSON olarak çözümlenemedi. Yanıt: " + responseString); }

                var result = new AiResult
                {
                    Text = root.SelectToken("response")?.ToString() ?? "",
                    Success = true,
                    PromptTokens = (int?)root.SelectToken("prompt_eval_count") ?? 0,
                    CompletionTokens = (int?)root.SelectToken("eval_count") ?? 0
                };
                result.TotalTokens = result.PromptTokens + result.CompletionTokens;
                return result;
            }
        }
    }
}
