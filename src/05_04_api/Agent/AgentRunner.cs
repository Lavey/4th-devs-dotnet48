using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FourthDevs.Common;
using FourthDevs.Common.Models;
using FourthDevs.MultiAgentApi.Db;
using FourthDevs.MultiAgentApi.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.MultiAgentApi.Agent
{
    /// <summary>
    /// Executes agent runs using Common's ResponsesApiClient.
    /// Handles building interaction requests, executing AI model calls,
    /// processing tool calls, and updating run/item state.
    /// </summary>
    internal sealed class AgentRunner
    {
        private readonly DatabaseManager _db;
        private readonly MemoryManager _memory;
        private readonly ResponsesApiClient _apiClient;
        private readonly Action<DomainEvent> _emitEvent;

        internal AgentRunner(DatabaseManager db, MemoryManager memory,
            ResponsesApiClient apiClient, Action<DomainEvent> emitEvent)
        {
            _db = db;
            _memory = memory;
            _apiClient = apiClient;
            _emitEvent = emitEvent;
        }

        /// <summary>
        /// Executes a run against an agent with thread context.
        /// Returns the completed run.
        /// </summary>
        internal async Task<Run> ExecuteRunAsync(Run run, CancellationToken ct)
        {
            var agent = _db.GetAgent(run.AgentId);
            if (agent == null)
            {
                run.Status = "failed";
                run.Error = "Agent not found: " + run.AgentId;
                _db.UpdateRunStatus(run.Id, run.Status, run.Error);
                return run;
            }

            var revision = _db.GetActiveRevision(run.AgentId);

            if (revision == null)
            {
                run.Status = "failed";
                run.Error = "No active revision for agent: " + run.AgentId;
                _db.UpdateRunStatus(run.Id, run.Status, run.Error);
                return run;
            }

            run.RevisionId = revision.Id;
            _db.UpdateRunStatus(run.Id, "running", null);

            EmitRunEvent(run, "run.started");

            try
            {
                await RunLoop(run, revision, ct);
            }
            catch (OperationCanceledException)
            {
                run.Status = "cancelled";
                _db.UpdateRunStatus(run.Id, run.Status, null);
                EmitRunEvent(run, "run.cancelled");
            }
            catch (Exception ex)
            {
                run.Status = "failed";
                run.Error = ex.Message;
                _db.UpdateRunStatus(run.Id, run.Status, run.Error);
                EmitRunEvent(run, "run.failed");
            }

            return run;
        }

        private async Task RunLoop(Run run, AgentRevision revision, CancellationToken ct)
        {
            int maxTurns = run.MaxTurns > 0 ? run.MaxTurns : 10;

            for (int turn = 0; turn < maxTurns; turn++)
            {
                ct.ThrowIfCancellationRequested();

                _db.IncrementRunTurnCount(run.Id);
                run.TurnCount = turn + 1;

                // Build input from thread messages and memory
                var input = BuildInput(run, revision);

                string resolvedModel = AiConfig.ResolveModel(revision.Model ?? "gpt-4.1-mini");

                var request = new ResponsesRequest
                {
                    Model = resolvedModel,
                    Input = input
                };

                // Execute AI call
                ResponsesResponse response;
                try
                {
                    response = await _apiClient.SendAsync(request);
                }
                catch (Exception ex)
                {
                    run.Status = "failed";
                    run.Error = "AI API error: " + ex.Message;
                    _db.UpdateRunStatus(run.Id, run.Status, run.Error);
                    EmitRunEvent(run, "run.failed");
                    return;
                }

                // Process response output
                bool hasToolCalls = false;
                var toolCalls = ResponsesApiClient.GetToolCalls(response);

                if (toolCalls.Count > 0)
                {
                    hasToolCalls = true;
                    foreach (var toolCall in toolCalls)
                    {
                        // Record tool call item
                        int seq = GetNextSequence(run.Id);
                        var toolItem = new Item
                        {
                            Id = IdGenerator.NewItemId(),
                            RunId = run.Id,
                            ThreadId = run.ThreadId,
                            Sequence = seq,
                            Type = "function_call",
                            CallId = toolCall.CallId,
                            ToolName = toolCall.Name,
                            Arguments = toolCall.Arguments,
                            CreatedAt = DatabaseManager.UtcNow()
                        };
                        _db.InsertItem(toolItem);

                        // Execute tool (stub — returns a placeholder)
                        string toolOutput = ExecuteTool(toolCall.Name, toolCall.Arguments, run);

                        // Record tool result item
                        int resultSeq = GetNextSequence(run.Id);
                        var resultItem = new Item
                        {
                            Id = IdGenerator.NewItemId(),
                            RunId = run.Id,
                            ThreadId = run.ThreadId,
                            Sequence = resultSeq,
                            Type = "function_call_output",
                            CallId = toolCall.CallId,
                            ToolName = toolCall.Name,
                            Output = toolOutput,
                            CreatedAt = DatabaseManager.UtcNow()
                        };
                        _db.InsertItem(resultItem);
                    }
                }

                // Extract text response
                string text = ResponsesApiClient.ExtractText(response);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    int seq = GetNextSequence(run.Id);
                    var msgItem = new Item
                    {
                        Id = IdGenerator.NewItemId(),
                        RunId = run.Id,
                        ThreadId = run.ThreadId,
                        Sequence = seq,
                        Type = "message",
                        Role = "assistant",
                        Content = text,
                        CreatedAt = DatabaseManager.UtcNow()
                    };
                    _db.InsertItem(msgItem);

                    // Also persist as thread message
                    if (!string.IsNullOrWhiteSpace(run.ThreadId))
                    {
                        var threadMsg = new SessionMessage
                        {
                            Id = IdGenerator.NewMessageId(),
                            ThreadId = run.ThreadId,
                            Role = "assistant",
                            Content = text,
                            CreatedBy = run.AgentId,
                            CreatedAt = DatabaseManager.UtcNow()
                        };
                        _db.InsertMessage(threadMsg);
                    }

                    EmitRunEvent(run, "run.message");
                }

                // If no tool calls, the run is complete
                if (!hasToolCalls)
                {
                    run.Status = "completed";
                    _db.UpdateRunStatus(run.Id, run.Status, null);
                    EmitRunEvent(run, "run.completed");
                    return;
                }
            }

            // Max turns reached
            run.Status = "completed";
            run.Error = "Max turns reached";
            _db.UpdateRunStatus(run.Id, run.Status, run.Error);
            EmitRunEvent(run, "run.completed");
        }

        private List<InputMessage> BuildInput(Run run, AgentRevision revision)
        {
            var input = new List<InputMessage>();

            // System instruction
            string systemPrompt = revision.Instructions ?? "You are a helpful AI assistant.";

            // Add memory context if memory policy is not "none"
            if (revision.MemoryPolicy != "none" &&
                !string.IsNullOrWhiteSpace(run.AgentId))
            {
                string memoryContext = _memory.BuildMemoryContext(run.AgentId, run.ThreadId);
                if (!string.IsNullOrWhiteSpace(memoryContext))
                {
                    systemPrompt = systemPrompt + "\n\n" + memoryContext;
                }
            }

            input.Add(new InputMessage
            {
                Role = "system",
                Content = systemPrompt
            });

            // Add thread messages as context
            if (!string.IsNullOrWhiteSpace(run.ThreadId))
            {
                var messages = _db.ListMessagesByThread(run.ThreadId);
                foreach (var msg in messages)
                {
                    input.Add(new InputMessage
                    {
                        Role = msg.Role,
                        Content = msg.Content
                    });
                }
            }

            // Add run items as conversation history
            var items = _db.ListItemsByRun(run.Id);
            foreach (var item in items)
            {
                if (item.Type == "message")
                {
                    input.Add(new InputMessage
                    {
                        Role = item.Role ?? "assistant",
                        Content = item.Content
                    });
                }
            }

            return input;
        }

        private string ExecuteTool(string toolName, string arguments, Run run)
        {
            // Record tool execution
            string texId = IdGenerator.NewToolExecutionId();
            var tex = new ToolExecution
            {
                Id = texId,
                RunId = run.Id,
                ToolName = toolName,
                Arguments = arguments,
                Status = "completed",
                CreatedAt = DatabaseManager.UtcNow(),
                CompletedAt = DatabaseManager.UtcNow()
            };

            string output;
            try
            {
                // Built-in tools
                if (toolName == "remember" || toolName == "add_memory")
                {
                    output = HandleMemoryTool(arguments, run);
                }
                else
                {
                    output = string.Format("Tool '{0}' is not available in this environment.", toolName);
                }
            }
            catch (Exception ex)
            {
                output = "Error executing tool: " + ex.Message;
                tex.Status = "failed";
            }

            tex.Output = output;
            return output;
        }

        private string HandleMemoryTool(string arguments, Run run)
        {
            try
            {
                var args = JObject.Parse(arguments);
                string content = args["content"]?.ToString() ?? args["observation"]?.ToString() ?? "";
                string keywords = args["keywords"]?.ToString();
                string scope = args["scope"]?.ToString() ?? "thread_shared";

                if (string.IsNullOrWhiteSpace(content))
                    return "No content provided for memory.";

                _memory.CreateObservation(run.TenantId, run.AgentId, run.ThreadId,
                    run.SessionId, run.Id, scope, content, keywords);

                return "Memory recorded successfully.";
            }
            catch (Exception ex)
            {
                return "Failed to record memory: " + ex.Message;
            }
        }

        private int GetNextSequence(string runId)
        {
            var items = _db.ListItemsByRun(runId);
            return items.Count;
        }

        private void EmitRunEvent(Run run, string eventType)
        {
            if (_emitEvent == null) return;

            var evt = new DomainEvent
            {
                Id = IdGenerator.NewEventId(),
                TenantId = run.TenantId,
                AggregateType = "run",
                AggregateId = run.Id,
                EventType = eventType,
                Payload = JsonConvert.SerializeObject(new
                {
                    run_id = run.Id,
                    thread_id = run.ThreadId,
                    agent_id = run.AgentId,
                    status = run.Status,
                    turn_count = run.TurnCount
                }),
                CreatedAt = DatabaseManager.UtcNow()
            };

            try
            {
                _db.InsertDomainEvent(evt);
                _emitEvent(evt);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[AgentRunner] Failed to emit event: " + ex.Message);
            }
        }
    }
}
