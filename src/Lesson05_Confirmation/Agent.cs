using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using FourthDevs.Common;
using FourthDevs.Common.Models;
using FourthDevs.Lesson05_Confirmation.Tools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Lesson05_Confirmation
{
    /// <summary>
    /// Runs the agentic loop for the confirmation agent.
    /// Mirrors 01_05_confirmation/src/agent.js in the source repo.
    /// </summary>
    internal static class AgentRunner
    {
        private const int MaxSteps = 50;

        // ----------------------------------------------------------------
        // Main agent loop
        // ----------------------------------------------------------------

        internal static async Task<string> RunAgentLoop(
            List<object> conversation,
            Func<string, JObject, Task<bool>> shouldRunTool)
        {
            var tools = ToolDefinitions.Build();

            for (int step = 0; step < MaxSteps; step++)
            {
                var body = new JObject
                {
                    ["model"] = AiConfig.ResolveModel(Model),
                    ["input"] = JArray.FromObject(conversation),
                    ["tools"] = JArray.FromObject(tools)
                };

                string responseJson = await PostRawAsync(body.ToString(Formatting.None));
                var parsed = JsonConvert.DeserializeObject<ResponsesResponse>(responseJson);

                if (parsed?.Error != null)
                    throw new InvalidOperationException(parsed.Error.Message);

                var toolCalls = ResponsesApiClient.GetToolCalls(parsed);

                if (toolCalls.Count == 0)
                {
                    string text = ResponsesApiClient.ExtractText(parsed);
                    foreach (var item in parsed.Output)
                    {
                        if (item.Type == "message")
                            conversation.Add(new { type = "message", role = "assistant", content = text });
                    }
                    return text;
                }

                // Append function_call items to conversation
                foreach (var item in parsed.Output)
                {
                    if (item.Type == "function_call")
                        conversation.Add(new
                        {
                            type      = "function_call",
                            call_id   = item.CallId,
                            name      = item.Name,
                            arguments = item.Arguments
                        });
                }

                // Execute tools (with confirmation for sensitive ones)
                foreach (var call in toolCalls)
                {
                    var args       = JObject.Parse(call.Arguments ?? "{}");
                    bool shouldRun = await shouldRunTool(call.Name, args);
                    object result;

                    if (shouldRun)
                    {
                        result = await ExecuteToolAsync(call.Name, args);
                    }
                    else
                    {
                        result = new
                        {
                            success  = false,
                            error    = "User rejected the action",
                            rejected = true
                        };
                    }

                    string resultJson = JsonConvert.SerializeObject(result);
                    string logPreview = resultJson.Length > 200
                        ? resultJson.Substring(0, 200) + "..."
                        : resultJson;

                    ColorLine(string.Format("  [tool] {0} → {1}", call.Name, logPreview),
                        ConsoleColor.DarkCyan);

                    conversation.Add(new
                    {
                        type    = "function_call_output",
                        call_id = call.CallId,
                        output  = resultJson
                    });
                }
            }

            throw new InvalidOperationException(
                string.Format("Agent loop did not finish within {0} steps.", MaxSteps));
        }

        // ----------------------------------------------------------------
        // Tool dispatcher
        // ----------------------------------------------------------------

        private static async Task<object> ExecuteToolAsync(string name, JObject args)
        {
            switch (name)
            {
                case "list_files":   return ToolExecutors.ExecuteListFiles(args);
                case "read_file":    return ToolExecutors.ExecuteReadFile(args);
                case "write_file":   return ToolExecutors.ExecuteWriteFile(args);
                case "search_files": return ToolExecutors.ExecuteSearchFiles(args);
                case "send_email":   return await ToolExecutors.ExecuteSendEmailAsync(args);
                default:
                    throw new InvalidOperationException("Unknown tool: " + name);
            }
        }

        // ----------------------------------------------------------------
        // HTTP helper (shared AI API call)
        // ----------------------------------------------------------------

        private static async Task<string> PostRawAsync(string jsonBody)
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

                using (var content = new StringContent(jsonBody, Encoding.UTF8, "application/json"))
                using (var response = await http.PostAsync(AiConfig.ApiEndpoint, content))
                {
                    return await response.Content.ReadAsStringAsync();
                }
            }
        }

        // ----------------------------------------------------------------
        // Console helper
        // ----------------------------------------------------------------

        private static void ColorLine(string text, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(text);
            Console.ResetColor();
        }

        // ----------------------------------------------------------------
        // Model constant (matches Program.cs constant)
        // ----------------------------------------------------------------

        private const string Model = "gpt-4.1-mini";
    }
}
