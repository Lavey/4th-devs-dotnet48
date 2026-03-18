using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using FourthDevs.Common;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Lesson08_GraphAgents.Graph
{
    /// <summary>
    /// HTTP client for the OpenAI / OpenRouter embeddings API.
    /// Mirrors 02_03_graph_agents/src/graph/embeddings.js (i-am-alice/4th-devs)
    /// </summary>
    internal sealed class EmbeddingClient : IDisposable
    {
        private const string EmbeddingModel = "text-embedding-3-small";
        private const int    BatchSize      = 20;

        private readonly HttpClient _http;
        private readonly string     _endpoint;
        private readonly string     _model;

        internal EmbeddingClient()
        {
            _http = new HttpClient();
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", AiConfig.ApiKey);

            if (AiConfig.Provider == "openrouter")
            {
                if (!string.IsNullOrWhiteSpace(AiConfig.HttpReferer))
                    _http.DefaultRequestHeaders.TryAddWithoutValidation(
                        "HTTP-Referer", AiConfig.HttpReferer);
                if (!string.IsNullOrWhiteSpace(AiConfig.AppName))
                    _http.DefaultRequestHeaders.TryAddWithoutValidation(
                        "X-Title", AiConfig.AppName);
            }

            _endpoint = AiConfig.EmbeddingsEndpoint;
            _model    = AiConfig.Provider == "openrouter"
                ? "openai/" + EmbeddingModel
                : EmbeddingModel;
        }

        /// <summary>
        /// Embeds a single text.
        /// </summary>
        internal async Task<float[]> EmbedAsync(string text)
        {
            var results = await EmbedBatchAsync(new[] { text });
            return results[0];
        }

        /// <summary>
        /// Embeds multiple texts in batches of 20, preserving order.
        /// </summary>
        internal async Task<List<float[]>> EmbedBatchAsync(IList<string> texts)
        {
            var all = new List<float[]>();

            for (int i = 0; i < texts.Count; i += BatchSize)
            {
                int end   = Math.Min(i + BatchSize, texts.Count);
                var batch = new List<string>();
                for (int j = i; j < end; j++)
                    batch.Add(texts[j]);

                Console.Write(
                    string.Format("  embeddings: {0}/{1}\r",
                        all.Count + batch.Count, texts.Count));

                all.AddRange(await EmbedRawAsync(batch));
            }

            if (texts.Count > BatchSize) Console.WriteLine();
            return all;
        }

        private async Task<List<float[]>> EmbedRawAsync(IList<string> batch)
        {
            var body = new { model = _model, input = batch };
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(body);

            using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
            using (var response = await _http.PostAsync(_endpoint, content))
            {
                string responseBody = await response.Content.ReadAsStringAsync();
                var data = JObject.Parse(responseBody);

                if (data["error"] != null)
                    throw new InvalidOperationException(
                        "Embedding error: " +
                        (data["error"]["message"]?.Value<string>() ?? responseBody));

                var dataArr = data["data"] as JArray;
                if (dataArr == null)
                    throw new InvalidOperationException(
                        "Unexpected embedding response: " + responseBody);

                var sorted = new List<(int index, float[] vec)>();
                foreach (var item in dataArr)
                {
                    int     idx = item["index"].Value<int>();
                    float[] vec = item["embedding"].ToObject<float[]>();
                    sorted.Add((idx, vec));
                }
                sorted.Sort((a, b) => a.index.CompareTo(b.index));

                var result = new List<float[]>();
                foreach (var (_, vec) in sorted)
                    result.Add(vec);

                return result;
            }
        }

        public void Dispose() => _http?.Dispose();
    }
}
