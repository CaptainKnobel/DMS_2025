using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace DMS_2025.Services.Worker.GenAI
{
    public class GeminiService
    {
        private readonly HttpClient _http = new();
        private readonly string _apiKey;

        public GeminiService()
        {
            _apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY")
                      ?? throw new InvalidOperationException("GEMINI_API_KEY is not set");
        }

        public async Task<string?> SummarizeAsync(string ocrText, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(ocrText))
                return null;

            var prompt = $"Summarize the following document in 3–5 bullet points. " +
                         $"Use the same language as the text.\n\n{ocrText}";

            var payload = new
            {
                contents = new[]
                {
                new
                {
                    parts = new[]
                    {
                        new { text = prompt }
                    }
                }
            },
                generationConfig = new
                {
                    temperature = 0.3,
                    topK = 40,
                    topP = 0.8,
                    maxOutputTokens = 300
                }
            };

            using var body = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            _http.DefaultRequestHeaders.Clear();
            _http.DefaultRequestHeaders.Add("X-goog-api-key", _apiKey);

            using var response = await _http.PostAsync(
                "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent",
                body,
                ct);

            var json = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                // log details in the caller
                return null;
            }

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var text = root
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString();

                return string.IsNullOrWhiteSpace(text) ? null : text;
            }
            catch
            {
                return null;
            }
        }
    }
}
