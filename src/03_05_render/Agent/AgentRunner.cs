using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FourthDevs.Common;
using FourthDevs.Render.Core;
using FourthDevs.Render.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Render.Agent
{
    internal static class AgentRunner
    {
        private static readonly string AgentInstructions =
@"You are a CLI render agent with two optional tools: create_render and edit_render.
Use create_render for requests to build/generate/create dashboards, reports, specs, layouts, or visualized data views.
Use edit_render for requests to modify/refine/update the current rendered document.
For normal questions, greetings, or non-render tasks, do NOT call tools and respond conversationally.
If the user request is ambiguous, ask a concise clarifying question instead of calling tools.
Prefer the minimal set of packs needed to satisfy the request.
Keep responses concise and practical.";

        private static readonly string[] AllPackIds = RenderCatalog.GetAllPackIds();

        public static async Task<AgentTurnResult> RunTurnAsync(
            string userMessage,
            RenderDocument currentDocument)
        {
            JArray tools = BuildToolsArray(currentDocument);

            var inputArray = new JArray
            {
                new JObject
                {
                    ["type"]    = "message",
                    ["role"]    = "user",
                    ["content"] = userMessage
                }
            };

            // Phase 1: routing call
            var body = new JObject
            {
                ["model"]               = AiConfig.ResolveModel("gpt-4.1"),
                ["instructions"]        = AgentInstructions,
                ["input"]               = inputArray,
                ["tools"]               = tools,
                ["reasoning"]           = new JObject { ["effort"] = "high" },
                ["parallel_tool_calls"] = false
            };

            JObject response = await ApiClient.PostAsync(body);
            string responseId = response["id"]?.ToString();

            List<JObject> toolCalls = ApiClient.GetToolCalls(response);

            // No tool call → plain chat response
            if (toolCalls.Count == 0)
            {
                string text = ApiClient.ExtractText(response);
                return new AgentTurnResult { Kind = "chat", Text = text };
            }

            JObject call    = toolCalls[0];
            string toolName = call["name"]?.ToString();
            string callId   = call["call_id"]?.ToString();

            JObject args;
            try { args = JObject.Parse(call["arguments"]?.ToString() ?? "{}"); }
            catch { args = new JObject(); }

            // Execute the tool
            AgentTurnResult result;
            string toolOutput;

            if (toolName == "create_render")
            {
                result = await ExecuteCreateRender(args);
                toolOutput = result.Document != null
                    ? JsonConvert.SerializeObject(new
                    {
                        success = true,
                        id      = result.Document.Id,
                        title   = result.Document.Title,
                        summary = result.Document.Summary
                    })
                    : JsonConvert.SerializeObject(new { success = false, error = result.Text });
            }
            else if (toolName == "edit_render")
            {
                result = await ExecuteEditRender(args, currentDocument);
                toolOutput = result.Document != null
                    ? JsonConvert.SerializeObject(new
                    {
                        success = true,
                        id      = result.Document.Id,
                        title   = result.Document.Title,
                        summary = result.Document.Summary
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
                ["model"]                = AiConfig.ResolveModel("gpt-4.1"),
                ["reasoning"]            = new JObject { ["effort"] = "high" },
                ["previous_response_id"] = responseId,
                ["input"] = new JArray
                {
                    new JObject
                    {
                        ["type"]    = "function_call_output",
                        ["call_id"] = callId,
                        ["output"]  = toolOutput
                    }
                }
            };

            JObject followup = await ApiClient.PostAsync(followupBody);
            string followupText = ApiClient.ExtractText(followup);

            result.Text = followupText;
            return result;
        }

        private static async Task<AgentTurnResult> ExecuteCreateRender(JObject args)
        {
            string prompt = args["prompt"]?.ToString() ?? string.Empty;
            string[] packs = ParsePackIds(args["packs"]);

            try
            {
                RenderDocument doc = await SpecGenerator.GenerateAsync(prompt, packs);
                return new AgentTurnResult
                {
                    Kind     = "render",
                    Text     = string.Empty,
                    Document = doc
                };
            }
            catch (Exception ex)
            {
                return new AgentTurnResult
                {
                    Kind = "render",
                    Text = "Error generating render document: " + ex.Message
                };
            }
        }

        private static async Task<AgentTurnResult> ExecuteEditRender(
            JObject args, RenderDocument currentDocument)
        {
            if (currentDocument == null)
            {
                return new AgentTurnResult
                {
                    Kind = "render",
                    Text = "No document to edit. Ask me to create one first."
                };
            }

            string instructions = args["instructions"]?.ToString() ?? string.Empty;
            string[] packs = currentDocument.Packs != null
                ? currentDocument.Packs.ToArray()
                : RenderCatalog.GetDefaultPackIds();

            // Build an edit prompt incorporating the original prompt and edit instructions
            string editPrompt = string.Format(
                "Original dashboard: {0}\n\nEdit instructions: {1}",
                currentDocument.Prompt,
                instructions);

            try
            {
                RenderDocument doc = await SpecGenerator.GenerateAsync(editPrompt, packs);
                // Preserve the original prompt reference
                doc.Prompt = currentDocument.Prompt;
                return new AgentTurnResult
                {
                    Kind     = "render",
                    Text     = string.Empty,
                    Document = doc
                };
            }
            catch (Exception ex)
            {
                return new AgentTurnResult
                {
                    Kind = "render",
                    Text = "Error editing render document: " + ex.Message
                };
            }
        }

        private static string[] ParsePackIds(JToken packsToken)
        {
            if (packsToken == null)
                return RenderCatalog.GetDefaultPackIds();

            if (packsToken.Type == JTokenType.Array)
            {
                var list = new List<string>();
                foreach (JToken t in (JArray)packsToken)
                {
                    string id = t.ToString();
                    if (!string.IsNullOrWhiteSpace(id))
                        list.Add(id);
                }
                return list.Count > 0 ? list.ToArray() : RenderCatalog.GetDefaultPackIds();
            }

            string single = packsToken.ToString();
            return !string.IsNullOrWhiteSpace(single)
                ? new[] { single }
                : RenderCatalog.GetDefaultPackIds();
        }

        private static JArray BuildToolsArray(RenderDocument currentDocument)
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
                        ["type"]        = "string",
                        ["description"] = "Detailed description of what the dashboard should show and contain."
                    },
                    ["packs"] = new JObject
                    {
                        ["type"]        = "array",
                        ["description"] = "Component packs to include. Prefer the minimal set needed.",
                        ["items"]       = new JObject
                        {
                            ["type"] = "string",
                            ["enum"] = packEnum
                        }
                    }
                },
                ["required"]             = new JArray { "prompt", "packs" },
                ["additionalProperties"] = false
            };

            var editParams = new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["instructions"] = new JObject
                    {
                        ["type"]        = "string",
                        ["description"] = "Detailed instructions describing what to change in the current document."
                    }
                },
                ["required"]             = new JArray { "instructions" },
                ["additionalProperties"] = false
            };

            var tools = new JArray
            {
                new JObject
                {
                    ["type"]        = "function",
                    ["name"]        = "create_render",
                    ["description"] = "Generates a new dashboard render document from a prompt.",
                    ["parameters"]  = createParams
                }
            };

            if (currentDocument != null)
            {
                tools.Add(new JObject
                {
                    ["type"]        = "function",
                    ["name"]        = "edit_render",
                    ["description"] = "Regenerates the current rendered document incorporating the edit instructions.",
                    ["parameters"]  = editParams
                });
            }

            return tools;
        }
    }
}
