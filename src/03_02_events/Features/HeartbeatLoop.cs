using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using FourthDevs.Events.Config;
using FourthDevs.Events.Core;
using FourthDevs.Events.Mcp;
using FourthDevs.Events.Models;
using FourthDevs.Events.Workflows;

namespace FourthDevs.Events.Features
{
    /// <summary>
    /// Main heartbeat loop: claims tasks, dispatches to agents, detects completion.
    /// </summary>
    internal static class HeartbeatLoop
    {
        public static async Task RunAsync(
            WorkflowDefinition workflow,
            int rounds,
            int delayMs,
            bool autoHuman,
            McpManager mcp,
            CancellationToken ct)
        {
            string eventsDir = Path.Combine(EnvConfig.ProjectPath, "system", "events");
            Directory.CreateDirectory(eventsDir);
            var events = new EventStore(eventsDir);

            string heartbeatId = "hb-" + Guid.NewGuid().ToString("N").Substring(0, 8);

            var capMap = Autonomy.CapabilityMap.Build(workflow.AgentOrder);

            for (int round = 1; round <= rounds; round++)
            {
                if (ct.IsCancellationRequested) break;

                string runId = heartbeatId + ":r" + round;
                int claimed = 0;
                int completed = 0;
                int blocked = 0;
                int waitingHuman = 0;

                await events.EmitAsync(new HeartbeatEvent
                {
                    Type = "heartbeat.started",
                    Round = round,
                    Message = "Heartbeat round " + round + " started.",
                    Data = new JObject { ["workflow_id"] = workflow.Id, ["heartbeatId"] = heartbeatId }
                });

                // Reconcile dependency states
                var depChanges = TaskManager.ReconcileDependencyStates();
                foreach (var change in depChanges)
                {
                    string evtType = change.Became == "blocked" ? "task.blocked" : "task.unblocked";
                    await events.EmitAsync(new HeartbeatEvent
                    {
                        Type = evtType,
                        Round = round,
                        Agent = change.Task.Frontmatter.Agent,
                        TaskId = change.Task.Frontmatter.Id,
                        Message = change.Became == "blocked"
                            ? "Blocked by dependencies: " + string.Join(", ", change.PendingDeps)
                            : "Dependencies resolved; task reopened."
                    });
                }

                // Resolve waiting-human tasks
                var waitingTasks = TaskManager.ListWaitingHuman();
                foreach (var wt in waitingTasks)
                {
                    string question = wt.Frontmatter.WaitQuestion ?? "Please provide a decision.";
                    string answer = autoHuman
                        ? ChooseAutoAnswer(question)
                        : PromptHuman(question);

                    TaskManager.ReopenTaskWithAnswer(wt, answer);
                    await events.EmitAsync(new HeartbeatEvent
                    {
                        Type = "human.input-provided",
                        Round = round,
                        Agent = wt.Frontmatter.Agent,
                        TaskId = wt.Frontmatter.Id,
                        Message = Truncate(answer, 180)
                    });
                }

                // For each agent, claim and run a task
                foreach (string agent in workflow.AgentOrder)
                {
                    if (ct.IsCancellationRequested) break;

                    List<string> caps;
                    if (!capMap.TryGetValue(agent, out caps))
                        caps = new List<string>();

                    var task = TaskManager.ClaimNextTask(agent, runId, caps);
                    if (task == null) continue;
                    claimed++;

                    await events.EmitAsync(new HeartbeatEvent
                    {
                        Type = "task.claimed",
                        Round = round,
                        Agent = agent,
                        TaskId = task.Frontmatter.Id,
                        Message = task.Frontmatter.Title ?? task.Frontmatter.Id
                    });

                    string taskPrompt = BuildTaskPrompt(task, round);
                    long startMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                    AgentRunResult result;
                    try
                    {
                        result = await AgentRunner.RunAgent(agent, taskPrompt, EnvConfig.ProjectPath, mcp, ct);
                    }
                    catch (Exception ex)
                    {
                        long execMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - startMs;
                        TaskManager.MarkTaskBlocked(task, ex.Message);
                        blocked++;
                        await events.EmitAsync(new HeartbeatEvent
                        {
                            Type = "task.blocked",
                            Round = round,
                            Agent = agent,
                            TaskId = task.Frontmatter.Id,
                            Message = Truncate(ex.Message, 180),
                            Data = new JObject { ["exec_ms"] = execMs }
                        });
                        continue;
                    }

                    long elapsed = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - startMs;

                    if (result.Status == "waiting-human")
                    {
                        string waitId = result.WaitId ?? "wait-" + Guid.NewGuid().ToString("N").Substring(0, 8);
                        TaskManager.MarkTaskWaitingHuman(task, waitId, result.WaitQuestion ?? "Agent requested human input.");
                        waitingHuman++;
                        await events.EmitAsync(new HeartbeatEvent
                        {
                            Type = "task.waiting-human",
                            Round = round,
                            Agent = agent,
                            TaskId = task.Frontmatter.Id,
                            Message = Truncate(result.WaitQuestion ?? "Human input needed", 180),
                            Data = new JObject { ["waitId"] = waitId, ["exec_ms"] = elapsed }
                        });
                    }
                    else if (result.Status == "failed")
                    {
                        TaskManager.MarkTaskBlocked(task, result.Error ?? "Agent failed");
                        blocked++;
                        await events.EmitAsync(new HeartbeatEvent
                        {
                            Type = "task.blocked",
                            Round = round,
                            Agent = agent,
                            TaskId = task.Frontmatter.Id,
                            Message = Truncate(result.Error ?? "Agent failed", 180),
                            Data = new JObject { ["exec_ms"] = elapsed, ["turns"] = result.Usage.Turns }
                        });
                    }
                    else
                    {
                        TaskManager.MarkTaskCompleted(task, Truncate(result.Response ?? "", 500));
                        completed++;
                        await events.EmitAsync(new HeartbeatEvent
                        {
                            Type = "task.completed",
                            Round = round,
                            Agent = agent,
                            TaskId = task.Frontmatter.Id,
                            Message = Truncate(result.Response ?? "Task completed.", 180),
                            Data = new JObject
                            {
                                ["exec_ms"] = elapsed,
                                ["turns"] = result.Usage.Turns,
                                ["actual_tokens"] = result.Usage.TotalActualTokens
                            }
                        });
                    }
                }

                if (claimed == 0)
                {
                    var counts = TaskManager.CountByStatus();
                    await events.EmitAsync(new HeartbeatEvent
                    {
                        Type = "heartbeat.idle",
                        Round = round,
                        Message = "No claimable tasks this round.",
                        Data = JObject.FromObject(counts)
                    });
                }

                // Check completion
                if (TaskManager.AllTasksCompleted())
                {
                    await events.EmitAsync(new HeartbeatEvent
                    {
                        Type = "project.completed",
                        Round = round,
                        Message = "All tasks completed.",
                        Data = new JObject { ["workflow_id"] = workflow.Id }
                    });
                    Logger.Info("heartbeat", "All tasks completed. Stopping.");
                    break;
                }

                await events.EmitAsync(new HeartbeatEvent
                {
                    Type = "heartbeat.finished",
                    Round = round,
                    Message = "Heartbeat round " + round + " finished.",
                    Data = new JObject
                    {
                        ["claimed"] = claimed,
                        ["completed_runs"] = completed,
                        ["blocked_runs"] = blocked,
                        ["waiting_human_runs"] = waitingHuman
                    }
                });

                events.FlushRound(round);

                // Delay between rounds
                if (round < rounds && delayMs > 0 && !ct.IsCancellationRequested)
                {
                    try { await Task.Delay(delayMs, ct); }
                    catch (OperationCanceledException) { break; }
                }
            }
        }

        private static string BuildTaskPrompt(TaskRecord task, int round)
        {
            return string.Join("\n", new[]
            {
                "You are executing heartbeat round " + round + ".",
                "Task ID: " + task.Frontmatter.Id,
                "",
                "Task title: " + task.Frontmatter.Title,
                "",
                "Task body:",
                string.IsNullOrEmpty(task.Body) ? "[empty]" : task.Body,
                "",
                "Execution rules:",
                "- Work only on this task.",
                "- If frontmatter has output_file, write the deliverable there.",
                "- Finish with a concise completion note."
            });
        }

        private static string ChooseAutoAnswer(string question)
        {
            string lower = question.ToLowerInvariant();
            if (lower.Contains("tone") || lower.Contains("style"))
                return "Use a clear, executive-friendly tone with concrete implementation details.";
            if (lower.Contains("yes") && lower.Contains("no"))
                return "Yes, proceed with the lower-risk and reversible option.";
            return "Proceed with the most evidence-backed and reversible option.";
        }

        private static string PromptHuman(string question)
        {
            Console.WriteLine();
            Console.WriteLine("[human decision needed]");
            Console.WriteLine(question);
            Console.Write("> ");
            string answer = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(answer))
                return ChooseAutoAnswer(question);
            return answer.Trim();
        }

        private static string Truncate(string text, int max)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text.Length > max ? text.Substring(0, max) + "..." : text;
        }
    }
}
