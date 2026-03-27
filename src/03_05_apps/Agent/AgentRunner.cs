using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using FourthDevs.Apps.Models;
using FourthDevs.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Apps.Agent
{
    internal static class AgentRunner
    {
        private const string Instructions =
            "You are a CLI assistant with one optional tool: open_list_manager.\n" +
            "open_list_manager ONLY opens a browser UI — it does NOT add, remove, or modify any items.\n" +
            "You cannot change list data yourself. The user must edit items in the browser UI.\n" +
            "NEVER claim you have added, removed, or changed any item. Only say you opened the UI.\n" +
            "Use open_list_manager for any request to manage, edit, update, or review todo/shopping lists.\n" +
            "If the user asks for todo only, set focus=todo.\n" +
            "If the user asks for shopping only, set focus=shopping.\n" +
            "If both or unclear, set focus=todo.\n" +
            "For unrelated conversation, do not call tools.\n" +
            "Keep responses concise and practical.";

        private static readonly JObject ToolDefinition = new JObject
        {
            ["type"]        = "function",
            ["name"]        = "open_list_manager",
            ["description"] = "Open browser UI to manage todo/shopping lists stored in markdown files.",
            ["parameters"]  = new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["focus"] = new JObject
                    {
                        ["type"]        = "string",
                        ["enum"]        = new JArray { "todo", "shopping" },
                        ["description"] = "Preferred section to focus in UI."
                    }
                }
            }
        };

        public static async Task<AgentTurnResult> RunTurnAsync(string userMessage, string listsSummary)
        {
            string model = AiConfig.ResolveModel("gpt-4.1");

            string userContent = string.IsNullOrWhiteSpace(listsSummary)
                ? userMessage
                : userMessage + "\n\n[Lists summary: " + listsSummary + "]";

            var body = new JObject
            {
                ["model"]        = model,
                ["instructions"] = Instructions,
                ["input"]        = new JArray
                {
                    new JObject
                    {
                        ["type"]    = "message",
                        ["role"]    = "user",
                        ["content"] = userContent
                    }
                },
                ["tools"]               = new JArray { ToolDefinition },
                ["reasoning"]           = new JObject { ["effort"] = "high" },
                ["parallel_tool_calls"] = false
            };

            string responseJson = await PostRawAsync(body.ToString(Formatting.None));

            JObject parsed;
            try
            {
                parsed = JObject.Parse(responseJson);
            }
            catch (Exception ex)
            {
                return new AgentTurnResult { Kind = "chat", Text = "Error parsing API response: " + ex.Message };
            }

            if (parsed["error"] != null)
            {
                string errMsg = parsed["error"]["message"]?.ToString() ?? "Unknown error";
                return new AgentTurnResult { Kind = "chat", Text = "API error: " + errMsg };
            }

            // Check for tool call in output
            var outputArray = parsed["output"] as JArray;
            if (outputArray != null)
            {
                foreach (JToken item in outputArray)
                {
                    if (item["type"]?.ToString() == "function_call" &&
                        item["name"]?.ToString() == "open_list_manager")
                    {
                        string focus = "todo";
                        string argsStr = item["arguments"]?.ToString() ?? "{}";
                        try
                        {
                            var args = JObject.Parse(argsStr);
                            string focusArg = args["focus"]?.ToString();
                            if (!string.IsNullOrWhiteSpace(focusArg))
                                focus = focusArg;
                        }
                        catch { }

                        // Get the assistant text (may be in reasoning summary or a text message)
                        string followupText = ExtractText(parsed);
                        if (string.IsNullOrWhiteSpace(followupText))
                            followupText = "Opening the list manager…";

                        return new AgentTurnResult
                        {
                            Kind  = "open_manager",
                            Text  = followupText,
                            Focus = focus
                        };
                    }
                }
            }

            // No tool call → plain chat
            string text = ExtractText(parsed);
            if (string.IsNullOrWhiteSpace(text))
                text = "(no response)";

            return new AgentTurnResult { Kind = "chat", Text = text };
        }

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
                http.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", AiConfig.ApiKey);

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
