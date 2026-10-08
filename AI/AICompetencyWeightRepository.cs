using System;
using System.Data;
using System.Data.SqlClient;

namespace WindowsFormsApp1.AI
{
    public sealed class AICompetencyWeightRepository
    {
        public void EnsureSchema()
        {
            string sql = @"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[AICompetencyWeights]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[AICompetencyWeights](
        [AICompetencyWeightsID] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [Title] NVARCHAR(200) NOT NULL,
        [Department] NVARCHAR(200) NOT NULL,
        [Model] NVARCHAR(100) NOT NULL,
        [Seed] INT NOT NULL,
        [Temperature] FLOAT NOT NULL,
        [WeightsJson] NVARCHAR(MAX) NOT NULL,
        [CreatedAt] DATETIME NOT NULL CONSTRAINT [DF_AICompetencyWeights_CreatedAt] DEFAULT(GETDATE())
    );

    CREATE UNIQUE INDEX [UX_AICompetencyWeights_Key]
    ON [dbo].[AICompetencyWeights]([Title],[Department],[Model],[Seed],[Temperature]);
END";

            SqlHelper.ExecuteNonQuery(sql);
        }

        public string TryGetWeightsJson(string title, string department, string model, int seed, double temperature)
        {
            if (string.IsNullOrWhiteSpace(title)) return null;
            if (string.IsNullOrWhiteSpace(department)) return null;
            if (string.IsNullOrWhiteSpace(model)) return null;

            string sql = @"
SELECT TOP 1 WeightsJson
FROM AICompetencyWeights
WHERE Title = @t AND Department = @d AND Model = @m AND Seed = @s AND Temperature = @temp
ORDER BY CreatedAt DESC;";

            DataTable dt = SqlHelper.GetDataTable(sql, new[]
            {
                new SqlParameter("@t", title),
                new SqlParameter("@d", department),
                new SqlParameter("@m", model),
                new SqlParameter("@s", seed),
                new SqlParameter("@temp", temperature)
            });

            if (dt.Rows.Count == 0) return null;
            object o = dt.Rows[0][0];
            return (o == null || o == DBNull.Value) ? null : o.ToString();
        }

        public void UpsertWeightsJson(string title, string department, string model, int seed, double temperature, string weightsJson)
        {
            if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("title boş olamaz.", nameof(title));
            if (string.IsNullOrWhiteSpace(department)) throw new ArgumentException("department boş olamaz.", nameof(department));
            if (string.IsNullOrWhiteSpace(model)) throw new ArgumentException("model boş olamaz.", nameof(model));
            if (string.IsNullOrWhiteSpace(weightsJson)) throw new ArgumentException("weightsJson boş olamaz.", nameof(weightsJson));

            // SQL Server'da MERGE yerine UPDATE/INSERT ile basit ve güvenli akış
            string sql = @"
UPDATE AICompetencyWeights
SET WeightsJson = @json, CreatedAt = GETDATE()
WHERE Title = @t AND Department = @d AND Model = @m AND Seed = @s AND Temperature = @temp;

IF (@@ROWCOUNT = 0)
BEGIN
    INSERT INTO AICompetencyWeights (Title, Department, Model, Seed, Temperature, WeightsJson)
    VALUES (@t, @d, @m, @s, @temp, @json);
END";

            SqlHelper.ExecuteNonQuery(sql, new[]
            {
                new SqlParameter("@t", title),
                new SqlParameter("@d", department),
                new SqlParameter("@m", model),
                new SqlParameter("@s", seed),
                new SqlParameter("@temp", temperature),
                new SqlParameter("@json", weightsJson)
            });
        }
    }
}

