using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FourthDevs.Common;
using FourthDevs.McpApps.Core;
using FourthDevs.McpApps.Models;
using FourthDevs.McpApps.Store;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.McpApps.Agent
{
    internal static class AgentRunner
    {
        private const int MaxToolRounds = 8;

        private static string BuildInstructions()
        {
            return "You are a marketing operations assistant for a SaaS company.\n" +
                   "You help the user manage their daily marketing work across these workflows:\n\n" +
                   "## Workflows & tools\n\n" +
                   "1. **Todos** — open_todo_board, list_todos, add_todo, complete_todo, reopen_todo, remove_todo.\n" +
                   "2. **Campaign review** — get_campaign_report, compare_campaigns, open_newsletter_dashboard, list_campaigns.\n" +
                   "3. **Sales analytics** — open_sales_analytics, get_sales_report.\n" +
                   "4. **Coupon management** — open_coupon_manager, create_coupon, deactivate_coupon, list_coupons.\n" +
                   "5. **Product catalog** — list_products, update_product, open_stripe_dashboard.\n\n" +
                   "## Guidelines\n\n" +
                   "- Be precise. Use get_campaign_report for a specific campaign, compare_campaigns for side-by-side.\n" +
                   "- Scope sales: if user says date range, pass from/to to open_sales_analytics.\n" +
                   "- Chain across domains when user asks.\n" +
                   "- Be concise. Reference actual data from tool results.\n" +
                   "- Today's date is " + DateTime.UtcNow.ToString("yyyy-MM-dd") + ".";
        }

        public static async Task<AgentTurnResult> RunTurnAsync(string message, string contextSummary)
        {
            string prompt = (message ?? "").Trim();
            if (string.IsNullOrEmpty(prompt))
                return new AgentTurnResult { Text = "Please type a message.", Mode = "local" };

            if (string.IsNullOrWhiteSpace(AiConfig.ApiKey))
            {
                var fallback = BuildFallbackResponse(prompt);
                fallback.Mode = "local";
                return fallback;
            }

            string userContent = string.IsNullOrWhiteSpace(contextSummary)
                ? prompt : prompt + "\n\n[Context: " + contextSummary + "]";

            var conversation = new JArray
            {
                new JObject { ["role"] = "user", ["content"] = userContent }
            };

            var toolExecs = new List<ToolExecution>();
            var toolDefs = ToolRegistry.GetDefinitionsForApi();

            for (int round = 0; round < MaxToolRounds; round++)
            {
                var body = new JObject
                {
                    ["model"] = AiConfig.ResolveModel("gpt-4.1"),
                    ["instructions"] = BuildInstructions(),
                    ["input"] = conversation,
                    ["tools"] = toolDefs,
                    ["parallel_tool_calls"] = false
                };

                string responseJson = await PostRawAsync(body.ToString(Formatting.None));
                JObject parsed;
                try { parsed = JObject.Parse(responseJson); }
                catch (Exception ex)
                {
                    return new AgentTurnResult { Text = "Error: " + ex.Message, Mode = "ai" };
                }

                if (parsed["error"] != null)
                    return new AgentTurnResult { Text = "API error: " + (parsed["error"]["message"]?.ToString() ?? "unknown"), Mode = "ai" };

                var outputArr = parsed["output"] as JArray ?? new JArray();
                var calls = new List<JToken>();
                foreach (JToken item in outputArr)
                {
                    if (item["type"]?.ToString() == "function_call")
                        calls.Add(item);
                }

                if (calls.Count == 0)
                {
                    string text = ExtractText(parsed);
                    return new AgentTurnResult { Text = string.IsNullOrEmpty(text) ? "Done." : text, ToolExecutions = toolExecs, Mode = "ai" };
                }

                var outputs = new JArray();
                foreach (JToken call in calls)
                {
                    string toolName = call["name"]?.ToString() ?? "";
                    string callId = call["call_id"]?.ToString() ?? "";
                    JObject toolArgs;
                    try { toolArgs = JObject.Parse(call["arguments"]?.ToString() ?? "{}"); }
                    catch { toolArgs = new JObject(); }

                    var tool = ToolRegistry.Find(toolName);
                    string resultJson;
                    if (tool == null)
                    {
                        resultJson = JsonConvert.SerializeObject(new { error = "Unknown tool: " + toolName });
                    }
                    else
                    {
                        try
                        {
                            var result = tool.Handler(toolArgs);
                            toolExecs.Add(new ToolExecution { ToolName = toolName, ToolArgs = toolArgs, ToolResult = result.Structured ?? result.Text });
                            resultJson = JsonConvert.SerializeObject(new { text = result.Text });
                        }
                        catch (Exception ex)
                        {
                            resultJson = JsonConvert.SerializeObject(new { error = ex.Message });
                        }
                    }

                    outputs.Add(call);
                    outputs.Add(new JObject
                    {
                        ["type"] = "function_call_output",
                        ["call_id"] = callId,
                        ["output"] = resultJson
                    });
                }

                foreach (JToken o in outputs) conversation.Add(o);
            }

            return new AgentTurnResult { Text = "Hit the tool limit for this turn.", ToolExecutions = toolExecs, Mode = "ai" };
        }

        // ── Fallback (no AI key) ──

        private static AgentTurnResult BuildFallbackResponse(string message)
        {
            string lower = message.ToLowerInvariant();
            var execs = new List<ToolExecution>();

            if (Regex.IsMatch(lower, @"\b(todo|task)\b"))
            {
                var tool = ToolRegistry.Find("open_todo_board");
                var result = tool.Handler(new JObject());
                execs.Add(new ToolExecution { ToolName = "open_todo_board", ToolArgs = new { }, ToolResult = result.Structured });
                return new AgentTurnResult { Text = result.Text, ToolExecutions = execs };
            }
            if (Regex.IsMatch(lower, @"\b(campaign|newsletter)\b"))
            {
                var tool = ToolRegistry.Find("open_newsletter_dashboard");
                var result = tool.Handler(new JObject());
                execs.Add(new ToolExecution { ToolName = "open_newsletter_dashboard", ToolArgs = new { }, ToolResult = result.Structured });
                return new AgentTurnResult { Text = result.Text, ToolExecutions = execs };
            }
            if (Regex.IsMatch(lower, @"\b(sales|revenue)\b"))
            {
                var tool = ToolRegistry.Find("get_sales_report");
                var result = tool.Handler(new JObject());
                execs.Add(new ToolExecution { ToolName = "get_sales_report", ToolArgs = new { }, ToolResult = result.Structured });
                return new AgentTurnResult { Text = result.Text, ToolExecutions = execs };
            }
            if (Regex.IsMatch(lower, @"\b(coupon|discount|promo)\b"))
            {
                var tool = ToolRegistry.Find("open_coupon_manager");
                var result = tool.Handler(new JObject());
                execs.Add(new ToolExecution { ToolName = "open_coupon_manager", ToolArgs = new { }, ToolResult = result.Structured });
                return new AgentTurnResult { Text = result.Text, ToolExecutions = execs };
            }
            if (Regex.IsMatch(lower, @"\b(product|pricing|plan|stripe)\b"))
            {
                var tool = ToolRegistry.Find("open_stripe_dashboard");
                var result = tool.Handler(new JObject());
                execs.Add(new ToolExecution { ToolName = "open_stripe_dashboard", ToolArgs = new { }, ToolResult = result.Structured });
                return new AgentTurnResult { Text = result.Text, ToolExecutions = execs };
            }

            return new AgentTurnResult { Text = "I can help with campaigns, sales, coupons, products, or todos. Try asking about any of these." };
        }

        // ── Helpers ──

        private static string ExtractText(JObject parsed)
        {
            string outputText = parsed["output_text"]?.ToString();
            if (!string.IsNullOrWhiteSpace(outputText)) return outputText;
            var outputArray = parsed["output"] as JArray;
            if (outputArray != null)
            {
                foreach (JToken item in outputArray)
                {
                    if (item["type"]?.ToString() == "message")
                    {
                        var content = item["content"] as JArray;
                        if (content != null)
                        {
                            foreach (JToken part in content)
                            {
                                if (part["type"]?.ToString() == "output_text")
                                {
                                    string t = part["text"]?.ToString();
                                    if (!string.IsNullOrEmpty(t)) return t;
                                }
                            }
                        }
                    }
                }
            }
            return string.Empty;
        }

        private static async Task<string> PostRawAsync(string jsonBody)
        {
            using (var http = new HttpClient())
            {
                http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AiConfig.ApiKey);
                if (AiConfig.Provider == "openrouter")
                {
                    if (!string.IsNullOrWhiteSpace(AiConfig.HttpReferer))
                        http.DefaultRequestHeaders.TryAddWithoutValidation("HTTP-Referer", AiConfig.HttpReferer);
                    if (!string.IsNullOrWhiteSpace(AiConfig.AppName))
                        http.DefaultRequestHeaders.TryAddWithoutValidation("X-Title", AiConfig.AppName);
                }
                using (var content = new StringContent(jsonBody, Encoding.UTF8, "application/json"))
                using (var response = await http.PostAsync(AiConfig.ApiEndpoint, content))
                {
                    return await response.Content.ReadAsStringAsync();
                }
            }
        }
    }
}
