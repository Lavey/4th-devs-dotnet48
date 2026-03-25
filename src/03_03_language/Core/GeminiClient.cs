using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Language.Core
{
    public static class GeminiClient
    {
        private const string ApiUrl = "https://generativelanguage.googleapis.com/v1beta/interactions";

        private static readonly HttpClient Http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(120)
        };

        public static async Task<GeminiInteraction> CallInteractionAsync(GeminiInteractionRequest request)
        {
            string apiKey = FourthDevs.Common.AiConfig.GeminiApiKey;
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException("GEMINI_API_KEY is not configured in App.config");

            string json = JsonConvert.SerializeObject(request, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                Formatting = Formatting.None
            });

            var requestMsg = new HttpRequestMessage(HttpMethod.Post, ApiUrl)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            requestMsg.Headers.Add("x-goog-api-key", apiKey);

            using (var response = await Http.SendAsync(requestMsg))
            {
                string body = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                    throw new InvalidOperationException(
                        $"Gemini API error {(int)response.StatusCode}: {(body.Length > 500 ? body.Substring(0, 500) : body)}");

                var interaction = JsonConvert.DeserializeObject<GeminiInteraction>(body);
                if (interaction == null)
                    throw new InvalidOperationException("Gemini returned null response");
                return interaction;
            }
        }

        public static string ExtractText(List<JObject> outputs)
        {
            if (outputs == null) return string.Empty;
            var parts = new StringBuilder();
            foreach (var output in outputs)
            {
                string type = output["type"]?.Value<string>();
                if (type == "text")
                    parts.Append(output["text"]?.Value<string>() ?? string.Empty);
                else if (type == "thought")
                {
                    var summary = output["summary"] as JArray;
                    if (summary != null)
                        foreach (var s in summary)
                            parts.Append(s["text"]?.Value<string>() ?? string.Empty);
                }
            }
            return parts.ToString().Trim();
        }

        public static List<JObject> ExtractFunctionCalls(List<JObject> outputs)
        {
            if (outputs == null) return new List<JObject>();
            var calls = new List<JObject>();
            foreach (var output in outputs)
            {
                if (output["type"]?.Value<string>() == "function_call")
                    calls.Add(output);
            }
            return calls;
        }

        public static (string data, string mime) ExtractAudio(List<JObject> outputs)
        {
            if (outputs == null) return (null, null);
            foreach (var output in outputs)
            {
                if (output["type"]?.Value<string>() == "audio")
                {
                    string data = output["data"]?.Value<string>();
                    string mime = output["mime_type"]?.Value<string>() ?? "audio/pcm";
                    if (!string.IsNullOrEmpty(data)) return (data, mime);
                }
            }
            return (null, null);
        }

        public static T ParseJsonLoose<T>(string text) where T : class
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            string trimmed = text.Trim();

            var fenceMatch = Regex.Match(trimmed, @"```(?:json)?\s*([\s\S]*?)```", RegexOptions.IgnoreCase);
            string candidate = fenceMatch.Success ? fenceMatch.Groups[1].Value.Trim() : trimmed;

            var candidates = new List<string> { candidate };

            int firstBrace = candidate.IndexOf('{');
            int lastBrace = candidate.LastIndexOf('}');
            if (firstBrace >= 0 && lastBrace > firstBrace)
                candidates.Add(candidate.Substring(firstBrace, lastBrace - firstBrace + 1));

            int firstBracket = candidate.IndexOf('[');
            int lastBracket = candidate.LastIndexOf(']');
            if (firstBracket >= 0 && lastBracket > firstBracket)
                candidates.Add(candidate.Substring(firstBracket, lastBracket - firstBracket + 1));

            foreach (var c in candidates)
            {
                try { return JsonConvert.DeserializeObject<T>(c); }
                catch { }
            }
            return null;
        }

        public static byte[] ToWav(byte[] pcm, int sampleRate = 24000)
        {
            int channels = 1;
            int bitsPerSample = 16;
            int dataSize = pcm.Length;
            int byteRate = sampleRate * channels * (bitsPerSample / 8);
            short blockAlign = (short)(channels * (bitsPerSample / 8));

            using (var ms = new System.IO.MemoryStream())
            using (var writer = new System.IO.BinaryWriter(ms))
            {
                writer.Write(Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + dataSize);
                writer.Write(Encoding.ASCII.GetBytes("WAVE"));
                writer.Write(Encoding.ASCII.GetBytes("fmt "));
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)channels);
                writer.Write(sampleRate);
                writer.Write(byteRate);
                writer.Write(blockAlign);
                writer.Write((short)bitsPerSample);
                writer.Write(Encoding.ASCII.GetBytes("data"));
                writer.Write(dataSize);
                writer.Write(pcm);
                return ms.ToArray();
            }
        }

        public static string MimeFor(string path)
        {
            string ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
            switch (ext)
            {
                case ".mp3": return "audio/mpeg";
                case ".m4a": return "audio/mp4";
                case ".ogg": return "audio/ogg";
                case ".webm": return "audio/webm";
                case ".flac": return "audio/flac";
                case ".wav":
                default: return "audio/wav";
            }
        }
    }
}
