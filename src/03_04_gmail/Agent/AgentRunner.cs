using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using FourthDevs.Common;
using FourthDevs.Gmail.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Gmail.Agent
{
    internal static class AgentRunner
    {
        private const int MaxTurns = 20;

        private const string SystemPrompt =
            "You are a Gmail agent. You help users manage their Gmail inbox.\n\n" +
            "## TOOLS\n" +
            "- gmail_search: Search emails by query (returns list of messages with metadata)\n" +
            "- gmail_read: Read a specific email (get full content with body and attachments info)\n" +
            "- gmail_send: Send or reply to an email (auto-drafts if recipient outside whitelist)\n" +
            "- gmail_modify: Add/remove labels, mark as read/unread, archive\n" +
            "- gmail_attachment: Download a specific attachment\n\n" +
            "## DISCOVERY-FIRST WORKFLOW\n" +
            "Always follow this order:\n" +
            "1. SEARCH first to find relevant emails (never assume message IDs)\n" +
            "2. READ to get full content before replying\n" +
            "3. ACT after understanding the full context\n\n" +
            "## RULES\n" +
            "- Never invent email content - always read the email first before replying\n" +
            "- When replying, use thread_id and in_reply_to from the original email\n" +
            "- gmail_send auto-drafts if recipient is outside the whitelist\n" +
            "- Use gmail_search with standard Gmail query syntax (from:, to:, subject:, is:unread, etc.)\n\n" +
            "Be helpful and precise. Confirm actions before executing them.";

        public static void InitConversation(List<object> conversation)
        {
            conversation.Add(new
            {
                type    = "message",
                role    = "system",
                content = SystemPrompt
            });
        }

        public static async Task<AgentRunResult> RunAsync(
            string model,
            string userMessage,
            List<GmailToolDefinition> tools,
            List<object> conversation)
        {
            string resolvedModel = AiConfig.ResolveModel(model);

            conversation.Add(new
            {
                type    = "message",
                role    = "user",
                content = userMessage
            });

            JArray toolsArray = BuildToolsArray(tools);

            var handlers = new Dictionary<string, Func<JObject, Task<object>>>(
                StringComparer.OrdinalIgnoreCase);
            foreach (var tool in tools)
                handlers[tool.Name] = tool.Handler;

            string finalText = string.Empty;

            for (int turn = 0; turn < MaxTurns; turn++)
            {
                ColorLine("\n[agent] Turn " + (turn + 1) + "/" + MaxTurns, ConsoleColor.Cyan);

                var body = new JObject
                {
                    ["model"] = resolvedModel,
                    ["input"] = JArray.FromObject(conversation),
                    ["tools"] = toolsArray
                };

                string responseJson = await PostRawAsync(body.ToString(Formatting.None));

                JObject parsed;
                try { parsed = JObject.Parse(responseJson); }
                catch (Exception ex)
                {
                    finalText = "Agent error: failed to parse API response – " + ex.Message;
                    break;
                }

                if (parsed["error"] != null)
                {
                    finalText = "Agent error: " + (parsed["error"]["message"]?.ToString() ?? "Unknown error");
                    break;
                }

                var usage = parsed["usage"];
                if (usage != null)
                {
                    ColorLine(
                        "[agent] Tokens: in=" + usage["input_tokens"] + " out=" + usage["output_tokens"],
                        ConsoleColor.DarkGray);
                }

                var toolCalls = new List<JObject>();
                var outputArray = parsed["output"] as JArray;
                if (outputArray != null)
                {
                    foreach (JToken item in outputArray)
                    {
                        if (item["type"]?.ToString() == "function_call")
                            toolCalls.Add((JObject)item);
                    }
                }

                if (toolCalls.Count == 0)
                {
                    finalText = ExtractText(parsed);
                    ColorLine("[agent] Completed", ConsoleColor.Green);

                    conversation.Add(new
                    {
                        type    = "message",
                        role    = "assistant",
                        content = finalText
                    });

                    return new AgentRunResult
                    {
                        Text               = finalText,
                        Turns              = turn + 1,
                        ConversationHistory = conversation
                    };
                }

                // Append function_call items to conversation
                if (outputArray != null)
                {
                    foreach (JObject call in outputArray)
                    {
                        if (call["type"]?.ToString() == "function_call")
                        {
                            conversation.Add(new
                            {
                                type      = "function_call",
                                call_id   = call["call_id"]?.ToString(),
                                name      = call["name"]?.ToString(),
                                arguments = call["arguments"]?.ToString() ?? "{}"
                            });
                        }
                    }
                }

                // Execute tool calls; append results to conversation
                foreach (JObject call in toolCalls)
                {
                    string toolName = call["name"]?.ToString();
                    string callId   = call["call_id"]?.ToString();

                    JObject args;
                    try { args = JObject.Parse(call["arguments"]?.ToString() ?? "{}"); }
                    catch { args = new JObject(); }

                    ColorLine(
                        "[agent] Tool: " + toolName + "(" + Truncate(args.ToString(Formatting.None), 120) + ")",
                        ConsoleColor.DarkYellow);

                    string result;
                    try
                    {
                        Func<JObject, Task<object>> handler;
                        if (!handlers.TryGetValue(toolName, out handler))
                        {
                            result = JsonConvert.SerializeObject(new { error = "Unknown tool: " + toolName });
                        }
                        else
                        {
                            object resultObj = await handler(args);
                            result = resultObj is string s
                                ? s
                                : JsonConvert.SerializeObject(resultObj, Formatting.None);
                        }
                    }
                    catch (Exception ex)
                    {
                        result = JsonConvert.SerializeObject(new { error = ex.Message });
                    }

                    ColorLine("[agent]   -> " + Truncate(result, 200), ConsoleColor.DarkGray);

                    conversation.Add(new
                    {
                        type    = "function_call_output",
                        call_id = callId,
                        output  = result
                    });
                }
            }

            return new AgentRunResult
            {
                Text               = string.IsNullOrEmpty(finalText)
                    ? "Agent exceeded maximum turns (" + MaxTurns + ")."
                    : finalText,
                Turns              = MaxTurns,
                ConversationHistory = conversation
            };
        }

        // ----------------------------------------------------------------
        // Helpers
        // ----------------------------------------------------------------

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
                                    string text = part["text"]?.ToString();
                                    if (!string.IsNullOrEmpty(text)) return text;
                                }
                            }
                        }
                    }
                }
            }

            return string.Empty;
        }

        private static JArray BuildToolsArray(List<GmailToolDefinition> tools)
        {
            var arr = new JArray();
            foreach (var tool in tools)
            {
                arr.Add(new JObject
                {
                    ["type"]        = "function",
                    ["name"]        = tool.Name,
                    ["description"] = tool.Description,
                    ["parameters"]  = tool.Parameters ?? new JObject
                    {
                        ["type"]                 = "object",
                        ["properties"]           = new JObject(),
                        ["additionalProperties"] = false
                    }
                });
            }
            return arr;
        }

        private static async Task<string> PostRawAsync(string jsonBody)
        {
            using (var http = new HttpClient())
            {
                http.Timeout = TimeSpan.FromMinutes(5);
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

                using (var content = new StringContent(jsonBody, Encoding.UTF8, "application/json"))
                using (var response = await http.PostAsync(AiConfig.ApiEndpoint, content))
                {
                    return await response.Content.ReadAsStringAsync();
                }
            }
        }

        private static string Truncate(string s, int max)
            => s != null && s.Length > max ? s.Substring(0, max) + "..." : s ?? string.Empty;

        private static void ColorLine(string text, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(text);
            Console.ResetColor();
        }
    }
}
