using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using FourthDevs.Common;
using FourthDevs.Common.Models;
using FourthDevs.Events.Core;
using FourthDevs.Events.Mcp;
using FourthDevs.Events.Models;

namespace FourthDevs.Events.Helpers
{
    /// <summary>
    /// Result of a single Responses API call.
    /// </summary>
    internal class ResponseLoopResult
    {
        public string TextContent { get; set; } = string.Empty;
        public List<JObject> ToolCalls { get; set; } = new List<JObject>();
        public List<JObject> OutputMessages { get; set; } = new List<JObject>();
        public int EstimatedTokens { get; set; }
        public int ActualTokens { get; set; }
    }

    /// <summary>
    /// Handles calling the Responses API, parsing output, and executing tool calls.
    /// </summary>
    internal static class AgentResponseLoop
    {
        public static async Task<ResponseLoopResult> CallResponses(
            Session session,
            string model,
            List<Models.Tool> tools)
        {
            var result = new ResponseLoopResult();

            // Build Responses API input
            var inputMessages = new List<InputMessage>();
            foreach (var msg in session.Messages)
            {
                string type = msg["type"]?.ToString();
                string role = msg["role"]?.ToString();

                if (type == "function_call_output")
                {
                    // Tool call outputs go as special input items
                    inputMessages.Add(new InputMessage
                    {
                        Type = "function_call_output",
                        Role = null,
                        Content = msg["output"]?.ToString() ?? ""
                    });
                    // Set call_id via the raw object handling below
                    continue;
                }

                if (!string.IsNullOrEmpty(role))
                {
                    inputMessages.Add(new InputMessage
                    {
                        Role = role,
                        Content = msg["content"]?.ToString() ?? ""
                    });
                }
            }

            // Build tool definitions
            List<FourthDevs.Common.Models.ToolDefinition> toolDefs = null;
            if (tools != null && tools.Count > 0)
            {
                toolDefs = new List<FourthDevs.Common.Models.ToolDefinition>();
                foreach (var t in tools)
                {
                    toolDefs.Add(new FourthDevs.Common.Models.ToolDefinition
                    {
                        Type = "function",
                        Name = t.Definition.Name,
                        Description = t.Definition.Description,
                        Parameters = t.Definition.Parameters
                    });
                }
            }

            // Estimate tokens
            int estimatedInput = 0;
            foreach (var m in inputMessages)
            {
                string c = m.Content as string;
                if (c != null)
                    estimatedInput += TokenEstimator.Estimate(c);
            }
            result.EstimatedTokens = estimatedInput;

            string resolvedModel = AiConfig.ResolveModel(model ?? "gpt-4.1");

            var request = new ResponsesRequest
            {
                Model = resolvedModel,
                Input = inputMessages,
                Tools = toolDefs
            };

            var response = await OpenAIClient.Instance.SendAsync(request);

            // Track actual tokens
            if (response.Usage != null)
            {
                result.ActualTokens = response.Usage.InputTokens + response.Usage.OutputTokens;
                TokenEstimator.Calibrate(estimatedInput, response.Usage.InputTokens);
            }

            // Parse output
            if (response.Output != null)
            {
                foreach (var item in response.Output)
                {
                    if (item.Type == "message" && item.Content != null)
                    {
                        foreach (var part in item.Content)
                        {
                            if (part.Type == "output_text" && !string.IsNullOrEmpty(part.Text))
                            {
                                result.TextContent = part.Text;
                            }
                        }

                        var msgObj = new JObject
                        {
                            ["role"] = "assistant",
                            ["content"] = result.TextContent
                        };
                        result.OutputMessages.Add(msgObj);
                    }
                    else if (item.Type == "function_call")
                    {
                        var callObj = new JObject
                        {
                            ["type"] = "function_call",
                            ["call_id"] = item.CallId,
                            ["name"] = item.Name,
                            ["arguments"] = item.Arguments
                        };
                        result.ToolCalls.Add(callObj);
                        result.OutputMessages.Add(callObj);
                    }
                }
            }

            // Fallback to OutputText
            if (string.IsNullOrEmpty(result.TextContent) && !string.IsNullOrEmpty(response.OutputText))
            {
                result.TextContent = response.OutputText;
            }

            return result;
        }

        public static async Task<ToolResult> ExecuteToolCall(
            string toolName,
            JObject args,
            List<Models.Tool> tools,
            McpManager mcpManager,
            ToolRuntimeContext ctx)
        {
            // Check if it's an MCP tool (contains __ separator)
            if (toolName.Contains("__") && mcpManager != null)
            {
                string mcpResult = await mcpManager.CallToolAsync(toolName, args);
                return new ToolResult { Kind = "text", Content = mcpResult };
            }

            // Find local tool
            var tool = tools.FirstOrDefault(t => t.Definition.Name == toolName);
            if (tool == null)
            {
                return new ToolResult
                {
                    Kind = "text",
                    Content = "Error: Unknown tool '" + toolName + "'"
                };
            }

            try
            {
                return await tool.Handler(args, ctx);
            }
            catch (Exception ex)
            {
                return new ToolResult
                {
                    Kind = "text",
                    Content = "Error executing " + toolName + ": " + ex.Message
                };
            }
        }
    }
}
