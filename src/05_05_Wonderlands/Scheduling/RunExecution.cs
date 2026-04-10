using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using FourthDevs.Wonderlands.Agents;
using FourthDevs.Wonderlands.Ai;
using FourthDevs.Wonderlands.Core;
using FourthDevs.Wonderlands.Memory;
using FourthDevs.Wonderlands.Models;
using FourthDevs.Wonderlands.Tools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Wonderlands.Scheduling
{
    public class RunResult
    {
        public string Status { get; set; } // "completed", "waiting", "blocked", "suspended"
        public string Message { get; set; }
        public TokenUsage Usage { get; set; }
    }

    public static class RunExecution
    {
        private const int DefaultMaxSteps = 8;

        public static async Task<RunResult> DriveRun(Job job, Run run, Runtime rt, ReadinessEngine engine)
        {
            var agentConfig = ToolRegistry.GetAgentConfig(run.AgentName ?? job.AgentName);
            var tools = agentConfig.Tools
                .Where(t => ToolRegistry.Definitions.ContainsKey(t))
                .Select(t => ToolRegistry.Definitions[t])
                .ToList();

            if (tools.Count == 0)
                return new RunResult { Status = "blocked", Message = "Agent \"" + run.AgentName + "\" has no tools configured", Usage = TokenUsage.Empty() };

            var def = AgentRegistry.Get(run.AgentName ?? job.AgentName);
            int maxSteps = def != null && def.MaxSteps.HasValue && def.MaxSteps.Value > 0 ? def.MaxSteps.Value : DefaultMaxSteps;
            var log = Log.Scoped(run.AgentName ?? job.AgentName);
            var cumulative = TokenUsage.Empty();
            var promptPrefix = await ContextAssembler.BuildRunPromptPrefix(job, run, rt);

            string cacheKey;
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(job.SessionId + ":" + job.Id + ":" + run.Id));
                cacheKey = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant().Substring(0, 48);
            }

            for (int step = 1; step <= maxSteps; step++)
            {
                log.Llm(step);

                await rt.Runs.Update(run.Id, r => r.TurnCount = step);

                try { await MemoryProcessor.ProcessRunMemory(run.Id, rt); }
                catch (Exception ex) { Log.Warn("[memory] pre-step processing failed: " + ex.Message); }

                var input = await ContextAssembler.BuildRunInput(job, run, rt, engine, promptPrefix);

                var response = await GenerateToolStepWithRetry(
                    agentConfig.Instructions, input, tools, agentConfig.WebSearch, cacheKey, run.AgentName ?? job.AgentName, step);

                if (response.Usage != null)
                {
                    cumulative = TokenUsage.Add(cumulative, response.Usage);
                    log.Usage(response.Usage.InputTokens, response.Usage.OutputTokens, response.Usage.CachedTokens);
                }

                var text = response.Text != null ? response.Text.Trim() : "";
                if (!string.IsNullOrEmpty(text))
                {
                    log.Decision(text);
                    await RuntimeHelpers.AddItem(rt, job.SessionId, "decision",
                        new JObject { ["text"] = text, ["step"] = step }, job.Id, run.Id);
                }

                if (response.ToolCalls.Count == 0)
                {
                    if (await engine.HasUnfinishedChildren(job))
                        return new RunResult
                        {
                            Status = "suspended",
                            Message = "Suspended — waiting for child jobs to finish before continuing \"" + job.Title + "\"",
                            Usage = cumulative,
                        };

                    return new RunResult
                    {
                        Status = !string.IsNullOrEmpty(text) ? "completed" : "blocked",
                        Message = !string.IsNullOrEmpty(text) ? text : "Agent \"" + run.AgentName + "\" produced no output and no tool calls",
                        Usage = cumulative,
                    };
                }

                TerminalOutcome terminalOutcome = null;

                foreach (var call in response.ToolCalls)
                {
                    log.Tool(call.Name, call.Arguments);

                    await RuntimeHelpers.AddItem(rt, job.SessionId, "invocation",
                        new JObject { ["callId"] = call.CallId, ["tool"] = call.Name, ["input"] = call.Arguments, ["step"] = step },
                        job.Id, run.Id);

                    var ctx = new ToolContext { Call = call, Job = job, Run = run, Rt = rt };
                    ToolExecutionOutcome outcome;
                    bool toolOk = true;
                    try
                    {
                        outcome = await ToolRegistry.ExecuteToolCall(call, ctx);
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

                    await RuntimeHelpers.AddItem(rt, job.SessionId, "result",
                        new JObject { ["callId"] = call.CallId, ["tool"] = call.Name, ["output"] = outcome.Output, ["status"] = outcome.Status, ["step"] = step },
                        job.Id, run.Id);

                    if (terminalOutcome == null && (outcome.Status == "completed" || outcome.Status == "blocked"))
                        terminalOutcome = new TerminalOutcome { Status = outcome.Status, Message = outcome.Message };
                }

                if (terminalOutcome != null)
                    return new RunResult { Status = terminalOutcome.Status, Message = terminalOutcome.Message, Usage = cumulative };
            }

            if (await engine.HasUnfinishedChildren(job))
                return new RunResult
                {
                    Status = "suspended",
                    Message = "Suspended — waiting for child jobs to finish before continuing \"" + job.Title + "\"",
                    Usage = cumulative,
                };

            return new RunResult { Status = "blocked", Message = "Agent \"" + run.AgentName + "\" reached max step limit (" + maxSteps + ")", Usage = cumulative };
        }

        private class TerminalOutcome
        {
            public string Status;
            public string Message;
        }

        private static async Task<GenerateToolStepResult> GenerateToolStepWithRetry(
            string instructions, JArray input, List<ToolDefinition> tools,
            bool webSearch, string cacheKey, string agentName, int step)
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
                        throw new RecoverableRunError(
                            "Transient LLM failure for \"" + agentName + "\" on step " + step + ": " + ex.Message,
                            Recovery.ComputeRetryDelayMs(attempt));

                    int delay = Recovery.ComputeRetryDelayMs(attempt);
                    Log.Warn("[" + agentName + "] transient LLM failure on step " + step + "; retrying in " + delay + "ms (" + ex.Message + ")");
                    await Task.Delay(delay);
                }
            }
            throw lastError;
        }
    }
}
