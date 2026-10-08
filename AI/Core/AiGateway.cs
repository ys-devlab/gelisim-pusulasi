using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace WindowsFormsApp1.AI.Core
{
    /// <summary>
    /// Tüm AI çağrılarının geçtiği tek nokta. Yapılandırmadan sağlayıcı/model/endpoint çözer,
    /// çağrıyı yürütür, süre + token sayar, maliyeti hesaplar ve <c>AI_UsageLog</c> tablosuna loglar.
    /// Loglama her zaman best-effort'tur ve asıl akışı asla bozmaz.
    /// </summary>
    public static class AiGateway
    {
        private static readonly OpenRouterProvider _openRouter = new OpenRouterProvider();
        private static readonly OllamaProvider _ollama = new OllamaProvider();
        private static readonly AiUsageLogRepository _logRepo = new AiUsageLogRepository();
        private static readonly AiModelPricingRepository _pricingRepo = new AiModelPricingRepository();
        private static readonly OpenRouterPricingService _pricingService = new OpenRouterPricingService();

        private static readonly object _schemaLock = new object();
        private static bool _schemaEnsured;

        // Canlı fiyat tazeleme: process başına bir kez başarı; başarısızsa en sık 10 dk'da bir tekrar dener.
        private static readonly SemaphoreSlim _pricingGate = new SemaphoreSlim(1, 1);
        private static bool _pricingRefreshed;
        private static DateTime _lastPricingAttemptUtc = DateTime.MinValue;

        /// <summary>Bir görev için çözülmüş hedefi döner (çağıranlar cache anahtarı için kullanır).</summary>
        public static ResolvedAiTarget Resolve(AiPurpose purpose)
        {
            return AiSettings.Resolve(purpose);
        }

        /// <summary>AI çağrısını yürütür, loglar ve sonucu döner. Hata olursa loglayıp yeniden fırlatır.</summary>
        public static async Task<AiResult> GenerateAsync(
            AiPurpose purpose,
            AiPrompt prompt,
            string contextKey = null,
            Action<string> log = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (prompt == null) throw new ArgumentNullException(nameof(prompt));

            ResolvedAiTarget target = AiSettings.Resolve(purpose);
            IAiProvider provider = target.Provider == AiProvider.Ollama ? (IAiProvider)_ollama : _openRouter;

            if (!prompt.Seed.HasValue) prompt.Seed = target.Seed;
            if (!prompt.Temperature.HasValue) prompt.Temperature = target.Temperature;

            var sw = Stopwatch.StartNew();
            AiResult result = null;
            Exception failure = null;

            try
            {
                result = await provider.GenerateAsync(target, prompt, log, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                failure = ex;
                result = new AiResult { Success = false, Error = ex.Message, Text = string.Empty };
            }
            finally
            {
                sw.Stop();
            }

            await TryLogAsync(purpose, target, result, sw.ElapsedMilliseconds, contextKey, cancellationToken).ConfigureAwait(false);

            if (failure != null) throw failure; // Mevcut davranışı koru: çağıran hatayı görür

            return result;
        }

        /// <summary>Kısayol: yalnızca metin döner.</summary>
        public static async Task<string> GenerateTextAsync(
            AiPurpose purpose,
            AiPrompt prompt,
            string contextKey = null,
            Action<string> log = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            AiResult r = await GenerateAsync(purpose, prompt, contextKey, log, cancellationToken).ConfigureAwait(false);
            return r?.Text ?? string.Empty;
        }

        /// <summary>OpenRouter model fiyatlarını canlı API'den tabloya zorla tazeler. Eklenen kayıt sayısını döner.</summary>
        public static async Task<int> RefreshPricingAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            EnsureSchemaOnce();
            string apiKey = AiSettings.OpenRouterApiKey;
            int n = await _pricingService.RefreshAsync(_pricingRepo, apiKey, cancellationToken).ConfigureAwait(false);
            _pricingRefreshed = true;
            return n;
        }

        private static async Task TryLogAsync(AiPurpose purpose, ResolvedAiTarget target, AiResult result, long latencyMs, string contextKey, CancellationToken cancellationToken)
        {
            try
            {
                EnsureSchemaOnce();

                decimal inputCost = 0m;
                decimal outputCost = 0m;
                try
                {
                    // OpenRouter modeli için fiyatları canlı API'den (bir kez) doldur.
                    if (target.Provider == AiProvider.OpenRouter)
                        await EnsurePricingRefreshedAsync(target.ApiKey).ConfigureAwait(false);

                    ModelPrice price = _pricingRepo.GetPrice(target.Model);
                    inputCost = (result.PromptTokens / 1000000m) * price.InputUsdPer1M;
                    outputCost = (result.CompletionTokens / 1000000m) * price.OutputUsdPer1M;
                }
                catch { /* fiyat bulunamazsa 0 */ }

                _logRepo.Insert(new AiUsageRecord
                {
                    Provider = target.ProviderName,
                    Model = target.Model,
                    Endpoint = target.Endpoint,
                    Purpose = purpose.ToString(),
                    PromptTokens = result.PromptTokens,
                    CompletionTokens = result.CompletionTokens,
                    TotalTokens = result.TotalTokens,
                    LatencyMs = latencyMs,
                    InputCostUsd = inputCost,
                    OutputCostUsd = outputCost,
                    EstimatedCostUsd = inputCost + outputCost,
                    Success = result.Success,
                    ErrorMessage = result.Success ? null : Trim(result.Error, 3900),
                    ContextKey = Trim(contextKey, 200)
                });
            }
            catch
            {
                // Loglama asla asıl akışı bozmaz.
            }
        }

        private static void EnsureSchemaOnce()
        {
            if (_schemaEnsured) return;
            lock (_schemaLock)
            {
                if (_schemaEnsured) return;
                _logRepo.EnsureSchema();
                _pricingRepo.EnsureSchema();
                _schemaEnsured = true;
            }
        }

        private static async Task EnsurePricingRefreshedAsync(string apiKey)
        {
            if (_pricingRefreshed) return;
            if ((DateTime.UtcNow - _lastPricingAttemptUtc).TotalMinutes < 10) return;

            await _pricingGate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_pricingRefreshed) return;
                if ((DateTime.UtcNow - _lastPricingAttemptUtc).TotalMinutes < 10) return;

                _lastPricingAttemptUtc = DateTime.UtcNow;
                await _pricingService.RefreshAsync(_pricingRepo, apiKey, CancellationToken.None).ConfigureAwait(false);
                _pricingRefreshed = true; // yalnızca başarıda kalıcı olarak işaretle
            }
            finally
            {
                _pricingGate.Release();
            }
        }

        private static string Trim(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Length <= max ? s : s.Substring(0, max);
        }
    }
}
