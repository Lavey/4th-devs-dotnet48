using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FourthDevs.Awareness.Core;
using FourthDevs.Awareness.Models;
using FourthDevs.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Awareness.Agent
{
    internal static class ScoutRunner
    {
        private const int MaxTurns = 10;

        public static async Task<string> RunAsync(string goal, string userMessage, string previousResponseId)
        {
            string templatePath = Path.Combine(WorkspaceInit.BaseDir, "templates", "scout.agent.md");
            AgentTemplate template = TemplateLoader.Load(templatePath);
            string resolvedModel = AiConfig.ResolveModel(template.Model ?? "gpt-4.1");

            string workspaceDir = Path.Combine(WorkspaceInit.BaseDir, "workspace");
            string indexPath = Path.Combine(workspaceDir, "system", "index.md");
            string workspaceIndex = File.Exists(indexPath) ? File.ReadAllText(indexPath) : "(no index available)";

            var tools = BuildFileTools(workspaceDir);
            JArray toolsArray = BuildToolsArray(tools);
            var handlers = new Dictionary<string, Func<JObject, Task<object>>>(StringComparer.OrdinalIgnoreCase);
            foreach (var tool in tools)
                handlers[tool.Name] = tool.Handler;

            string userContent = $"Workspace Index:\n{workspaceIndex}\n\nGoal: {goal}\n\nUser message: {userMessage}";
            JArray input = new JArray
            {
                new JObject
                {
                    ["type"] = "message",
                    ["role"] = "user",
                    ["content"] = userContent
                }
            };

            string currentResponseId = previousResponseId;
            string finalText = string.Empty;

            for (int turn = 0; turn < MaxTurns; turn++)
            {
                var body = new JObject
                {
                    ["model"] = resolvedModel,
                    ["store"] = true,
                    ["input"] = input
                };

                if (toolsArray.Count > 0)
                    body["tools"] = toolsArray;

                if (currentResponseId == null)
                    body["instructions"] = template.SystemPrompt;
                else
                    body["previous_response_id"] = currentResponseId;

                JObject parsed = await ApiClient.PostAsync(body);

                if (parsed["error"] != null)
                    return "Scout error: " + (parsed["error"]["message"]?.ToString() ?? "unknown");

                currentResponseId = parsed["id"]?.ToString();

                var toolCalls = new List<JObject>();
                JArray outputArray = parsed["output"] as JArray;
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
                    break;
                }

                input = new JArray();
                foreach (JObject call in toolCalls)
                {
                    string toolName = call["name"]?.ToString();
                    string callId = call["call_id"]?.ToString();

                    JObject args;
                    try { args = JObject.Parse(call["arguments"]?.ToString() ?? "{}"); }
                    catch { args = new JObject(); }

                    string result;
                    try
                    {
                        Func<JObject, Task<object>> handler;
                        if (!handlers.TryGetValue(toolName, out handler))
                            result = JsonConvert.SerializeObject(new { error = "Unknown tool: " + toolName });
                        else
                        {
                            object resultObj = await handler(args);
                            result = resultObj is string s ? s : JsonConvert.SerializeObject(resultObj, Formatting.None);
                        }
                    }
                    catch (Exception ex)
                    {
                        result = JsonConvert.SerializeObject(new { error = ex.Message });
                    }

                    input.Add(new JObject
                    {
                        ["type"] = "function_call_output",
                        ["call_id"] = callId,
                        ["output"] = result
                    });
                }
            }

            await SaveScoutNotesAsync(goal, finalText);

            return finalText;
        }

        private static async Task SaveScoutNotesAsync(string goal, string findings)
        {
            string notesDir = Path.Combine(WorkspaceInit.BaseDir, "workspace", "notes", "scout");
            if (!Directory.Exists(notesDir)) Directory.CreateDirectory(notesDir);
            string timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            string notePath = Path.Combine(notesDir, $"scan-{timestamp}.md");
            string content = $"# Scout Scan\n\n**Goal:** {goal}\n\n**Findings:**\n\n{findings}\n";
            using (var writer = new StreamWriter(notePath, append: false))
                await writer.WriteAsync(content);
        }

        private static List<LocalToolDefinition> BuildFileTools(string workspaceDir)
        {
            return new List<LocalToolDefinition>
            {
                new LocalToolDefinition
                {
                    Name = "files__list_files",
                    Description = "List files in a workspace subdirectory. Pass a relative path from workspace root (e.g. 'profile/user' or '' for root).",
                    Parameters = JObject.Parse(@"{
                        ""type"": ""object"",
                        ""properties"": {
                            ""path"": { ""type"": ""string"", ""description"": ""Relative path inside workspace (empty string for root)"" }
                        },
                        ""required"": [""path""]
                    }"),
                    Handler = async (args) =>
                    {
                        string relPath = args["path"]?.ToString() ?? string.Empty;
                        string fullDir = string.IsNullOrWhiteSpace(relPath)
                            ? workspaceDir
                            : Path.GetFullPath(Path.Combine(workspaceDir, relPath.Replace('/', Path.DirectorySeparatorChar)));

                        // Guard against path traversal
                        if (!fullDir.StartsWith(workspaceDir, StringComparison.OrdinalIgnoreCase))
                            return "Access denied: path is outside workspace.";

                        if (!Directory.Exists(fullDir))
                            return $"Directory not found: {relPath}";

                        var files = Directory.GetFiles(fullDir, "*", SearchOption.AllDirectories);
                        var relPaths = new List<string>();
                        foreach (string f in files)
                        {
                            string rel = f.Substring(workspaceDir.Length).TrimStart(Path.DirectorySeparatorChar).Replace(Path.DirectorySeparatorChar, '/');
                            relPaths.Add(rel);
                        }
                        await Task.FromResult(0);
                        return string.Join("\n", relPaths);
                    }
                },
                new LocalToolDefinition
                {
                    Name = "files__read_file",
                    Description = "Read the contents of a workspace file. Pass a relative path from workspace root (e.g. 'profile/user/identity.md').",
                    Parameters = JObject.Parse(@"{
                        ""type"": ""object"",
                        ""properties"": {
                            ""path"": { ""type"": ""string"", ""description"": ""Relative file path inside workspace"" }
                        },
                        ""required"": [""path""]
                    }"),
                    Handler = async (args) =>
                    {
                        string relPath = args["path"]?.ToString() ?? string.Empty;
                        string fullPath = Path.GetFullPath(Path.Combine(workspaceDir, relPath.Replace('/', Path.DirectorySeparatorChar)));

                        // Guard against path traversal
                        if (!fullPath.StartsWith(workspaceDir, StringComparison.OrdinalIgnoreCase))
                            return "Access denied: path is outside workspace.";

                        if (!File.Exists(fullPath))
                            return $"File not found: {relPath}";
                        await Task.FromResult(0);
                        return File.ReadAllText(fullPath);
                    }
                }
            };
        }

        private static JArray BuildToolsArray(List<LocalToolDefinition> tools)
        {
            var arr = new JArray();
            foreach (var tool in tools)
            {
                arr.Add(new JObject
                {
                    ["type"] = "function",
                    ["name"] = tool.Name,
                    ["description"] = tool.Description,
                    ["parameters"] = tool.Parameters ?? new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject(),
                        ["additionalProperties"] = false
                    }
                });
            }
            return arr;
        }

        private static string ExtractText(JObject parsed)
        {
            string outputText = parsed["output_text"]?.ToString();
            if (!string.IsNullOrWhiteSpace(outputText)) return outputText;

            JArray outputArray = parsed["output"] as JArray;
            if (outputArray != null)
            {
                foreach (JToken item in outputArray)
                {
                    if (item["type"]?.ToString() == "message")
                    {
                        JArray content = item["content"] as JArray;
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
    }
}
