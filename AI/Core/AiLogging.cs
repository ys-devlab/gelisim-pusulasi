using System;
using System.Data;
using System.Data.SqlClient;

namespace WindowsFormsApp1.AI.Core
{
    /// <summary>Tek bir AI çağrısının kullanım/performans/maliyet kaydı.</summary>
    public sealed class AiUsageRecord
    {
        public string Provider { get; set; }
        public string Model { get; set; }
        public string Endpoint { get; set; }
        public string Purpose { get; set; }
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        public int TotalTokens { get; set; }
        public long LatencyMs { get; set; }
        public decimal InputCostUsd { get; set; }
        public decimal OutputCostUsd { get; set; }
        public decimal EstimatedCostUsd { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public string ContextKey { get; set; }
    }

    /// <summary>
    /// <c>AI_UsageLog</c> tablosunu yönetir. Loglama her zaman "best-effort"tur: log yazımı
    /// başarısız olsa bile asıl AI akışını bozmaz (çağıran tarafça try/catch ile sarılır).
    /// </summary>
    public sealed class AiUsageLogRepository
    {
        public void EnsureSchema()
        {
            const string sql = @"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[AI_UsageLog]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[AI_UsageLog](
        [AI_UsageLogID] BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [CreatedAt] DATETIME NOT NULL CONSTRAINT [DF_AI_UsageLog_CreatedAt] DEFAULT(GETDATE()),
        [Provider] NVARCHAR(50) NOT NULL,
        [Model] NVARCHAR(120) NOT NULL,
        [Endpoint] NVARCHAR(300) NULL,
        [Purpose] NVARCHAR(50) NOT NULL,
        [PromptTokens] INT NOT NULL CONSTRAINT [DF_AI_UsageLog_Prompt] DEFAULT(0),
        [CompletionTokens] INT NOT NULL CONSTRAINT [DF_AI_UsageLog_Completion] DEFAULT(0),
        [TotalTokens] INT NOT NULL CONSTRAINT [DF_AI_UsageLog_Total] DEFAULT(0),
        [LatencyMs] INT NOT NULL CONSTRAINT [DF_AI_UsageLog_Latency] DEFAULT(0),
        [InputCostUsd] DECIMAL(18,6) NOT NULL CONSTRAINT [DF_AI_UsageLog_InCost] DEFAULT(0),
        [OutputCostUsd] DECIMAL(18,6) NOT NULL CONSTRAINT [DF_AI_UsageLog_OutCost] DEFAULT(0),
        [EstimatedCostUsd] DECIMAL(18,6) NOT NULL CONSTRAINT [DF_AI_UsageLog_Cost] DEFAULT(0),
        [Success] BIT NOT NULL CONSTRAINT [DF_AI_UsageLog_Success] DEFAULT(1),
        [ErrorMessage] NVARCHAR(MAX) NULL,
        [ContextKey] NVARCHAR(200) NULL
    );

    CREATE INDEX [IX_AI_UsageLog_CreatedAt] ON [dbo].[AI_UsageLog]([CreatedAt]);
    CREATE INDEX [IX_AI_UsageLog_Model] ON [dbo].[AI_UsageLog]([Provider],[Model]);
END

-- Mevcut tablolara (bu degisiklikten onceki) yeni maliyet sutunlarini ekle
IF COL_LENGTH('dbo.AI_UsageLog','InputCostUsd') IS NULL
    ALTER TABLE [dbo].[AI_UsageLog] ADD [InputCostUsd] DECIMAL(18,6) NOT NULL CONSTRAINT [DF_AI_UsageLog_InCost] DEFAULT(0);
IF COL_LENGTH('dbo.AI_UsageLog','OutputCostUsd') IS NULL
    ALTER TABLE [dbo].[AI_UsageLog] ADD [OutputCostUsd] DECIMAL(18,6) NOT NULL CONSTRAINT [DF_AI_UsageLog_OutCost] DEFAULT(0);";
            SqlHelper.ExecuteNonQuery(sql);
        }

        public void Insert(AiUsageRecord r)
        {
            if (r == null) return;

            const string sql = @"
INSERT INTO AI_UsageLog
    (Provider, Model, Endpoint, Purpose, PromptTokens, CompletionTokens, TotalTokens, LatencyMs, InputCostUsd, OutputCostUsd, EstimatedCostUsd, Success, ErrorMessage, ContextKey)
VALUES
    (@provider, @model, @endpoint, @purpose, @pt, @ct, @tt, @lat, @incost, @outcost, @cost, @ok, @err, @ctx);";

            SqlHelper.ExecuteNonQuery(sql, new[]
            {
                new SqlParameter("@provider", (object)(r.Provider ?? "") ),
                new SqlParameter("@model", (object)(r.Model ?? "") ),
                new SqlParameter("@endpoint", (object)r.Endpoint ?? DBNull.Value),
                new SqlParameter("@purpose", (object)(r.Purpose ?? "") ),
                new SqlParameter("@pt", r.PromptTokens),
                new SqlParameter("@ct", r.CompletionTokens),
                new SqlParameter("@tt", r.TotalTokens),
                new SqlParameter("@lat", (int)Math.Min(int.MaxValue, r.LatencyMs)),
                new SqlParameter("@incost", r.InputCostUsd),
                new SqlParameter("@outcost", r.OutputCostUsd),
                new SqlParameter("@cost", r.EstimatedCostUsd),
                new SqlParameter("@ok", r.Success),
                new SqlParameter("@err", (object)r.ErrorMessage ?? DBNull.Value),
                new SqlParameter("@ctx", (object)r.ContextKey ?? DBNull.Value),
            });
        }
    }

    /// <summary>Model başına 1M token fiyatı (USD). Yerel modeller için 0.</summary>
    public sealed class ModelPrice
    {
        public decimal InputUsdPer1M { get; set; }
        public decimal OutputUsdPer1M { get; set; }
    }

    /// <summary>
    /// <c>AI_ModelPricing</c> tablosunu yönetir ve bilinen modeller için varsayılan fiyatları
    /// (idempotent) tohumlar. Bilinmeyen/yerel modeller için fiyat 0 kabul edilir; token yine loglanır.
    /// Fiyatlar tablodan SQL ile güncellenebilir.
    /// </summary>
    public sealed class AiModelPricingRepository
    {
        public void EnsureSchema()
        {
            const string sql = @"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[AI_ModelPricing]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[AI_ModelPricing](
        [Model] NVARCHAR(120) NOT NULL PRIMARY KEY,
        [InputUsdPer1M] DECIMAL(18,6) NOT NULL CONSTRAINT [DF_AI_ModelPricing_In] DEFAULT(0),
        [OutputUsdPer1M] DECIMAL(18,6) NOT NULL CONSTRAINT [DF_AI_ModelPricing_Out] DEFAULT(0),
        [UpdatedAt] DATETIME NOT NULL CONSTRAINT [DF_AI_ModelPricing_UpdatedAt] DEFAULT(GETDATE())
    );
END";
            SqlHelper.ExecuteNonQuery(sql);
            SeedDefaults();
        }

        private void SeedDefaults()
        {
            // OpenRouter modellerinin fiyatları canlı API'den (OpenRouterPricingService) doldurulur.
            // Burada yalnızca yerel (Ollama) modelleri 0 maliyetle tohumluyoruz; bunlar API'de yer almaz.
            InsertIfMissing("qwen2.5", 0m, 0m);
            InsertIfMissing("qwen2.5:7b", 0m, 0m);
            InsertIfMissing("llama3.1:8b", 0m, 0m);
        }

        /// <summary>Model fiyatını yalnızca tabloda yoksa ekler (mevcut/elle düzenlenmiş kayıtları korur).</summary>
        public void InsertIfMissing(string model, decimal input, decimal output)
        {
            if (string.IsNullOrWhiteSpace(model)) return;

            const string sql = @"
IF NOT EXISTS (SELECT 1 FROM AI_ModelPricing WHERE Model = @m)
    INSERT INTO AI_ModelPricing (Model, InputUsdPer1M, OutputUsdPer1M) VALUES (@m, @in, @out);";
            SqlHelper.ExecuteNonQuery(sql, new[]
            {
                new SqlParameter("@m", model),
                new SqlParameter("@in", input),
                new SqlParameter("@out", output),
            });
        }

        public ModelPrice GetPrice(string model)
        {
            if (string.IsNullOrWhiteSpace(model)) return new ModelPrice();

            const string sql = "SELECT TOP 1 InputUsdPer1M, OutputUsdPer1M FROM AI_ModelPricing WHERE Model = @m;";
            DataTable dt = SqlHelper.GetDataTable(sql, new[] { new SqlParameter("@m", model) });
            if (dt.Rows.Count == 0) return new ModelPrice();

            return new ModelPrice
            {
                InputUsdPer1M = ToDecimal(dt.Rows[0][0]),
                OutputUsdPer1M = ToDecimal(dt.Rows[0][1])
            };
        }

        private static decimal ToDecimal(object o)
        {
            if (o == null || o == DBNull.Value) return 0m;
            try { return Convert.ToDecimal(o); }
            catch { return 0m; }
        }
    }
}
