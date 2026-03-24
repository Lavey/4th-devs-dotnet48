using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using FourthDevs.Common;
using FourthDevs.Common.Models;
using FourthDevs.Events.Config;
using FourthDevs.Events.Helpers;
using FourthDevs.Events.Memory;
using FourthDevs.Events.Mcp;
using FourthDevs.Events.Models;
using FourthDevs.Events.Tools;

namespace FourthDevs.Events.Core
{
    /// <summary>
    /// Runs a single agent session: load template, build tools, call Responses API in a loop.
    /// </summary>
    internal static class AgentRunner
    {
        private const int MaxTurns = 16;

        public static Session CreateFreshSession(string agentName)
        {
            return new Session
            {
                Id = agentName + "-" + Guid.NewGuid().ToString("N").Substring(0, 8),
                Messages = new List<JObject>(),
                Memory = new MemoryState()
            };
        }

        public static AgentTemplate LoadAgentTemplate(string agentName)
        {
            return AgentTemplateHelper.LoadFromWorkspace(agentName);
        }

        public static async Task<AgentRunResult> RunAgent(
            string agentName,
            string taskBody,
            string workspacePath,
            McpManager mcpManager,
            CancellationToken ct)
        {
            var usage = new AgentUsage();
            try
            {
                var template = LoadAgentTemplate(agentName);
                var session = CreateFreshSession(agentName);

                // Add system message
                var sysMsg = new JObject
                {
                    ["role"] = "system",
                    ["content"] = template.SystemPrompt + "\n\n" + WorkspaceNav.WORKSPACE_NAV_INSTRUCTIONS
                };
                session.Messages.Add(sysMsg);

                // Add user message with task
                var userMsg = new JObject
                {
                    ["role"] = "user",
                    ["content"] = taskBody
                };
                session.Messages.Add(userMsg);

                // Resolve tools
                var tools = ToolRegistry.ResolveTools(template.Tools, mcpManager);

                var ctx = new ToolRuntimeContext
                {
                    Agent = agentName,
                    WorkspacePath = workspacePath,
                    AbortSignal = ct
                };

                string lastResponse = string.Empty;

                for (int turn = 0; turn < MaxTurns; turn++)
                {
                    ct.ThrowIfCancellationRequested();

                    // Process memory
                    session = await Processor.ProcessMemory(session, template.Model);
                    usage.Turns++;

                    // Call Responses API
                    var apiResult = await AgentResponseLoop.CallResponses(
                        session, template.Model, tools);

                    if (apiResult.ActualTokens > 0)
                        usage.TotalActualTokens += apiResult.ActualTokens;
                    usage.TotalEstimatedTokens += apiResult.EstimatedTokens;

                    // Add assistant output to session
                    foreach (var outputMsg in apiResult.OutputMessages)
                    {
                        session.Messages.Add(outputMsg);
                    }

                    if (!string.IsNullOrEmpty(apiResult.TextContent))
                        lastResponse = apiResult.TextContent;

                    // If no tool calls, we're done
                    if (apiResult.ToolCalls == null || apiResult.ToolCalls.Count == 0)
                        break;

                    // Execute tool calls
                    foreach (var call in apiResult.ToolCalls)
                    {
                        string toolName = call["name"]?.ToString() ?? string.Empty;
                        string callId = call["call_id"]?.ToString() ?? string.Empty;
                        string argsJson = call["arguments"]?.ToString() ?? "{}";

                        JObject args;
                        try { args = JObject.Parse(argsJson); }
                        catch { args = new JObject(); }

                        var toolResult = await AgentResponseLoop.ExecuteToolCall(
                            toolName, args, tools, mcpManager, ctx);

                        if (toolResult.Kind == "human_request")
                        {
                            // Add tool output to session
                            var toolMsg = new JObject
                            {
                                ["type"] = "function_call_output",
                                ["call_id"] = callId,
                                ["output"] = toolResult.Content
                            };
                            session.Messages.Add(toolMsg);

                            return new AgentRunResult
                            {
                                Status = "waiting-human",
                                Response = lastResponse,
                                WaitId = toolResult.WaitId,
                                WaitQuestion = toolResult.Question,
                                Usage = usage
                            };
                        }

                        var resultMsg = new JObject
                        {
                            ["type"] = "function_call_output",
                            ["call_id"] = callId,
                            ["output"] = toolResult.Content
                        };
                        session.Messages.Add(resultMsg);
                    }
                }

                return new AgentRunResult
                {
                    Status = "done",
                    Response = lastResponse,
                    Usage = usage
                };
            }
            catch (OperationCanceledException)
            {
                return new AgentRunResult
                {
                    Status = "failed",
                    Error = "Cancelled",
                    Usage = usage
                };
            }
            catch (Exception ex)
            {
                Logger.Error("agent", agentName + " failed: " + ex.Message);
                return new AgentRunResult
                {
                    Status = "failed",
                    Error = ex.Message,
                    Usage = usage
                };
            }
        }
    }
}
