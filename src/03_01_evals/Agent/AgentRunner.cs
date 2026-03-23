using System;
using System.Collections.Generic;
using System.Configuration;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using FourthDevs.Common;
using FourthDevs.Common.Models;
using FourthDevs.Evals.Core;
using FourthDevs.Evals.Core.Tracing;
using FourthDevs.Evals.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Evals.Agent
{
    /// <summary>
    /// Core agent loop: sends messages to the Responses API, executes
    /// tool calls, and repeats until the model returns a final text answer.
    /// Uses raw JSON for the input array to support the heterogeneous
    /// message types (InputMessage, OutputItem, ToolCallInput) that the
    /// Responses API accepts.
    /// </summary>
    internal static class AgentRunner
    {
        public const string SystemPrompt =
            "You are Alice, a concise and practical assistant.\n" +
            "Use tools when they improve correctness.\n" +
            "Never invent tool outputs.";

        private const int MaxTurns = 8;

        public static async Task<AgentRunResult> RunAsync(
            Logger logger,
            Session session,
            string message)
        {
            // Add user message to session
            session.Messages.Add(new { type = "message", role = "user", content = message });

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
                    var toolsUsed = new List<string>();
                    int toolCallCount = 0;

                    for (int turn = 0; turn < MaxTurns; turn++)
                    {
                        int turnNum = TracingContextStore.AdvanceTurn();

                        string model = AiConfig.ResolveModel(
                            ConfigurationManager.AppSettings["OPENAI_MODEL"] ?? "gpt-4.1-mini");

                        // Start a generation span
                        var gen = Tracer.StartGeneration(new GenerationParams
                        {
                            Name = "chat",
                            Model = model,
                            Input = message
                        });

                        ResponsesResponse response;
                        try
                        {
                            response = await SendRawRequestAsync(model, session.Messages)
                                .ConfigureAwait(false);
                            gen.End(ResponsesApiClient.ExtractText(response));
                        }
                        catch (Exception ex)
                        {
                            gen.Error("completion_error", ex.Message);
                            throw;
                        }

                        // Merge usage
                        if (response.Usage != null)
                        {
                            usage.Input = (usage.Input ?? 0) + response.Usage.InputTokens;
                            usage.Output = (usage.Output ?? 0) + response.Usage.OutputTokens;
                            usage.Total = (usage.Total ?? 0)
                                + response.Usage.InputTokens + response.Usage.OutputTokens;
                        }

                        // Extract text and tool calls
                        string text = ResponsesApiClient.ExtractText(response);
                        var toolCalls = ResponsesApiClient.GetToolCalls(response);

                        // Add all output items to session messages
                        foreach (var outputItem in response.Output)
                        {
                            if (outputItem.Type == "function_call")
                            {
                                session.Messages.Add(new
                                {
                                    type = "function_call",
                                    call_id = outputItem.CallId,
                                    name = outputItem.Name,
                                    arguments = outputItem.Arguments
                                });
                            }
                            else if (outputItem.Type == "message")
                            {
                                session.Messages.Add(new
                                {
                                    type = "message",
                                    role = outputItem.Role,
                                    content = outputItem.Content
                                });
                            }
                        }

                        if (toolCalls.Count == 0)
                        {
                            logger.Info("Agent completed", new Dictionary<string, object>
                            {
                                { "turns", turnNum },
                                { "usage", usage }
                            });

                            return new AgentRunResult
                            {
                                Response = text ?? "No response",
                                Turns = turnNum,
                                Usage = usage,
                                ToolsUsed = toolsUsed,
                                ToolCallCount = toolCallCount
                            };
                        }

                        // Execute each tool call
                        foreach (var tc in toolCalls)
                        {
                            if (!toolsUsed.Contains(tc.Name))
                                toolsUsed.Add(tc.Name);
                            toolCallCount++;

                            logger.Debug("Calling tool", new Dictionary<string, object>
                            {
                                { "tool", tc.Name },
                                { "callId", tc.CallId }
                            });

                            string toolOutput = await Tracer.WithTool(
                                new ToolParams
                                {
                                    Name = tc.Name,
                                    CallId = tc.CallId,
                                    Input = tc.Arguments
                                },
                                () => ToolExecutor.ExecuteAsync(tc.Name, tc.Arguments)
                            ).ConfigureAwait(false);

                            // Add function_call_output to session
                            session.Messages.Add(new
                            {
                                type = "function_call_output",
                                call_id = tc.CallId,
                                output = toolOutput
                            });
                        }
                    }

                    throw new InvalidOperationException(
                        "Exceeded maximum turns before a final assistant answer");
                }).ConfigureAwait(false);
        }

        /// <summary>
        /// Sends a raw JSON request to the Responses API with heterogeneous input.
        /// </summary>
        private static async Task<ResponsesResponse> SendRawRequestAsync(
            string model, List<object> inputMessages)
        {
            var body = new JObject
            {
                ["model"] = model,
                ["instructions"] = SystemPrompt,
                ["input"] = JArray.FromObject(inputMessages),
                ["tools"] = JArray.FromObject(ToolExecutor.ToolDefinitions),
                ["store"] = false
            };

            string json = body.ToString(Formatting.None);

            using (var http = new HttpClient())
            {
                http.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", AiConfig.ApiKey);

                if (AiConfig.Provider == "openrouter")
                {
                    if (!string.IsNullOrWhiteSpace(AiConfig.HttpReferer))
                        http.DefaultRequestHeaders.TryAddWithoutValidation(
                            "HTTP-Referer", AiConfig.HttpReferer);
                    if (!string.IsNullOrWhiteSpace(AiConfig.AppName))
                        http.DefaultRequestHeaders.TryAddWithoutValidation(
                            "X-Title", AiConfig.AppName);
                }

                using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
                using (var resp = await http.PostAsync(AiConfig.ApiEndpoint, content)
                    .ConfigureAwait(false))
                {
                    string responseBody = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var parsed = JsonConvert.DeserializeObject<ResponsesResponse>(responseBody);

                    if (!resp.IsSuccessStatusCode || parsed?.Error != null)
                    {
                        string msg = parsed?.Error?.Message
                            ?? string.Format("Request failed with status {0}", (int)resp.StatusCode);
                        throw new InvalidOperationException(msg);
                    }

                    return parsed;
                }
            }
        }
    }
}
