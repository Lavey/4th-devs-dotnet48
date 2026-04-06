using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using FourthDevs.AgentGraph.Agents;
using FourthDevs.AgentGraph.Ai;
using FourthDevs.AgentGraph.Core;
using FourthDevs.AgentGraph.Memory;
using FourthDevs.AgentGraph.Models;
using FourthDevs.AgentGraph.Tools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.AgentGraph.Scheduler
{
    public class ActorRunResult
    {
        public string Status { get; set; } // "completed", "waiting", "blocked"
        public string Message { get; set; }
        public TokenUsage Usage { get; set; }
    }

    public static class ActorRunner
    {
        private const int DefaultMaxSteps = 8;

        public static async Task<ActorRunResult> RunActorTask(AgentTask task, Actor actor, Runtime rt, GraphQueries graph)
        {
            var actorConfig = ToolRegistry.GetActorConfig(actor);
            var tools = actorConfig.Tools.Select(t => ToolRegistry.Definitions[t]).ToList();

            if (tools.Count == 0)
                return new ActorRunResult { Status = "blocked", Message = "Actor \"" + actor.Name + "\" has no tools configured", Usage = TokenUsage.Empty() };

            var def = AgentRegistry.Get(actor.Name);
            int maxSteps = def != null && def.MaxSteps.HasValue && def.MaxSteps.Value > 0 ? def.MaxSteps.Value : DefaultMaxSteps;
            var log = Log.Scoped(actor.Name);
            var cumulative = TokenUsage.Empty();
            var promptPrefix = await ContextBuilder.BuildTaskPromptPrefix(task, actor, rt);

            string cacheKey;
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(task.SessionId + ":" + task.Id + ":" + actor.Id));
                cacheKey = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant().Substring(0, 48);
            }

            for (int step = 1; step <= maxSteps; step++)
            {
                log.Llm(step);

                try { await MemoryProcessor.ProcessTaskMemory(task.Id, rt); }
                catch (Exception ex) { Log.Warn("[memory] pre-step processing failed: " + ex.Message); }

                var input = await ContextBuilder.BuildTaskRunInput(task, rt, graph, promptPrefix);

                var response = await GenerateToolStepWithRetry(
                    actorConfig.Instructions, input, tools, actorConfig.WebSearch, cacheKey, actor.Name, step);

                if (response.Usage != null)
                {
                    cumulative = TokenUsage.Add(cumulative, response.Usage);
                    log.Usage(response.Usage.InputTokens, response.Usage.OutputTokens, response.Usage.CachedTokens);
                }

                var text = response.Text != null ? response.Text.Trim() : "";
                if (!string.IsNullOrEmpty(text))
                {
                    log.Decision(text);
                    await RuntimeHelpers.AddItem(rt, task.SessionId, "decision",
                        new JObject { ["text"] = text, ["step"] = step }, task.Id, actor.Id);
                }

                if (response.ToolCalls.Count == 0)
                {
                    if (await graph.HasUnfinishedChildren(task))
                        return new ActorRunResult
                        {
                            Status = "waiting",
                            Message = "Waiting for delegated child tasks to finish before continuing \"" + task.Title + "\"",
                            Usage = cumulative,
                        };

                    return new ActorRunResult
                    {
                        Status = !string.IsNullOrEmpty(text) ? "completed" : "blocked",
                        Message = !string.IsNullOrEmpty(text) ? text : "Actor \"" + actor.Name + "\" produced no output and no tool calls",
                        Usage = cumulative,
                    };
                }

                TerminalOutcome terminalOutcome = null;

                foreach (var call in response.ToolCalls)
                {
                    log.Tool(call.Name, call.Arguments);

                    await RuntimeHelpers.AddItem(rt, task.SessionId, "invocation",
                        new JObject { ["callId"] = call.CallId, ["tool"] = call.Name, ["input"] = call.Arguments, ["step"] = step },
                        task.Id, actor.Id);

                    ToolExecutionOutcome outcome;
                    bool toolOk = true;
                    try
                    {
                        outcome = await ToolRegistry.ExecuteToolCall(call, task, actor, rt);
                    }
                    catch (Exception ex)
                    {
                        toolOk = false;
                        var errorMsg = ex.Message;
                        outcome = new ToolExecutionOutcome
                        {
                            Status = "continue",
                            Message = errorMsg,
                            Output = JsonConvert.SerializeObject(new { error = errorMsg }),
                        };
                    }

                    log.ToolResult(call.Name, toolOk && outcome.Status != "blocked", outcome.Message);

                    await RuntimeHelpers.AddItem(rt, task.SessionId, "result",
                        new JObject { ["callId"] = call.CallId, ["tool"] = call.Name, ["output"] = outcome.Output, ["status"] = outcome.Status, ["step"] = step },
                        task.Id, actor.Id);

                    if (terminalOutcome == null && (outcome.Status == "completed" || outcome.Status == "blocked"))
                        terminalOutcome = new TerminalOutcome { Status = outcome.Status, Message = outcome.Message };
                }

                if (terminalOutcome != null)
                    return new ActorRunResult { Status = terminalOutcome.Status, Message = terminalOutcome.Message, Usage = cumulative };
            }

            if (await graph.HasUnfinishedChildren(task))
                return new ActorRunResult
                {
                    Status = "waiting",
                    Message = "Waiting for delegated child tasks to finish before continuing \"" + task.Title + "\"",
                    Usage = cumulative,
                };

            return new ActorRunResult { Status = "blocked", Message = "Actor \"" + actor.Name + "\" reached the max step limit (" + maxSteps + ")", Usage = cumulative };
        }

        private class TerminalOutcome
        {
            public string Status;
            public string Message;
        }

        private static async Task<GenerateToolStepResult> GenerateToolStepWithRetry(
            string instructions, JArray input, List<ToolDefinition> tools,
            bool webSearch, string cacheKey, string actorName, int step)
        {
            Exception lastError = null;
            for (int attempt = 1; attempt <= Recovery.MaxLlmCallAttempts; attempt++)
            {
                try
                {
                    return await AiClient.GenerateToolStep(instructions, input, tools, webSearch, cacheKey);
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    if (!Recovery.IsTransientLlmError(ex)) throw;
                    if (attempt == Recovery.MaxLlmCallAttempts)
                        throw new RecoverableActorError(
                            "Transient LLM failure for \"" + actorName + "\" on step " + step + ": " + ex.Message,
                            Recovery.ComputeRetryDelayMs(attempt));

                    int delay = Recovery.ComputeRetryDelayMs(attempt);
                    Log.Warn("[" + actorName + "] transient LLM failure on step " + step + "; retrying in " + delay + "ms (" + ex.Message + ")");
                    await Task.Delay(delay);
                }
            }
            throw lastError;
        }
    }
}
