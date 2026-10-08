using System;
using System.Threading.Tasks;
using WindowsFormsApp1.AI.Core;

namespace WindowsFormsApp1.AI
{
	public class GeminiAIService
	{
		// Model/anahtar/endpoint artık ai.env'den (AiGateway üzerinden) yönetilir.

		public async Task<string> GenerateReminderEmailAsync(string employeeName, string competencyName, string trainingName)
		{
			string prompt = $@"Sen profesyonel bir İK hatırlatma sistemisin.
            Çalışanımız {employeeName}, '{competencyName}' yetkinliği için atanan '{trainingName}' adımında 14 gündür hareketsiz.
            Ona nazik, motive edici bir e-posta yaz. Maksimum 3 paragraf olsun. En başa 'Konu: ...' ekle.";

			return await CallGeminiApiAsync(prompt, employeeName, competencyName, trainingName);
		}

		private async Task<string> CallGeminiApiAsync(string promptText, string calisan, string yetkinlik, string egitim)
		{
			try
			{
				string text = await AiGateway.GenerateTextAsync(
					AiPurpose.ReminderMail,
					new AiPrompt { User = promptText },
					contextKey: calisan).ConfigureAwait(false);

				return string.IsNullOrWhiteSpace(text) ? GetFallbackMail(calisan, yetkinlik, egitim) : text;
			}
			catch (Exception)
			{
				return GetFallbackMail(calisan, yetkinlik, egitim);
			}
		}

		private string GetFallbackMail(string calisan, string yetkinlik, string egitim)
		{
			return $"Konu: Gelişim Yolculuğunda Seni Bekliyoruz!\n\nMerhaba {calisan},\n\nLiftUp 360 sisteminde '{yetkinlik}' yetkinliğini geliştirmek için planlanan '{egitim}' adımında son 14 gündür bir movement göremedik.\n\nGelişimin bizim için çok değerli. Eğer yoğunluktan fırsat bulamadıysan İK ekibi olarak yanındayız. En kısa sürede eğitimini tamamlamanı bekliyoruz.\n\nSevgiler,\nLiftUp 360 İK Ekibi";
		}
	}
}