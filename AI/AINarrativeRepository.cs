using System;
using System.Data;
using System.Data.SqlClient;
using System.Security.Cryptography;
using System.Text;

namespace WindowsFormsApp1.AI
{
	public sealed class AINarrativeRepository
	{
		public void EnsureSchema()
		{
			string sql = @"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[AINarratives]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[AINarratives](
        [AINarrativesID] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [Title] NVARCHAR(200) NOT NULL,
        [Department] NVARCHAR(200) NOT NULL,
        [Model] NVARCHAR(100) NOT NULL,
        [Seed] INT NOT NULL,
        [Temperature] FLOAT NOT NULL,
        [PromptHash] NVARCHAR(64) NOT NULL,
        [ResponseJson] NVARCHAR(MAX) NOT NULL,
        [CreatedAt] DATETIME NOT NULL CONSTRAINT [DF_AINarratives_CreatedAt] DEFAULT(GETDATE())
    );

    CREATE UNIQUE INDEX [UX_AINarratives_Key]
    ON [dbo].[AINarratives]([Title],[Department],[Model],[Seed],[Temperature],[PromptHash]);
END";
			SqlHelper.ExecuteNonQuery(sql);
		}

		public string TryGetNarrativeJson(string title, string department, string model, int seed, double temperature, string prompt)
		{
			if (string.IsNullOrWhiteSpace(title)) return null;
			if (string.IsNullOrWhiteSpace(department)) return null;
			if (string.IsNullOrWhiteSpace(model)) return null;
			if (string.IsNullOrWhiteSpace(prompt)) return null;

			string hash = Sha256Hex(prompt);

			string sql = @"
SELECT TOP 1 ResponseJson
FROM AINarratives
WHERE Title = @t AND Department = @d AND Model = @m AND Seed = @s AND Temperature = @temp AND PromptHash = @h
ORDER BY CreatedAt DESC;";

			DataTable dt = SqlHelper.GetDataTable(sql, new[]
			{
				new SqlParameter("@t", title),
				new SqlParameter("@d", department),
				new SqlParameter("@m", model),
				new SqlParameter("@s", seed),
				new SqlParameter("@temp", temperature),
				new SqlParameter("@h", hash),
			});

			if (dt.Rows.Count == 0) return null;
			object o = dt.Rows[0][0];
			return (o == null || o == DBNull.Value) ? null : o.ToString();
		}

		public void UpsertNarrativeJson(string title, string department, string model, int seed, double temperature, string prompt, string responseJson)
		{
			if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("title boş olamaz.", nameof(title));
			if (string.IsNullOrWhiteSpace(department)) throw new ArgumentException("department boş olamaz.", nameof(department));
			if (string.IsNullOrWhiteSpace(model)) throw new ArgumentException("model boş olamaz.", nameof(model));
			if (string.IsNullOrWhiteSpace(prompt)) throw new ArgumentException("prompt boş olamaz.", nameof(prompt));
			if (string.IsNullOrWhiteSpace(responseJson)) throw new ArgumentException("responseJson boş olamaz.", nameof(responseJson));

			string hash = Sha256Hex(prompt);

			string sql = @"
UPDATE AINarratives
SET ResponseJson = @json, CreatedAt = GETDATE()
WHERE Title = @t AND Department = @d AND Model = @m AND Seed = @s AND Temperature = @temp AND PromptHash = @h;

IF (@@ROWCOUNT = 0)
BEGIN
    INSERT INTO AINarratives (Title, Department, Model, Seed, Temperature, PromptHash, ResponseJson)
    VALUES (@t, @d, @m, @s, @temp, @h, @json);
END";

			SqlHelper.ExecuteNonQuery(sql, new[]
			{
				new SqlParameter("@t", title),
				new SqlParameter("@d", department),
				new SqlParameter("@m", model),
				new SqlParameter("@s", seed),
				new SqlParameter("@temp", temperature),
				new SqlParameter("@h", hash),
				new SqlParameter("@json", responseJson),
			});
		}

		private static string Sha256Hex(string input)
		{
			using (var sha = SHA256.Create())
			{
				byte[] bytes = Encoding.UTF8.GetBytes(input ?? "");
				byte[] hash = sha.ComputeHash(bytes);
				var sb = new StringBuilder(hash.Length * 2);
				for (int i = 0; i < hash.Length; i++) sb.Append(hash[i].ToString("x2"));
				return sb.ToString();
			}
		}
	}
}

