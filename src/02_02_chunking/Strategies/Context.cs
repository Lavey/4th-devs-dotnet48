using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using FourthDevs.Common;
using FourthDevs.Common.Models;
using Newtonsoft.Json;

namespace FourthDevs.Lesson07_Chunking.Strategies
{
    /// <summary>
    /// Context-enriched chunking (Anthropic-style contextual retrieval).
    /// Splits with separators first, then uses LLM to generate a context prefix
    /// for each chunk.
    ///
    /// Mirrors 02_02_chunking/src/strategies/context.js (i-am-alice/4th-devs)
    /// </summary>
    internal static class Context
    {
        private const string Model = "gpt-4.1-mini";

        // ----------------------------------------------------------------
        // Public API
        // ----------------------------------------------------------------

        internal static async Task<List<Chunk>> ChunkWithContext(
            string text, string source = null)
        {
            var baseChunks = Separators.ChunkBySeparators(text, source);
            var enriched   = new List<Chunk>();

            for (int i = 0; i < baseChunks.Count; i++)
            {
                Console.Write(
                    string.Format("  context: enriching {0}/{1}\r", i + 1, baseChunks.Count));

                string contextPrefix = await EnrichChunk(baseChunks[i].Content);

                var meta = new Dictionary<string, object>(baseChunks[i].Metadata)
                {
                    ["strategy"] = "context",
                    ["context"]  = contextPrefix
                };

                enriched.Add(new Chunk
                {
                    Content  = baseChunks[i].Content,
                    Metadata = meta
                });
            }

            Console.WriteLine();
            return enriched;
        }

        // ----------------------------------------------------------------
        // LLM helper
        // ----------------------------------------------------------------

        private static async Task<string> EnrichChunk(string chunkContent)
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

                var body = new
                {
                    model        = AiConfig.ResolveModel(Model),
                    input        = string.Format("<chunk>{0}</chunk>", chunkContent),
                    instructions =
                        "Generate a very short (1-2 sentence) context that situates this " +
                        "chunk within the overall document. Return ONLY the context, nothing else."
                };

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
