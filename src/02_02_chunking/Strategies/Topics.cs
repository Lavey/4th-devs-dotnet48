using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using FourthDevs.Common;
using FourthDevs.Common.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Lesson07_Chunking.Strategies
{
    /// <summary>
    /// Topic-based (AI-driven) chunking.
    /// Uses the LLM to identify logical topic boundaries and returns one chunk per topic.
    ///
    /// Mirrors 02_02_chunking/src/strategies/topics.js (i-am-alice/4th-devs)
    /// </summary>
    internal static class Topics
    {
        private const string Model = "gpt-4.1-mini";

        private const string Instructions =
            "You are a document chunking expert. Break the provided document into logical " +
            "topic-based chunks.\n\n" +
            "Rules:\n" +
            "- Each chunk must contain ONE coherent topic or idea\n" +
            "- Preserve the original text — do NOT summarise or rewrite\n" +
            "- Return a JSON array of objects: " +
            "[{ \"topic\": \"short topic label\", \"content\": \"original text for this topic\" }]\n" +
            "- Return ONLY the JSON array, no markdown fences or explanation";

        // ----------------------------------------------------------------
        // Public API
        // ----------------------------------------------------------------

        internal static async Task<List<Chunk>> ChunkByTopics(
            string text, string source = null)
        {
            string raw = await ChatAsync(text, Instructions);

            JArray parsed;
            try
            {
                parsed = JArray.Parse(raw);
            }
            catch
            {
                string cleaned = raw
                    .Replace("```json\n", string.Empty)
                    .Replace("```json",   string.Empty)
                    .Replace("```\n",     string.Empty)
                    .Replace("```",       string.Empty)
                    .Trim();
                parsed = JArray.Parse(cleaned);
            }

            var headings = MarkdownUtils.BuildHeadingIndex(text);
            var chunks   = new List<Chunk>();

            for (int i = 0; i < parsed.Count; i++)
            {
                var item    = parsed[i] as JObject;
                string c    = item?["content"]?.Value<string>() ?? string.Empty;
                string topic = item?["topic"]?.Value<string>() ?? string.Empty;

                chunks.Add(new Chunk
                {
                    Content  = c,
                    Metadata = new Dictionary<string, object>
                    {
                        ["strategy"] = "topics",
                        ["index"]    = i,
                        ["topic"]    = topic,
                        ["chars"]    = c.Length,
                        ["section"]  = MarkdownUtils.FindSection(text, c, headings),
                        ["source"]   = source ?? (object)null
                    }
                });
            }

            return chunks;
        }

        // ----------------------------------------------------------------
        // LLM helper
        // ----------------------------------------------------------------

        private static async Task<string> ChatAsync(string input, string instructions)
        {
            using (var http = new HttpClient())
            {
                http.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", AiConfig.ApiKey);

                if (AiConfig.Provider == "openrouter")
                {
                    if (!string.IsNullOrWhiteSpace(AiConfig.HttpReferer))
                        http.DefaultRequestHeaders.TryAddWithoutValidation(
                            "HTTP-Referer", AiConfig.HttpReferer);
                    if (!string.IsNullOrWhiteSpace(AiConfig.AppName))
                        http.DefaultRequestHeaders.TryAddWithoutValidation(
                            "X-Title", AiConfig.AppName);
                }

                var body = new { model = AiConfig.ResolveModel(Model), input, instructions };
                string json = JsonConvert.SerializeObject(body);

                using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
                using (var response = await http.PostAsync(AiConfig.ApiEndpoint, content))
                {
                    string responseBody = await response.Content.ReadAsStringAsync();
                    var parsed = JsonConvert.DeserializeObject<ResponsesResponse>(responseBody);

                    if (parsed?.Error != null)
                        throw new InvalidOperationException(parsed.Error.Message);

                    return ResponsesApiClient.ExtractText(parsed) ?? string.Empty;
                }
            }
        }
    }
}
