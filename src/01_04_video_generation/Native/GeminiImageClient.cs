using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FourthDevs.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.VideoGeneration.Native
{
    /// <summary>
    /// Client for Gemini image generation using the generateContent API.
    /// Supports reference images for consistent end-frame generation.
    /// Falls back to OpenRouter (google/gemini-3.1-flash-image-preview) if configured.
    /// </summary>
    internal static class GeminiImageClient
    {
        private const string GeminiGenerateUrl =
            "https://generativelanguage.googleapis.com/v1beta/models/gemini-3-pro-image-preview:generateContent";

        private const string OpenRouterChatUrl =
            "https://openrouter.ai/api/v1/chat/completions";

        private const string OpenRouterImageModel = "google/gemini-3.1-flash-image-preview";

        private static readonly HttpClient Http = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(5)
        };

        /// <summary>
        /// Generates an image from a text prompt using Gemini or OpenRouter.
        /// Returns the raw image bytes (PNG).
        /// </summary>
        /// <param name="prompt">Text prompt describing the image to generate.</param>
        /// <param name="referenceImagePaths">Optional paths to reference images (base64-encoded inline).</param>
        public static async Task<byte[]> GenerateImageAsync(string prompt, string[] referenceImagePaths = null)
        {
            string openRouterKey = System.Configuration.ConfigurationManager.AppSettings["OPENROUTER_API_KEY"]?.Trim();

            if (!string.IsNullOrWhiteSpace(openRouterKey))
                return await GenerateViaOpenRouterAsync(prompt, openRouterKey);

            return await GenerateViaGeminiAsync(prompt, referenceImagePaths);
        }

        // ----------------------------------------------------------------
        // Gemini generateContent path
        // ----------------------------------------------------------------

        private static async Task<byte[]> GenerateViaGeminiAsync(string prompt, string[] referenceImagePaths)
        {
            string apiKey = AiConfig.GeminiApiKey;
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException(
                    "GEMINI_API_KEY is not configured. Copy App.config.example to App.config and fill in your key.");

            var parts = new JArray();

            // Add reference images first so the model can use them for consistency
            if (referenceImagePaths != null)
            {
                foreach (string imgPath in referenceImagePaths)
                {
                    if (!File.Exists(imgPath)) continue;
                    byte[] imgBytes = File.ReadAllBytes(imgPath);
                    string mimeType = GetImageMimeType(imgPath);
                    parts.Add(new JObject
                    {
                        ["inline_data"] = new JObject
                        {
                            ["mime_type"] = mimeType,
                            ["data"]      = Convert.ToBase64String(imgBytes)
                        }
                    });
                }
            }

            parts.Add(new JObject { ["text"] = prompt });

            var body = new JObject
            {
                ["contents"] = new JArray
                {
                    new JObject
                    {
                        ["role"]  = "user",
                        ["parts"] = parts
                    }
                },
                ["generationConfig"] = new JObject
                {
                    ["responseModalities"] = new JArray { "IMAGE", "TEXT" }
                }
            };

            string json = body.ToString(Formatting.None);
            var request = new HttpRequestMessage(HttpMethod.Post, GeminiGenerateUrl)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            request.Headers.Add("x-goog-api-key", apiKey);

            using (var response = await Http.SendAsync(request))
            {
                string responseBody = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                    throw new InvalidOperationException(
                        "Gemini image API error " + (int)response.StatusCode + ": " +
                        Truncate(responseBody, 500));

                return ExtractImageFromGeminiResponse(responseBody);
            }
        }

        private static byte[] ExtractImageFromGeminiResponse(string responseJson)
        {
            JObject parsed;
            try { parsed = JObject.Parse(responseJson); }
            catch { throw new InvalidOperationException("Could not parse Gemini response as JSON."); }

            string errMsg = parsed["error"]?["message"]?.ToString();
            if (!string.IsNullOrEmpty(errMsg))
                throw new InvalidOperationException("Gemini error: " + errMsg);

            var candidates = parsed["candidates"] as JArray;
            if (candidates == null || candidates.Count == 0)
                throw new InvalidOperationException("Gemini returned no candidates.");

            foreach (JToken candidate in candidates)
            {
                var parts = candidate["content"]?["parts"] as JArray;
                if (parts == null) continue;
                foreach (JToken part in parts)
                {
                    string b64 = part["inline_data"]?["data"]?.ToString();
                    if (!string.IsNullOrEmpty(b64))
                        return Convert.FromBase64String(b64);
                }
            }

            throw new InvalidOperationException("Gemini response contained no image data.");
        }

        // ----------------------------------------------------------------
        // OpenRouter path
        // ----------------------------------------------------------------

        private static async Task<byte[]> GenerateViaOpenRouterAsync(string prompt, string apiKey)
        {
            var body = new JObject
            {
                ["model"] = OpenRouterImageModel,
                ["messages"] = new JArray
                {
                    new JObject
                    {
                        ["role"]    = "user",
                        ["content"] = prompt
                    }
                }
            };

            string json = body.ToString(Formatting.None);
            var request = new HttpRequestMessage(HttpMethod.Post, OpenRouterChatUrl)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            using (var response = await Http.SendAsync(request))
            {
                string responseBody = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                    throw new InvalidOperationException(
                        "OpenRouter image API error " + (int)response.StatusCode + ": " +
                        Truncate(responseBody, 500));

                return ExtractImageFromOpenRouterResponse(responseBody);
            }
        }

        private static byte[] ExtractImageFromOpenRouterResponse(string responseJson)
        {
            JObject parsed;
            try { parsed = JObject.Parse(responseJson); }
            catch { throw new InvalidOperationException("Could not parse OpenRouter response as JSON."); }

            string errMsg = parsed["error"]?["message"]?.ToString();
            if (!string.IsNullOrEmpty(errMsg))
                throw new InvalidOperationException("OpenRouter error: " + errMsg);

            var choices = parsed["choices"] as JArray;
            if (choices == null || choices.Count == 0)
                throw new InvalidOperationException("OpenRouter returned no choices.");

            // Check for image in content parts (multimodal response)
            var content = choices[0]["message"]?["content"];
            if (content?.Type == JTokenType.Array)
            {
                foreach (JToken part in (JArray)content)
                {
                    if (part["type"]?.ToString() == "image_url")
                    {
                        string url = part["image_url"]?["url"]?.ToString();
                        if (!string.IsNullOrEmpty(url))
                        {
                            // data URI: "data:image/png;base64,..."
                            int commaIdx = url.IndexOf(',');
                            if (commaIdx >= 0)
                                return Convert.FromBase64String(url.Substring(commaIdx + 1));

                            // External URL – download it
                            return Http.GetByteArrayAsync(url).GetAwaiter().GetResult();
                        }
                    }
                }
            }

            // Some models return base64 in text content
            string textContent = content?.ToString() ?? string.Empty;
            if (textContent.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
            {
                int commaIdx = textContent.IndexOf(',');
                if (commaIdx >= 0)
                    return Convert.FromBase64String(textContent.Substring(commaIdx + 1));
            }

            throw new InvalidOperationException(
                "OpenRouter response contained no image data. Response: " + Truncate(responseJson, 300));
        }

        // ----------------------------------------------------------------
        // Helpers
        // ----------------------------------------------------------------

        private static string GetImageMimeType(string filePath)
        {
            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            switch (ext)
            {
                case ".jpg":
                case ".jpeg": return "image/jpeg";
                case ".webp": return "image/webp";
                case ".gif":  return "image/gif";
                default:      return "image/png";
            }
        }

        private static string Truncate(string s, int max)
            => s != null && s.Length > max ? s.Substring(0, max) + "..." : s ?? string.Empty;
    }
}
