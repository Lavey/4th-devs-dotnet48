using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using FourthDevs.Common;
using FourthDevs.Observability.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Observability.Core
{
    /// <summary>
    /// Simple HTTP client for the OpenAI Chat Completions API.
    /// Uses <see cref="AiConfig"/> for provider/key resolution.
    /// </summary>
    internal sealed class ChatCompletionsClient : IDisposable
    {
        private readonly HttpClient _http;
        private readonly string _endpoint;

        public ChatCompletionsClient()
        {
            _http = new HttpClient();
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", AiConfig.ApiKey);

            if (AiConfig.Provider == "openrouter")
            {
                if (!string.IsNullOrEmpty(AiConfig.HttpReferer))
                    _http.DefaultRequestHeaders.Add("HTTP-Referer", AiConfig.HttpReferer);
                if (!string.IsNullOrEmpty(AiConfig.AppName))
                    _http.DefaultRequestHeaders.Add("X-Title", AiConfig.AppName);
            }

            _endpoint = AiConfig.Provider == "openai"
                ? "https://api.openai.com/v1/chat/completions"
                : "https://openrouter.ai/api/v1/chat/completions";
        }

        /// <summary>
        /// Sends a Chat Completions request and returns the parsed result.
        /// </summary>
        public async Task<CompletionResult> CompleteAsync(
            string model,
            List<ChatMessage> messages,
            List<object> tools = null)
        {
            var body = new Dictionary<string, object>
            {
                { "model", model },
                { "messages", messages }
            };

            if (tools != null && tools.Count > 0)
            {
                body["tools"] = tools;
            }

            string json = JsonConvert.SerializeObject(body, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            });

            using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
            {
                HttpResponseMessage resp = await _http.PostAsync(_endpoint, content).ConfigureAwait(false);
                string respBody = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (!resp.IsSuccessStatusCode)
                {
                    throw new HttpRequestException(
                        string.Format("Chat Completions API error {0}: {1}",
                            (int)resp.StatusCode, respBody));
                }

                return ParseResponse(respBody);
            }
        }

        public void Dispose()
        {
            _http.Dispose();
        }

        // -----------------------------------------------------------------
        // Response parsing
        // -----------------------------------------------------------------

        private static CompletionResult ParseResponse(string json)
        {
            JObject root = JObject.Parse(json);
            var result = new CompletionResult();

            // Usage
            JToken usageTok = root["usage"];
            if (usageTok != null)
            {
                result.Usage = new Usage
                {
                    Input = (int?)usageTok["prompt_tokens"],
                    Output = (int?)usageTok["completion_tokens"],
                    Total = (int?)usageTok["total_tokens"]
                };
            }

            // First choice
            JToken choices = root["choices"];
            if (choices == null || !choices.HasValues)
            {
                return result;
            }

            JToken message = choices[0]["message"];
            if (message == null)
            {
                return result;
            }

            // Text content
            JToken contentTok = message["content"];
            if (contentTok != null && contentTok.Type == JTokenType.String)
            {
                result.Text = contentTok.Value<string>();
            }

            // Tool calls
            JToken toolCallsTok = message["tool_calls"];
            if (toolCallsTok != null && toolCallsTok.HasValues)
            {
                result.ToolCalls = new List<ToolCall>();
                foreach (JToken tc in toolCallsTok)
                {
                    result.ToolCalls.Add(new ToolCall
                    {
                        Id = (string)tc["id"],
                        Name = (string)tc["function"]["name"],
                        Arguments = (string)tc["function"]["arguments"]
                    });
                }
            }

            return result;
        }
    }
}
