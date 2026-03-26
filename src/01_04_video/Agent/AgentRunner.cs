using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using FourthDevs.Common;
using FourthDevs.Video.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Video.Agent
{
    /// <summary>
    /// Runs the video-processing agent loop using the OpenAI Responses API.
    /// Maintains a multi-turn conversation history so the user can ask follow-up
    /// questions without re-specifying the video.
    /// </summary>
    internal static class AgentRunner
    {
        private const int MaxTurns = 20;

        private const string SystemPrompt =
            "You are an autonomous video processing agent.\n\n" +
            "## GOAL\n" +
            "Process, analyze, transcribe, and extract information from videos. Handle both local files and YouTube URLs.\n\n" +
            "## RESOURCES\n" +
            "- workspace/input/   → Source video files to process\n" +
            "- workspace/output/  → Generated analysis, transcriptions, extractions (JSON)\n\n" +
            "## TOOLS\n" +
            "- analyze_video: Analyze video content (visual, audio, action, general)\n" +
            "- transcribe_video: Transcribe speech with timestamps and speaker detection\n" +
            "- extract_video: Extract scenes, keyframes, objects, or on-screen text\n" +
            "- query_video: Ask any custom question about video content\n\n" +
            "## VIDEO INPUT\n" +
            "Supported sources:\n" +
            "- Local files: workspace/input/video.mp4\n" +
            "- YouTube URLs: https://www.youtube.com/watch?v=... or https://youtu.be/...\n\n" +
            "## WORKFLOW\n" +
            "1. UNDERSTAND THE REQUEST - What does the user need? Which tool fits?\n" +
            "2. CHOOSE THE RIGHT TOOL and call it\n" +
            "3. PROCESS AND DELIVER - Use timestamps (MM:SS) when referencing moments\n\n" +
            "## RULES\n" +
            "1. YouTube URLs work directly - no download needed\n" +
            "2. Large files (>20MB) are uploaded automatically\n" +
            "3. Use clipping for long videos to reduce processing time\n" +
            "4. Reference timestamps in MM:SS format\n\n" +
            "Run autonomously. Report results with timestamps.";

        /// <summary>
        /// Adds the system prompt to a fresh conversation list.
        /// </summary>
        public static void InitConversation(List<object> conversation)
        {
            conversation.Add(new
            {
                type    = "message",
                role    = "system",
                content = SystemPrompt
            });
        }

        /// <summary>
        /// Adds the user message, runs the agent loop, and returns the final text response.
        /// The conversation list is updated in-place so follow-up questions retain history.
        /// </summary>
        public static async Task<string> RunAsync(
            string model,
            string userMessage,
            List<VideoToolDefinition> tools,
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
                    return "Agent error: failed to parse API response – " + ex.Message;
                }

                if (parsed["error"] != null)
                {
                    string errMsg = parsed["error"]["message"]?.ToString() ?? "Unknown error";
                    return "Agent error: " + errMsg;
                }

                // Log token usage
                var usage = parsed["usage"];
                if (usage != null)
                {
                    ColorLine(
                        "[agent] Tokens: in=" + usage["input_tokens"] + " out=" + usage["output_tokens"],
                        ConsoleColor.DarkGray);
                }

                // Collect tool calls from output
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

                // No tool calls → final answer
                if (toolCalls.Count == 0)
                {
                    string text = ExtractText(parsed);
                    ColorLine("[agent] Completed", ConsoleColor.Green);

                    // Append assistant reply to conversation for multi-turn
                    conversation.Add(new
                    {
                        type    = "message",
                        role    = "assistant",
                        content = text
                    });

                    return text;
                }

                // Append function_call items to conversation
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

            return "Agent exceeded maximum turns (" + MaxTurns + ").";
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

        private static JArray BuildToolsArray(List<VideoToolDefinition> tools)
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
