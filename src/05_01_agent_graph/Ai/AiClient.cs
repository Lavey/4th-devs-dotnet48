using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using FourthDevs.AgentGraph.Models;
using FourthDevs.Common;
using Newtonsoft.Json.Linq;

namespace FourthDevs.AgentGraph.Ai
{
    public class ToolDefinition
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public JObject Parameters { get; set; }
    }

    public class ToolCall
    {
        public string CallId { get; set; }
        public string Name { get; set; }
        public JObject Arguments { get; set; }
    }

    public class GenerateTextResult
    {
        public string Text { get; set; }
        public TokenUsage Usage { get; set; }
    }

    public class GenerateToolStepResult
    {
        public string Text { get; set; }
        public List<ToolCall> ToolCalls { get; set; } = new List<ToolCall>();
        public TokenUsage Usage { get; set; }
    }

    public static class AiClient
    {
        private static readonly string Model = AiConfig.ResolveModel(
            Environment.GetEnvironmentVariable("PRIMITIVES_MODEL") ?? "gpt-4.1");

        private static readonly int MaxOutputTokens = ParsePositiveInt(
            Environment.GetEnvironmentVariable("PRIMITIVES_MAX_OUTPUT_TOKENS"), 16000);

        private static readonly bool IsOpenRouter = AiConfig.Provider == "openrouter";

        public static string DescribeLlm() => AiConfig.Provider + ":" + Model;

        private static int ParsePositiveInt(string value, int fallback)
        {
            int result;
            if (int.TryParse(value, out result) && result > 0) return result;
            return fallback;
        }

        // ── Retry logic ──────────────────────────────────────────────────

        private const int MaxRetries = 2;
        private const int BaseDelayMs = 1000;

        private static bool IsRetryable(Exception ex)
        {
            var msg = ex.Message.ToLowerInvariant();
            return msg.Contains("429") || msg.Contains("500") || msg.Contains("502")
                || msg.Contains("503") || msg.Contains("504") || msg.Contains("timeout")
                || msg.Contains("connection") || msg.Contains("reset");
        }

        private static async Task<T> WithRetry<T>(Func<Task<T>> fn)
        {
            Exception lastErr = null;
            for (int attempt = 0; attempt <= MaxRetries; attempt++)
            {
                try { return await fn(); }
                catch (Exception ex)
                {
                    lastErr = ex;
                    if (attempt == MaxRetries || !IsRetryable(ex)) throw;
                    int delay = BaseDelayMs * (1 << attempt);
                    Console.Error.WriteLine(
                        string.Format("[ai] retrying in {0}ms (attempt {1}/{2})", delay, attempt + 1, MaxRetries));
                    await Task.Delay(delay);
                }
            }
            throw lastErr;
        }

        // ── Public API ───────────────────────────────────────────────────

        public static async Task<GenerateTextResult> GenerateText(
            string instructions, object input, int? maxOutputTokens = null)
        {
            using (var client = new ResponsesApiClient())
            {
                var body = new JObject
                {
                    ["model"] = Model,
                    ["instructions"] = instructions,
                    ["max_output_tokens"] = maxOutputTokens.HasValue ? maxOutputTokens.Value : MaxOutputTokens,
                };

                if (input is string s)
                    body["input"] = s;
                else if (input is JToken jt)
                    body["input"] = jt;

                var response = await WithRetry(() => client.PostRawAsync(body));
                return new GenerateTextResult
                {
                    Text = ExtractText(response),
                    Usage = ExtractUsage(response),
                };
            }
        }

        public static async Task<GenerateToolStepResult> GenerateToolStep(
            string instructions, JArray input, List<ToolDefinition> tools,
            bool webSearch = false, string promptCacheKey = null, int? maxOutputTokens = null)
        {
            using (var client = new ResponsesApiClient())
            {
                var toolsArray = new JArray();
                foreach (var tool in tools)
                {
                    toolsArray.Add(new JObject
                    {
                        ["type"] = "function",
                        ["name"] = tool.Name,
                        ["description"] = tool.Description,
                        ["parameters"] = tool.Parameters,
                        ["strict"] = false,
                    });
                }

                if (webSearch && !IsOpenRouter)
                {
                    toolsArray.Add(new JObject { ["type"] = "web_search_preview" });
                }

                var effectiveModel = (webSearch && IsOpenRouter) ? Model + ":online" : Model;

                var body = new JObject
                {
                    ["model"] = effectiveModel,
                    ["instructions"] = instructions,
                    ["input"] = input,
                    ["tools"] = toolsArray,
                    ["max_output_tokens"] = maxOutputTokens.HasValue ? maxOutputTokens.Value : MaxOutputTokens,
                    ["parallel_tool_calls"] = true,
                };

                if (!string.IsNullOrEmpty(promptCacheKey))
                    body["prompt_cache_key"] = promptCacheKey;

                var response = await WithRetry(() => client.PostRawAsync(body));

                return new GenerateToolStepResult
                {
                    Text = ExtractText(response),
                    ToolCalls = ExtractToolCalls(response),
                    Usage = ExtractUsage(response),
                };
            }
        }

        // ── Extractors ──────────────────────────────────────────────────

        private static string ExtractText(JObject response)
        {
            var output = response["output"] as JArray;
            if (output == null) return "";
            var sb = new StringBuilder();
            foreach (var item in output)
            {
                if (!(item is JObject)) continue;
                if (item["type"] == null || item["type"].ToString() != "message") continue;
                var content = item["content"] as JArray;
                if (content == null) continue;
                foreach (var part in content)
                {
                    if (!(part is JObject)) continue;
                    if (part["type"] != null && part["type"].ToString() == "output_text")
                        sb.Append(part["text"] != null ? part["text"].ToString() : "");
                }
            }
            return sb.ToString();
        }

        private static List<ToolCall> ExtractToolCalls(JObject response)
        {
            var result = new List<ToolCall>();
            var output = response["output"] as JArray;
            if (output == null) return result;
            foreach (var item in output)
            {
                if (!(item is JObject)) continue;
                if (item["type"] == null || item["type"].ToString() != "function_call") continue;
                JObject args;
                try { args = JObject.Parse(item["arguments"] != null ? item["arguments"].ToString() : "{}"); }
                catch { args = new JObject(); }
                result.Add(new ToolCall
                {
                    CallId = item["call_id"] != null ? item["call_id"].ToString() : "",
                    Name = item["name"] != null ? item["name"].ToString() : "",
                    Arguments = args,
                });
            }
            return result;
        }

        private static TokenUsage ExtractUsage(JObject response)
        {
            var u = response["usage"];
            if (u == null) return null;
            return new TokenUsage
            {
                InputTokens = u["input_tokens"] != null ? u["input_tokens"].Value<int>() : 0,
                OutputTokens = u["output_tokens"] != null ? u["output_tokens"].Value<int>() : 0,
                TotalTokens = u["total_tokens"] != null ? u["total_tokens"].Value<int>() : 0,
                CachedTokens = u["input_tokens_details"] is JObject inputDetails && inputDetails["cached_tokens"] != null
                    ? inputDetails["cached_tokens"].Value<int>() : 0,
            };
        }
    }
}
