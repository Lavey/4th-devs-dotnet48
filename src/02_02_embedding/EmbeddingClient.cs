using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using FourthDevs.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Lesson07_Embedding
{
    /// <summary>
    /// Calls the OpenAI / OpenRouter embeddings API to obtain vector representations
    /// of text using <c>text-embedding-3-small</c>.
    ///
    /// Mirrors 02_02_embedding/app.js embed() (i-am-alice/4th-devs)
    /// </summary>
    internal sealed class EmbeddingClient : IDisposable
    {
        private const string EmbeddingModel = "text-embedding-3-small";

        private readonly HttpClient _http;
        private readonly string     _endpoint;
        private readonly string     _model;

        public EmbeddingClient()
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

            // OpenRouter uses the same model name prefix as OpenAI for embeddings
            _model = AiConfig.Provider == "openrouter"
                ? "openai/" + EmbeddingModel
                : EmbeddingModel;
        }

        /// <summary>
        /// Embeds a single text and returns the embedding vector.
        /// </summary>
        public async Task<float[]> EmbedAsync(string text)
        {
            var results = await EmbedBatchAsync(new[] { text });
            return results[0];
        }

        /// <summary>
        /// Embeds multiple texts and returns embedding vectors in the same order.
        /// </summary>
        public async Task<List<float[]>> EmbedBatchAsync(IList<string> texts)
        {
            var body = new { model = _model, input = texts };
            string json = JsonConvert.SerializeObject(body);

            using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
            using (var response = await _http.PostAsync(_endpoint, content))
            {
                string responseBody = await response.Content.ReadAsStringAsync();
                var data = JObject.Parse(responseBody);

                if (data["error"] != null)
                    throw new InvalidOperationException(
                        data["error"]["message"]?.Value<string>() ?? responseBody);

                var dataArr = data["data"] as JArray;
                if (dataArr == null)
                    throw new InvalidOperationException(
                        "Unexpected embeddings response: " + responseBody);

                // Sort by index to preserve request order
                var sorted = new List<(int index, float[] vec)>();
                foreach (var item in dataArr)
                {
                    int idx = item["index"].Value<int>();
                    var embedding = item["embedding"].ToObject<float[]>();
                    sorted.Add((idx, embedding));
                }

                sorted.Sort((a, b) => a.index.CompareTo(b.index));

                var result = new List<float[]>();
                foreach (var (_, vec) in sorted)
                    result.Add(vec);

                return result;
            }
        }

        public void Dispose()
        {
            _http?.Dispose();
        }
    }
}
