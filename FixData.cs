using System;
using System.Data.SqlClient;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FixData
{
    class Program
    {
        static void Main(string[] args)
        {
            MainAsync().GetAwaiter().GetResult();
        }

        static async Task MainAsync()
        {
            string connectionString = @"Data Source=.\SQLEXPRESS;Initial Catalog=LiftUp360DB;Integrated Security=True";
            string apiKey = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY") ?? "";
            string apiUrl = "https://openrouter.ai/api/v1/chat/completions";

            string matrix = @"
| Yetkinlik Grubu | Unvan | Yetkinlik Adı |
| :--- | :--- | :--- |
| Temel | Uzman, Mühendis, Pilot, Teknisyen, Genel Hizmet | Dürüstlük, İş Ahlakı ve Sorumluluk |
| Temel | Uzman, Mühendis, Pilot, Teknisyen, Genel Hizmet | İletişim ve İlişki Yönetimi |
| Temel | Uzman, Mühendis, Pilot, Teknisyen, Genel Hizmet | Duygusal Zeka ve Stres Yönetimi |
| Temel | Uzman, Mühendis, Pilot, Teknisyen, Genel Hizmet | Kalite ve Sürekli Gelişim Odaklılık |
| Fonksiyonel | Pilot | Uçuş Operasyon Bilgisi ve Prosedürlere Hakimiyet |
| Fonksiyonel | Pilot | Emniyet Yönetimi ve Risk Analizi |
| Fonksiyonel | Pilot | Durumsal Farkındalık |
| Fonksiyonel | Mühendis | Analitik Problem Çözme ve Kök Neden Analizi |
| Fonksiyonel | Mühendis | Süreç İyileştirme ve Metodoloji Kullanımı |
| Fonksiyonel | Mühendis | Teknik Dokümantasyon ve Raporlama |
| Fonksiyonel | Uzman | İş Analizi ve Veri Yorumlama |
| Fonksiyonel | Uzman | Süreç Yönetimi ve Standartlara Uyum |
| Fonksiyonel | Uzman | Raporlama ve Dokümantasyon |
| Fonksiyonel | Teknisyen | Teknik Beceri ve Ekipman Kullanma Yetkinliği |
| Fonksiyonel | Teknisyen | Arıza Tespit ve Müdahale Becerisi |
| Fonksiyonel | Teknisyen | İş Sağlığı ve Güvenliği – Prosedürlere Uyum |
| Fonksiyonel | Genel Hizmet | Operasyonel Görev Bilgisi ve Uygulama |
| Fonksiyonel | Genel Hizmet | Hijyen / Kalite Standartlarına Uyum |
| Fonksiyonel | Genel Hizmet | İş Sağlığı ve Güvenliği Kurallarına Uyum |
| Yönetsel | Başkan, Direktör, Direktör Yrd., Yönetici | Stratejik Düşünme ve Karar Alma |
| Yönetsel | Başkan, Direktör | Etkin Çalışma Ortamı Yaratma |
| Yönetsel | Başkan, Direktör | Değişim Yönetimi ve Uyum Sağlama |
| Yönetsel | Başkan, Direktör | İş Zekası |
| Yönetsel | Başkan, Direktör | Yenilikçilik |
| Yönetsel | Başkan, Direktör, Direktör Yrd., Yönetici | Sonuç Odaklılık ve Kaynak Yönetimi |
| Yönetsel | Başkan, Direktör, Direktör Yrd., Yönetici | Liderlik ve Ekip Yönetimi |
| Yönetsel | Direktör Yrd., Yönetici | Zaman Yönetimi ve Planlama |
| Yönetsel | Direktör Yrd., Yönetici | Analitik Düşünme |
| Yönetsel | Direktör Yrd., Yönetici | Çatışma / Anlaşmazlık Yönetimi |
| Yönetsel | Direktör Yrd., Yönetici | Etkili İletişim ve İkna |
| Yönetsel | Direktör Yrd., Yönetici | Çalışanları Yönlendirme ve Motive Etme |";

            using (SqlConnection conn = new SqlConnection(connectionString))
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Authorization", string.Format("Bearer {0}", apiKey));
                client.DefaultRequestHeaders.Add("HTTP-Referer", "https://github.com");
                client.DefaultRequestHeaders.Add("X-OpenRouter-Title", "LiftUp");
                client.Timeout = TimeSpan.FromMinutes(2);

                await conn.OpenAsync();
                SqlCommand cmd = new SqlCommand("SELECT AINarrativesID, Title, ResponseJson FROM AINarratives", conn);
                using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        int id = reader.GetInt32(0);
                        string title = reader.GetString(1);
                        string jsonText = reader.GetString(2);

                        string prompt = string.Format(@"Sen LiftUp 360 performans değerlendirme sisteminin çok hassas bir veri temizleme asistanısın.

GÖREVİN:
Aşağıdaki değerlendirme metnini (JSON formatında) analiz et.
Unvan: {0}

KURUMSAL YETKİNLİK MATRİSİ:
{1}

ADIMLAR:
1. Bu unvanın matriste sahip olması gereken DOĞRU yetkinlikleri bul.
2. Aşağıdaki bozuk JSON metni içeriğini analiz et. Unvanın matrisinde BULUNMAYAN tüm uydurma yetkinlikleri, onlara ait puanları, yorumları ve gelişim planı maddelerini metinden (gerek commentary'den gerek array'lerden) KESİP AT.
3. Kısaltılmış adlar (örneğin 'Analitik Problem Çözme' yerine 'Analitik Problem Çözme ve Kök Neden Analizi') varsa onları DOĞRU kabul et ve SİLME. Sadece tamamen ilgisiz ve uydurma olanları sil.
4. Orijinal ve doğru yetkinliklerin değerlendirmelerine KESİNLİKLE DOKUNMA. Sadece uydurma olanları silerek metni bütünleştir ve yapısını bozmadan SADECE temizlenmiş JSON'ı döndür. Asla markdown kodu veya json etiketi ekleme, doğrudan JSON verisini yaz.

BOZUK METİN JSON:
{2}", title, matrix, jsonText);

                        var requestBody = new
                        {
                            model = "google/gemini-2.0-flash-001",
                            messages = new[] { new { role = "user", content = prompt } },
                            temperature = 0.0
                        };

                        string jsonBody = JsonConvert.SerializeObject(requestBody);
                        var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                        try
                        {
                            var response = await client.PostAsync(apiUrl, content);
                            string responseString = await response.Content.ReadAsStringAsync();
                            JObject resultObj = JObject.Parse(responseString);
                            var contentToken = resultObj.SelectToken("$.choices[0].message.content");
                            if (contentToken != null)
                            {
                                string cleaned = contentToken.ToString().Trim();
                                if (cleaned.StartsWith("`json")) cleaned = cleaned.Substring(7, cleaned.Length - 10).Trim();
                                else if (cleaned.StartsWith("`")) cleaned = cleaned.Substring(3, cleaned.Length - 6).Trim();

                                string updateSql = "UPDATE AINarratives SET ResponseJson = @json WHERE AINarrativesID = @id";
                                using (SqlConnection updateConn = new SqlConnection(connectionString))
                                {
                                    await updateConn.OpenAsync();
                                    SqlCommand updateCmd = new SqlCommand(updateSql, updateConn);
                                    updateCmd.Parameters.AddWithValue("@json", cleaned);
                                    updateCmd.Parameters.AddWithValue("@id", id);
                                    await updateCmd.ExecuteNonQueryAsync();
                                }
                                Console.WriteLine(string.Format("Successfully processed ID: {0}", id));
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(string.Format("Error for ID {0}: {1}", id, ex.Message));
                        }
                    }
                }
            }
        }
    }
}
