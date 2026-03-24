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

namespace FourthDevs.Email.Core
{
    /// <summary>
    /// Result of a single completion call.
    /// </summary>
    public class CompletionResult
    {
        public string OutputText { get; set; }
        public List<OutputItem> ToolCalls { get; set; } = new List<OutputItem>();
        public List<OutputItem> Output { get; set; } = new List<OutputItem>();
        public UsageInfo Usage { get; set; }
    }

    /// <summary>
    /// Thin wrapper around the Responses API that supports heterogeneous input arrays
    /// (user messages, function_call items, function_call_output items).
    /// </summary>
    public sealed class Completion : IDisposable
    {
        private readonly HttpClient _http;

        private static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
        };

        public Completion()
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
        }

        /// <summary>
        /// Send a completion request with heterogeneous input items.
        /// Input can contain InputMessage, OutputItem, ToolCallInput, or JObject instances.
        /// </summary>
        public async Task<CompletionResult> CompleteAsync(
            string model,
            string instructions,
            List<object> input,
            List<ToolDefinition> tools = null)
        {
            var serializer = JsonSerializer.Create(SerializerSettings);

            var requestObj = new JObject
            {
                ["model"] = AiConfig.ResolveModel(model),
                ["instructions"] = instructions,
                ["store"] = false,
            };

            // Build input array — each item serialized based on its actual type
            var inputArray = new JArray();
            foreach (var item in input)
            {
                if (item is JObject jobj)
                {
                    inputArray.Add(jobj);
                }
                else
                {
                    inputArray.Add(JObject.FromObject(item, serializer));
                }
            }
            requestObj["input"] = inputArray;

            if (tools != null && tools.Count > 0)
            {
                requestObj["tools"] = JArray.FromObject(tools, serializer);
            }

            string json = requestObj.ToString(Formatting.None);

            using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
            using (var response = await _http.PostAsync(AiConfig.ApiEndpoint, content).ConfigureAwait(false))
            {
                string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var parsed = JsonConvert.DeserializeObject<ResponsesResponse>(body);

                if (!response.IsSuccessStatusCode || parsed?.Error != null)
                {
                    string msg = parsed?.Error?.Message
                                 ?? $"Request failed with status {(int)response.StatusCode}";
                    throw new InvalidOperationException(msg);
                }

                return new CompletionResult
                {
                    OutputText = ResponsesApiClient.ExtractText(parsed),
                    ToolCalls = ResponsesApiClient.GetToolCalls(parsed),
                    Output = parsed.Output ?? new List<OutputItem>(),
                    Usage = parsed.Usage,
                };
            }
        }

        public void Dispose()
        {
            _http?.Dispose();
        }
    }
}
