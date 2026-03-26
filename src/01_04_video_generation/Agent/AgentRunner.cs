using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using FourthDevs.Common;
using FourthDevs.VideoGeneration.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.VideoGeneration.Agent
{
    /// <summary>
    /// Runs the video-generation agent loop using the OpenAI Responses API.
    /// Maintains a multi-turn conversation history so the user can ask follow-up questions.
    /// </summary>
    internal static class AgentRunner
    {
        private const int MaxTurns = 20;

        private const string SystemPrompt =
            "You are a video generation agent using JSON-based prompting for consistent frame generation.\n\n" +
            "## WORKFLOW\n\n" +
            "### Step 1: Generate START Frame\n" +
            "1. Copy workspace/template.json → workspace/prompts/{scene}_{timestamp}.json\n" +
            "2. Edit ONLY the \"subject\" section for the STARTING pose/state\n" +
            "3. Read complete JSON, pass to create_image (aspect_ratio: \"16:9\", image_size: \"2k\")\n" +
            "4. Output: {scene}_frame_start_{timestamp}.png\n\n" +
            "### Step 2: Generate END Frame (from start frame)\n" +
            "1. Use create_image with reference_images: [start_frame_path]\n" +
            "2. Prompt describes the END state while referencing the start frame for character consistency\n" +
            "3. Output: {scene}_frame_end_{timestamp}.png\n\n" +
            "### Step 3: Generate Video\n" +
            "Use image_to_video with BOTH frames:\n" +
            "- start_image: path to start frame\n" +
            "- end_image: path to end frame\n" +
            "- prompt: describes the motion between frames\n\n" +
            "## RULES\n" +
            "- START + END: Always generate both frames for better video control\n" +
            "- END FROM START: Use start frame as reference when creating end frame\n" +
            "- COPY FIRST: Create new prompt file, never edit template.json directly\n" +
            "- MINIMAL EDITS: Only edit \"subject\" section, preserve style/colors/composition\n" +
            "- 16:9 FOR VIDEO: Always use 16:9 aspect ratio\n\n" +
            "## FILE NAMING\n" +
            "- Start frame: {scene}_frame_start_{timestamp}.png\n" +
            "- End frame: {scene}_frame_end_{timestamp}.png\n" +
            "- Video: {scene}_video_{timestamp}.mp4\n\n" +
            "## DEFAULTS\n" +
            "- Duration: 10 seconds\n" +
            "- Aspect ratio: 16:9\n\n" +
            "Run autonomously. Report all output paths when complete.";

        /// <summary>Adds the system prompt to a fresh conversation list.</summary>
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
            List<VideoGenToolDefinition> tools,
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
                var toolCalls    = new List<JObject>();
                var outputArray  = parsed["output"] as JArray;
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

                    conversation.Add(new
                    {
                        type    = "message",
                        role    = "assistant",
                        content = text
                    });

                    return text;
                }

                // Append function_call items to conversation
                foreach (JObject call in toolCalls)
                {
                    conversation.Add(new
                    {
                        type      = "function_call",
                        call_id   = call["call_id"]?.ToString(),
                        name      = call["name"]?.ToString(),
                        arguments = call["arguments"]?.ToString() ?? "{}"
                    });
                }

                // Execute tool calls; append results to conversation
                foreach (JObject call in toolCalls)
                {
                    string toolName = call["name"]?.ToString();
                    string callId   = call["call_id"]?.ToString();

                    JObject toolArgs;
                    try { toolArgs = JObject.Parse(call["arguments"]?.ToString() ?? "{}"); }
                    catch { toolArgs = new JObject(); }

                    ColorLine(
                        "[agent] Tool: " + toolName + "(" + Truncate(toolArgs.ToString(Formatting.None), 120) + ")",
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
                            object resultObj = await handler(toolArgs);
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

        private static JArray BuildToolsArray(List<VideoGenToolDefinition> tools)
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
