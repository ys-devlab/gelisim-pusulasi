using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Data;
using System.Data.SqlClient;
using System.Text;
using System.Threading.Tasks;

namespace WindowsFormsApp1.AI
{
	public sealed class GeminiChatService
	{
		private const string FallbackCompetencyText = "Kritik eksik yetkinlik belirlemek için yeterli değerlendirici verisi bulunmuyor.";

		private readonly string _apiKey;
		private readonly CompetencyGapService _competencyGapService;

		public GeminiChatService(string apiKey)
		{
			_apiKey = (apiKey ?? string.Empty).Trim();

			string connectionString = ConfigurationManager.ConnectionStrings["LiftUpConnection"]?.ConnectionString ?? string.Empty;
			_competencyGapService = new CompetencyGapService(connectionString);
		}

		public async Task<string> SendMessageAsync(
			string userCode,
			string userMessage,
			IReadOnlyList<string> conversationHistory = null,
			Action<string> statusLogger = null)
		{
			// API anahtarı artık ai.env'den (AiGateway) yönetilir; burada zorunlu değil.
			if (string.IsNullOrWhiteSpace(userMessage))
			{
				return "Lütfen bir mesaj yazın.";
			}

			statusLogger?.Invoke("Aktif kullanıcı kodu: " + (string.IsNullOrWhiteSpace(userCode) ? "(boş)" : userCode));

			UserContext context = GetUserContext(userCode, statusLogger);
			string weaknesses = BuildWeaknessContext(userCode, statusLogger);
			string systemPrompt = BuildSystemPrompt(context, weaknesses);
			string conversationalPrompt = BuildConversationalPrompt(userMessage.Trim(), conversationHistory);

			statusLogger?.Invoke("Eksik yetkinlik bağlamı: " + weaknesses);
			statusLogger?.Invoke("Kullanıcı profili: " + context.FullName + " | " + context.Title + " | " + context.Department);
			statusLogger?.Invoke("Değerlendirme özeti: " + context.EvaluationSummary);
			statusLogger?.Invoke("System prompt (kısaltılmış): " + Truncate(systemPrompt, 500));
			statusLogger?.Invoke("Modele giden kullanıcı+geçmiş promptu (kısaltılmış): " + Truncate(conversationalPrompt, 900));

			using (var gemini = new GeminiClient(_apiKey))
			{
				string response = await gemini.GenerateAsync(
					prompt: conversationalPrompt,
					requireJson: false,
					systemInstructionText: systemPrompt,
					statusLogger: statusLogger).ConfigureAwait(false);

				return (response ?? string.Empty).Trim();
			}
		}

		private UserContext GetUserContext(string userCode, Action<string> statusLogger)
		{
			var context = new UserContext
			{
				FullName = "Bilinmiyor",
				Title = "Bilinmiyor",
				Department = "Bilinmiyor",
				EvaluationSummary = "Değerlendirme durumu alınamadı."
			};

			try
			{
				string profileSql = @"
SELECT TOP 1
    ISNULL(e.FullName, '') AS FullName,
    ISNULL(e.Title, '') AS Title,
    ISNULL(e.DepartmentName, '') AS DepartmentName
FROM Employees e
WHERE e.PersonelCode = @code;";

				using (var conn = new SqlConnection(ConfigurationManager.ConnectionStrings["LiftUpConnection"]?.ConnectionString ?? string.Empty))
				using (var cmd = new SqlCommand(profileSql, conn))
				{
					cmd.Parameters.AddWithValue("@code", userCode ?? string.Empty);
					conn.Open();
					using (var reader = cmd.ExecuteReader())
					{
						if (reader.Read())
						{
							context.FullName = SafeRead(reader, "FullName", "Bilinmiyor");
							context.Title = SafeRead(reader, "Title", "Bilinmiyor");
							context.Department = SafeRead(reader, "DepartmentName", "Bilinmiyor");
						}
					}
				}

				string evalSql = @"
SELECT
    SUM(CASE WHEN RH.Status = 'Completed' THEN 1 ELSE 0 END) AS CompletedHeaderCount,
    SUM(CASE WHEN RH.Status = 'Draft' THEN 1 ELSE 0 END) AS DraftHeaderCount,
    SUM(CASE WHEN RH.Status = 'Completed' AND RI.NumericAnswer > 0 THEN 1 ELSE 0 END) AS PositiveAnswerCount
FROM ResponseHeaders RH
LEFT JOIN ResponseItems RI ON RI.ResponseHeaderID = RH.ResponseHeaderID
WHERE RH.RateePersonelCode = @code
  AND RH.CycleID = (SELECT TOP 1 CycleID FROM EvaluationCycles WHERE IsActive = 1);";

				using (var conn = new SqlConnection(ConfigurationManager.ConnectionStrings["LiftUpConnection"]?.ConnectionString ?? string.Empty))
				using (var cmd = new SqlCommand(evalSql, conn))
				{
					cmd.Parameters.AddWithValue("@code", userCode ?? string.Empty);
					conn.Open();
					using (var reader = cmd.ExecuteReader())
					{
						if (reader.Read())
						{
							int completed = ToInt(reader["CompletedHeaderCount"]);
							int draft = ToInt(reader["DraftHeaderCount"]);
							int positive = ToInt(reader["PositiveAnswerCount"]);
							context.EvaluationSummary = string.Format(
								CultureInfo.InvariantCulture,
								"CompletedHeader={0}, DraftHeader={1}, PositiveAnswer={2}",
								completed, draft, positive);
						}
					}
				}
			}
			catch (Exception ex)
			{
				statusLogger?.Invoke("Kullanıcı bağlamı okunurken hata: " + ex.Message);
			}

			return context;
		}

		private string BuildWeaknessContext(string userCode, Action<string> statusLogger)
		{
			try
			{
				List<CompetencyGapService.CompetencyGapItem> items = _competencyGapService
					.GetTopWeakCompetencies(userCode, top: 3, threshold: 3.0)
					.Where(i => i != null && !string.IsNullOrWhiteSpace(i.CompetencyName) && i.WeightedScore < 3.0)
					.OrderBy(i => i.WeightedScore)
					.Take(3)
					.ToList();

				statusLogger?.Invoke("DB sorgu sonucu: " + items.Count + " kritik yetkinlik kaydı bulundu.");
				if (items == null || items.Count == 0)
				{
					statusLogger?.Invoke("Kullanıcı verisi alındı ancak kritik skor üretmek için yeterli dış değerlendirici verisi yok.");
					return FallbackCompetencyText;
				}

				return string.Join(", ",
					items.Select(i => string.Format(CultureInfo.InvariantCulture, "{0}: {1:0.00}", i.CompetencyName, i.WeightedScore)));
			}
			catch
			{
				return FallbackCompetencyText;
			}
		}

		private static string BuildSystemPrompt(UserContext context, string dbWeakCompetencies)
		{
			string safeWeaknesses = string.IsNullOrWhiteSpace(dbWeakCompetencies)
				? FallbackCompetencyText
				: dbWeakCompetencies.Trim();

			return "Sen LiftUp 360 sisteminin İK gelişim asistanı ve modern bir mentorsun. " +
				   "Sadece aktif oturumdaki kullanıcı için konuş. " +
				   "Aktif kullanıcı profili: AdSoyad=" + context.FullName + ", Unvan=" + context.Title + ", Birim=" + context.Department + ". " +
				   "Değerlendirme özeti: " + context.EvaluationSummary + ". " +
				   "Şu an konuştuğun çalışanın geliştirmesi gereken yetkinlikler şunlar: " + safeWeaknesses + ". " +
				   "Kullanıcı 'benim adım ne', 'hangi birimdeyim', 'ünvanım ne' gibi soru sorarsa profil bilgisinden net cevap ver. " +
				   "Kullanıcı genel bir soru soruyorsa gereksiz yere challenge dayatma; doğal, genel ve faydalı cevap ver. " +
				   "Kullanıcı gelişim önerisi istediğinde öncelikle düşük yetkinlik listesini kullan. " +
				   "Bu alanlarda gelişmesi için ona asla geleneksel sınıf eğitimleri, uzun makaleler veya sıkıcı teoriler önerme. " +
				   "Bunun yerine Z kuşağına hitap eden, oyunlaştırılmış, günlük iş akışına anında entegre edilebilecek mikro öğrenme adımları ve pratik görevler (challenge'lar) sun. " +
				   "Dilin cesaretlendirici, dinamik ve arkadaşça olsun. " +
				   "Yanıtlarını kısa ve öz tut: idealde 4-6 cümle veya en fazla 6 madde. " +
				   "Uzun paragraflardan kaçın, uygulamaya dönük net adımlar ver. " +
				   "Etkileşimli konuş: her yanıtta en sonda kullanıcıya tek bir net takip sorusu sor. " +
				   "Gereksiz tekrar yapma ve mümkün olduğunda mini challenge formatı kullan.";
		}

		private static string BuildConversationalPrompt(string userMessage, IReadOnlyList<string> conversationHistory)
		{
			var sb = new StringBuilder();
			sb.AppendLine("Aşağıda sohbet geçmişi var. Cevap verirken bu geçmişteki bağlamı koru ve tutarlı devam et.");
			sb.AppendLine();
			sb.AppendLine("SOHBET GEÇMİŞİ:");

			if (conversationHistory != null && conversationHistory.Count > 0)
			{
				int takeCount = Math.Min(8, conversationHistory.Count);
				int start = conversationHistory.Count - takeCount;
				for (int i = start; i < conversationHistory.Count; i++)
				{
					sb.AppendLine(conversationHistory[i]);
				}
			}
			else
			{
				sb.AppendLine("Önceki mesaj yok.");
			}

			sb.AppendLine();
			sb.AppendLine("KULLANICININ YENİ MESAJI:");
			sb.AppendLine(userMessage);
			return sb.ToString();
		}

		private static string Truncate(string value, int maxLen)
		{
			if (string.IsNullOrEmpty(value) || value.Length <= maxLen) return value ?? string.Empty;
			return value.Substring(0, maxLen) + "...";
		}

		private static string SafeRead(IDataRecord reader, string column, string fallback)
		{
			try
			{
				object value = reader[column];
				if (value == null || value == DBNull.Value) return fallback;
				string text = value.ToString();
				return string.IsNullOrWhiteSpace(text) ? fallback : text.Trim();
			}
			catch
			{
				return fallback;
			}
		}

		private static int ToInt(object value)
		{
			try
			{
				if (value == null || value == DBNull.Value) return 0;
				return Convert.ToInt32(value, CultureInfo.InvariantCulture);
			}
			catch
			{
				return 0;
			}
		}

		private sealed class UserContext
		{
			public string FullName { get; set; }
			public string Title { get; set; }
			public string Department { get; set; }
			public string EvaluationSummary { get; set; }
		}
	}
}
