using System;
using System.Collections.Generic;
using System.Configuration;
using System.Threading.Tasks;
using FourthDevs.Common;
using FourthDevs.Observability.Core;
using FourthDevs.Observability.Core.Tracing;
using FourthDevs.Observability.Models;

namespace FourthDevs.Observability.Agent
{
    /// <summary>
    /// Core agent loop: sends messages to the Chat Completions API, executes
    /// tool calls, and repeats until the model returns a final text answer.
    /// </summary>
    internal static class AgentRunner
    {
        public const string SystemPrompt =
            "You are Alice, a concise and practical assistant.\n" +
            "Use tools when they improve correctness.\n" +
            "Never invent tool outputs.";

        private const int MaxTurns = 8;

        public static async Task<AgentRunResult> RunAsync(
            ChatCompletionsClient client,
            Logger logger,
            Session session,
            string message)
        {
            session.Messages.Add(new ChatMessage { Role = "user", Content = message });

            return await Tracer.WithAgent(
                new AgentParams
                {
                    Name = "alice",
                    AgentId = string.Format("alice:{0}", session.Id),
                    Task = message
                },
                async () =>
                {
                    var usage = new Usage { Input = 0, Output = 0, Total = 0 };

                    for (int turn = 0; turn < MaxTurns; turn++)
                    {
                        int turnNum = TracingContextStore.AdvanceTurn();

                        // Build the full message list: system + session history
                        var messages = new List<ChatMessage>();
                        messages.Add(new ChatMessage { Role = "system", Content = SystemPrompt });
                        messages.AddRange(session.Messages);

                        string model = AiConfig.ResolveModel(
                            ConfigurationManager.AppSettings["OPENAI_MODEL"] ?? "gpt-4.1-mini");

                        // Start a generation span
                        var gen = Tracer.StartGeneration(new GenerationParams
                        {
                            Name = "chat",
                            Model = model,
                            Input = message
                        });

                        CompletionResult result;
                        try
                        {
                            result = await client.CompleteAsync(
                                model, messages, ToolExecutor.ToolDefinitions).ConfigureAwait(false);
                            gen.End(result.Text, result.Usage);
                        }
                        catch (Exception ex)
                        {
                            gen.Error("completion_error", ex.Message);
                            throw;
                        }

                        // Merge usage
                        usage = MergeUsage(usage, result.Usage);

                        // Build the assistant message for session history
                        var assistantMsg = new ChatMessage { Role = "assistant", Content = result.Text };

                        if (result.ToolCalls != null && result.ToolCalls.Count > 0)
                        {
                            assistantMsg.ToolCalls = new List<ChatToolCall>();
                            foreach (var tc in result.ToolCalls)
                            {
                                assistantMsg.ToolCalls.Add(new ChatToolCall
                                {
                                    Id = tc.Id,
                                    Type = "function",
                                    Function = new ChatFunctionCall
                                    {
                                        Name = tc.Name,
                                        Arguments = tc.Arguments
                                    }
                                });
                            }
                        }

                        session.Messages.Add(assistantMsg);

                        // If no tool calls, we have a final answer
                        if (result.ToolCalls == null || result.ToolCalls.Count == 0)
                        {
                            logger.Info("Agent completed", new Dictionary<string, object>
                            {
                                { "turns", turnNum },
                                { "usage", usage }
                            });

                            return new AgentRunResult
                            {
                                Response = result.Text ?? "No response",
                                Turns = turnNum,
                                Usage = usage
                            };
                        }

                        // Execute each tool call
                        foreach (var tc in result.ToolCalls)
                        {
                            logger.Debug("Calling tool", new Dictionary<string, object>
                            {
                                { "tool", tc.Name },
                                { "callId", tc.Id }
                            });

                            string toolOutput = await Tracer.WithTool(
                                new ToolParams
                                {
                                    Name = tc.Name,
                                    CallId = tc.Id,
                                    Input = tc.Arguments
                                },
                                () => ToolExecutor.ExecuteAsync(tc.Name, tc.Arguments)
                            ).ConfigureAwait(false);

                            session.Messages.Add(new ChatMessage
                            {
                                Role = "tool",
                                ToolCallId = tc.Id,
                                Content = toolOutput
                            });
                        }
                    }

                    throw new InvalidOperationException(
                        "Exceeded maximum turns before a final assistant answer");
                }).ConfigureAwait(false);
        }

        private static Usage MergeUsage(Usage current, Usage delta)
        {
            if (delta == null) return current;
            return new Usage
            {
                Input = (current.Input ?? 0) + (delta.Input ?? 0),
                Output = (current.Output ?? 0) + (delta.Output ?? 0),
                Total = (current.Total ?? 0) + (delta.Total ?? 0)
            };
        }
    }
}
