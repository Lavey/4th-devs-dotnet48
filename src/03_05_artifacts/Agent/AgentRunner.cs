using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FourthDevs.Artifacts.Core;
using FourthDevs.Artifacts.Models;
using FourthDevs.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Artifacts.Agent
{
    internal static class AgentRunner
    {
        private static readonly string AgentInstructions =
@"You are a CLI agent with two optional tools: create_artifact and edit_artifact.
Use create_artifact ONLY when the user explicitly asks you to build/generate/create a visual or interactive artifact.
Use edit_artifact ONLY when the user asks to modify the currently rendered artifact.
For greetings, small talk, or normal questions, DO NOT call tools and respond conversationally.
If a request is ambiguous, ask a concise clarifying question instead of calling tools.
When using create_artifact, choose the minimal packs needed for the request.
If the user asks for Tailwind/utility-first styling, include the tailwind pack.
When using edit_artifact, emit concrete search/replace operations. Avoid vague edits.
Keep responses concise.";

        private static readonly string[] AllPackIds = ArtifactCapabilities.GetAllPackIds();

        public static async Task<AgentTurnResult> RunTurnAsync(
            string userMessage,
            ArtifactDocument currentArtifact,
            string serverBaseUrl)
        {
            JArray tools = BuildToolsArray(currentArtifact);

            var inputArray = new JArray
            {
                new JObject
                {
                    ["type"] = "message",
                    ["role"] = "user",
                    ["content"] = userMessage
                }
            };

            // Phase 1: routing call
            var body = new JObject
            {
                ["model"] = AiConfig.ResolveModel("gpt-4.1"),
                ["instructions"] = AgentInstructions,
                ["input"] = inputArray,
                ["tools"] = tools,
                ["reasoning"] = new JObject { ["effort"] = "high" },
                ["parallel_tool_calls"] = false
            };

            JObject response = await ApiClient.PostAsync(body);
            string responseId = response["id"]?.ToString();

            List<JObject> toolCalls = ApiClient.GetToolCalls(response);

            // No tool call → plain chat response
            if (toolCalls.Count == 0)
            {
                string text = ApiClient.ExtractText(response);
                return new AgentTurnResult
                {
                    Kind = "chat",
                    Text = text
                };
            }

            JObject call = toolCalls[0];
            string toolName = call["name"]?.ToString();
            string callId = call["call_id"]?.ToString();

            JObject args;
            try { args = JObject.Parse(call["arguments"]?.ToString() ?? "{}"); }
            catch { args = new JObject(); }

            // Execute the tool
            AgentTurnResult result;
            string toolOutput;

            if (toolName == "create_artifact")
            {
                result = await ExecuteCreateArtifact(args, serverBaseUrl);
                toolOutput = result.Artifact != null
                    ? JsonConvert.SerializeObject(new
                    {
                        success = true,
                        id = result.Artifact.Id,
                        title = result.Artifact.Title
                    })
                    : JsonConvert.SerializeObject(new { success = false, error = result.Text });
            }
            else if (toolName == "edit_artifact")
            {
                result = ExecuteEditArtifact(args, currentArtifact);
                toolOutput = result.Artifact != null
                    ? JsonConvert.SerializeObject(new
                    {
                        success = true,
                        id = result.Artifact.Id,
                        title = result.Artifact.Title
                    })
                    : JsonConvert.SerializeObject(new { success = false, error = result.Text });
            }
            else
            {
                // Unknown tool – treat as chat
                string text = ApiClient.ExtractText(response);
                return new AgentTurnResult { Kind = "chat", Text = text };
            }

            // Phase 2: complete the tool turn to get followup text
            var followupBody = new JObject
            {
                ["model"] = AiConfig.ResolveModel("gpt-4.1"),
                ["reasoning"] = new JObject { ["effort"] = "high" },
                ["previous_response_id"] = responseId,
                ["input"] = new JArray
                {
                    new JObject
                    {
                        ["type"] = "function_call_output",
                        ["call_id"] = callId,
                        ["output"] = toolOutput
                    }
                }
            };

            JObject followup = await ApiClient.PostAsync(followupBody);
            string followupText = ApiClient.ExtractText(followup);

            result.Text = followupText;
            return result;
        }

        private static async Task<AgentTurnResult> ExecuteCreateArtifact(JObject args, string serverBaseUrl)
        {
            string prompt = args["prompt"]?.ToString() ?? string.Empty;
            string[] packs = ParsePackIds(args["packs"]);

            try
            {
                ArtifactDocument artifact = await ArtifactGenerator.GenerateAsync(prompt, packs, serverBaseUrl);
                return new AgentTurnResult
                {
                    Kind = "artifact",
                    Action = "created",
                    Text = string.Empty,
                    Artifact = artifact
                };
            }
            catch (Exception ex)
            {
                return new AgentTurnResult
                {
                    Kind = "artifact",
                    Text = "Error generating artifact: " + ex.Message
                };
            }
        }

        private static AgentTurnResult ExecuteEditArtifact(JObject args, ArtifactDocument currentArtifact)
        {
            if (currentArtifact == null)
            {
                return new AgentTurnResult
                {
                    Kind = "artifact",
                    Text = "No artifact to edit. Ask me to create one first."
                };
            }

            string instructions = args["instructions"]?.ToString() ?? string.Empty;
            string title = args["title"]?.ToString();

            var replacements = new List<SearchReplaceOperation>();
            var ops = args["replacements"] as JArray;
            if (ops != null)
            {
                foreach (JToken op in ops)
                {
                    replacements.Add(new SearchReplaceOperation
                    {
                        Search = op["search"]?.ToString() ?? string.Empty,
                        Replace = op["replace"]?.ToString() ?? string.Empty,
                        ReplaceAll = op["replaceAll"]?.Value<bool>() ?? false,
                        UseRegex = op["useRegex"]?.Value<bool>() ?? false,
                        CaseSensitive = op["caseSensitive"]?.Value<bool?>(),
                        RegexFlags = op["regexFlags"]?.ToString()
                    });
                }
            }

            try
            {
                var editResult = ArtifactEditor.EditWithSearchReplace(
                    currentArtifact, replacements, instructions, title);

                return new AgentTurnResult
                {
                    Kind = "artifact",
                    Action = "edited",
                    Text = string.Empty,
                    Artifact = editResult.Artifact
                };
            }
            catch (Exception ex)
            {
                return new AgentTurnResult
                {
                    Kind = "artifact",
                    Text = "Error editing artifact: " + ex.Message
                };
            }
        }

        private static string[] ParsePackIds(JToken packsToken)
        {
            if (packsToken == null)
                return new[] { "core" };

            if (packsToken.Type == JTokenType.Array)
            {
                var list = new List<string>();
                foreach (JToken t in (JArray)packsToken)
                {
                    string id = t.ToString();
                    if (!string.IsNullOrWhiteSpace(id))
                        list.Add(id);
                }
                return list.Count > 0 ? list.ToArray() : new[] { "core" };
            }

            // Single string
            string single = packsToken.ToString();
            return !string.IsNullOrWhiteSpace(single)
                ? new[] { single }
                : new[] { "core" };
        }

        private static JArray BuildToolsArray(ArtifactDocument currentArtifact)
        {
            var packEnum = new JArray();
            foreach (string id in AllPackIds)
                packEnum.Add(id);

            var createParams = new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["prompt"] = new JObject
                    {
                        ["type"] = "string",
                        ["description"] = "Detailed description of what the artifact should do and look like."
                    },
                    ["packs"] = new JObject
                    {
                        ["type"] = "array",
                        ["description"] = "Capability packs to include. Always include 'core'. Add others as needed.",
                        ["items"] = new JObject
                        {
                            ["type"] = "string",
                            ["enum"] = packEnum
                        }
                    }
                },
                ["required"] = new JArray { "prompt", "packs" },
                ["additionalProperties"] = false
            };

            var replacementItem = new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["search"] = new JObject { ["type"] = "string", ["description"] = "Text or regex pattern to find." },
                    ["replace"] = new JObject { ["type"] = "string", ["description"] = "Replacement text." },
                    ["replaceAll"] = new JObject { ["type"] = "boolean", ["description"] = "Replace all occurrences. Default false." },
                    ["useRegex"] = new JObject { ["type"] = "boolean", ["description"] = "Treat search as regex. Default false." },
                    ["caseSensitive"] = new JObject { ["type"] = "boolean", ["description"] = "Case-sensitive match. Default true." },
                    ["regexFlags"] = new JObject { ["type"] = "string", ["description"] = "Regex flags: i, m, s." }
                },
                ["required"] = new JArray { "search", "replace" },
                ["additionalProperties"] = false
            };

            var editParams = new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["instructions"] = new JObject
                    {
                        ["type"] = "string",
                        ["description"] = "High-level description of the edit being performed."
                    },
                    ["title"] = new JObject
                    {
                        ["type"] = "string",
                        ["description"] = "Optional new title for the artifact."
                    },
                    ["replacements"] = new JObject
                    {
                        ["type"] = "array",
                        ["description"] = "List of search/replace operations to apply to the artifact HTML.",
                        ["items"] = replacementItem
                    }
                },
                ["required"] = new JArray { "instructions", "replacements" },
                ["additionalProperties"] = false
            };

            var tools = new JArray
            {
                new JObject
                {
                    ["type"] = "function",
                    ["name"] = "create_artifact",
                    ["description"] = "Generates a new self-contained HTML artifact based on a prompt.",
                    ["parameters"] = createParams
                }
            };

            if (currentArtifact != null)
            {
                tools.Add(new JObject
                {
                    ["type"] = "function",
                    ["name"] = "edit_artifact",
                    ["description"] = "Edits the currently displayed artifact using search/replace operations.",
                    ["parameters"] = editParams
                });
            }

            return tools;
        }
    }
}
