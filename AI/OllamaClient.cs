using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace WindowsFormsApp1.AI
{
	public class OllamaClient : IDisposable
	{
		private readonly HttpClient _httpClient;

		public OllamaClient(string url = "http://localhost:11434")
		{
			_httpClient = new HttpClient
			{
				BaseAddress = new Uri(url),
				Timeout = TimeSpan.FromMinutes(10) // <-- ZAMAN AŞIMINI BURADAN UZATIYORUZ
			};
		}

		public async Task<OllamaGenerateResponse> GenerateAsync(OllamaGenerateRequest request, CancellationToken cancellationToken)
		{
			var json = JsonConvert.SerializeObject(request, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
			var content = new StringContent(json, Encoding.UTF8, "application/json");

			var response = await _httpClient.PostAsync("/api/generate", content, cancellationToken).ConfigureAwait(false);
			response.EnsureSuccessStatusCode();

			var responseJson = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
			return JsonConvert.DeserializeObject<OllamaGenerateResponse>(responseJson);
		}

		public async Task<OllamaGenerateResponse> GenerateJsonAsync(OllamaGenerateRequest request, CancellationToken cancellationToken)
		{
			request.Format = "json";
			return await GenerateAsync(request, cancellationToken).ConfigureAwait(false);
		}

		// DİĞER DOSYALARIN HATA VERMESİNİ ENGELLEYEN "SEED" PARAMETRESİ BURAYA EKLENDİ
		public async Task<string> GenerateJsonAsync(string model, string prompt, int? seed = null, double? temperature = null, CancellationToken cancellationToken = default)
		{
			var req = new OllamaGenerateRequest
			{
				Model = model,
				Prompt = prompt,
				Format = "json",
				Stream = false,
				Options = new OllamaOptions { Seed = seed, Temperature = temperature }
			};
			var res = await GenerateAsync(req, cancellationToken).ConfigureAwait(false);
			return res?.Response ?? "";
		}

		public void Dispose()
		{
			_httpClient?.Dispose();
		}
	}

	public class OllamaGenerateRequest
	{
		[JsonProperty("model")] public string Model { get; set; }
		[JsonProperty("prompt")] public string Prompt { get; set; }
		[JsonProperty("stream")] public bool Stream { get; set; }
		[JsonProperty("format")] public string Format { get; set; }
		[JsonProperty("options")] public OllamaOptions Options { get; set; }
	}

	public class OllamaOptions
	{
		[JsonProperty("temperature")] public double? Temperature { get; set; }
		[JsonProperty("seed")] public int? Seed { get; set; }
		[JsonProperty("top_p")] public double? TopP { get; set; }
	}

	public class OllamaGenerateResponse
	{
		[JsonProperty("response")] public string Response { get; set; }
	}
}