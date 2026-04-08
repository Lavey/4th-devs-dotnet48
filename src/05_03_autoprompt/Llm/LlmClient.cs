using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using FourthDevs.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.AutoPrompt.Llm
{
    public sealed class LlmClient : IDisposable
    {
        private readonly HttpClient _http;

        public LlmClient()
        {
            _http = new HttpClient();
            _http.Timeout = TimeSpan.FromMinutes(10);
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

        public async Task<string> CompleteAsync(
            string systemPrompt,
            string userMessage,
            string model = null,
            Config.ReasoningConfig reasoning = null,
            double? temperature = null,
            Models.ExtractionSchema jsonSchema = null,
            string stage = "unknown")
        {
            model = model ?? Config.Defaults.MODEL;
            model = AiConfig.ResolveModel(model);

            var body = new JObject();
            body["model"] = model;

            // Build input array
            var input = new JArray();
            if (!string.IsNullOrEmpty(systemPrompt))
            {
                input.Add(new JObject
                {
                    ["role"] = "developer",
                    ["content"] = systemPrompt
                });
            }
            input.Add(new JObject
            {
                ["role"] = "user",
                ["content"] = userMessage
            });
            body["input"] = input;

            if (reasoning != null && !string.IsNullOrEmpty(reasoning.Effort))
            {
                if (reasoning.Effort != "none")
                {
                    body["reasoning"] = new JObject
                    {
                        ["effort"] = reasoning.Effort
                    };
                }
            }

            if (temperature.HasValue)
            {
                body["temperature"] = temperature.Value;
            }

            if (jsonSchema != null)
            {
                body["text"] = new JObject
                {
                    ["format"] = new JObject
                    {
                        ["type"] = "json_schema",
                        ["name"] = jsonSchema.Name,
                        ["schema"] = jsonSchema.Schema,
                        ["strict"] = false
                    }
                };
            }

            var sw = Stopwatch.StartNew();
            string json = body.ToString(Formatting.None);

            using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
            using (var response = await _http.PostAsync(AiConfig.ApiEndpoint, content))
            {
                string responseBody = await response.Content.ReadAsStringAsync();
                sw.Stop();

                if (!response.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException(
                        string.Format("LLM API {0}: {1}", (int)response.StatusCode, responseBody));
                }

                var data = JObject.Parse(responseBody);
                string text = ExtractText(data);
                long durationMs = sw.ElapsedMilliseconds;

                JToken usageToken = data["usage"];

                TraceCollector.Record(new Models.TraceEntry
                {
                    Timestamp = DateTime.UtcNow.ToString("o"),
                    Stage = stage,
                    Request = new Models.TraceRequest
                    {
                        Model = model,
                        Instructions = systemPrompt,
                        Input = userMessage,
                        Schema = jsonSchema != null ? jsonSchema.Name : null
                    },
                    Response = new Models.TraceResponse
                    {
                        Text = text,
                        Usage = usageToken
                    },
                    DurationMs = durationMs
                });

                return text;
            }
        }

        private static string ExtractText(JObject data)
        {
            // Try output_text shorthand first
            var outputText = data["output_text"];
            if (outputText != null && outputText.Type == JTokenType.String)
            {
                string val = outputText.Value<string>();
                if (!string.IsNullOrWhiteSpace(val))
                    return val;
            }

            // Iterate output blocks
            var output = data["output"] as JArray;
            if (output != null)
            {
                var sb = new StringBuilder();
                foreach (var block in output)
                {
                    if (block["type"] != null && block["type"].Value<string>() == "message")
                    {
                        var contentArr = block["content"] as JArray;
                        if (contentArr != null)
                        {
                            foreach (var part in contentArr)
                            {
                                if (part["type"] != null &&
                                    part["type"].Value<string>() == "output_text" &&
                                    part["text"] != null)
                                {
                                    sb.Append(part["text"].Value<string>());
                                }
                            }
                        }
                    }
                }
                if (sb.Length > 0)
                    return sb.ToString();
            }

            return string.Empty;
        }

        public void Dispose()
        {
            _http.Dispose();
        }
    }
}
