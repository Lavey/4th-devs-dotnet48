using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FourthDevs.ChatUi.Models;
using FourthDevs.ChatUi.Tools;
using FourthDevs.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.ChatUi.Agent
{
    /// <summary>
    /// Runs a streaming agent turn against the OpenAI Responses API.
    /// Because .NET 4.8 does not support IAsyncEnumerable, this class
    /// accepts an <see cref="Action{BaseStreamEvent}"/> callback that is
    /// invoked for each event as it arrives.
    /// </summary>
    internal sealed class LiveTurnRunner
    {
        private const string Model = "gpt-4.1";
        private const int MaxSteps = 6;
        private const int MaxOutputTokens = 4000;

        private readonly string _dataDir;
        private readonly ToolRegistry _tools;

        public LiveTurnRunner(string dataDir)
        {
            _dataDir = dataDir;
            _tools = new ToolRegistry(dataDir);
        }

        /// <summary>
        /// Streams a full agent turn, calling <paramref name="onEvent"/>
        /// for every SSE event that should be sent to the client.
        /// </summary>
        public async Task RunAsync(
            List<ConversationMessage> history,
            string userPrompt,
            string messageId,
            Action<BaseStreamEvent> onEvent,
            CancellationToken ct)
        {
            var factory = new EventFactory(messageId);

            // Emit start
            onEvent(factory.Create<AssistantMessageStartEvent>());

            var input = InputBuilder.Build(history);
            input.Add(new JObject
            {
                ["role"] = "user",
                ["content"] = userPrompt
            });

            var pendingToolCalls = new List<PendingToolCall>();

            for (int step = 0; step < MaxSteps && !ct.IsCancellationRequested; step++)
            {
                pendingToolCalls.Clear();

                var body = BuildRequestBody(input);

                using (var http = CreateHttpClient())
                using (var request = new HttpRequestMessage(HttpMethod.Post, AiConfig.ApiEndpoint))
                {
                    request.Content = new StringContent(
                        body.ToString(Formatting.None),
                        Encoding.UTF8, "application/json");

                    using (var response = await http.SendAsync(
                        request, HttpCompletionOption.ResponseHeadersRead, ct))
                    {
                        response.EnsureSuccessStatusCode();

                        using (var stream = await response.Content.ReadAsStreamAsync())
                        using (var reader = new StreamReader(stream, Encoding.UTF8))
                        {
                            string line;
                            var argsBuffers = new Dictionary<string, StringBuilder>();
                            bool inThinking = false;

                            while ((line = await ReadLineAsync(reader, ct)) != null)
                            {
                                if (ct.IsCancellationRequested) break;

                                if (!line.StartsWith("data: ")) continue;
                                string data = line.Substring(6).Trim();
                                if (data == "[DONE]") break;

                                JObject evt;
                                try { evt = JObject.Parse(data); }
                                catch { continue; }

                                string type = evt["type"]?.ToString() ?? "";

                                switch (type)
                                {
                                    case "response.output_text.delta":
                                    {
                                        string delta = evt["delta"]?.ToString() ?? "";
                                        if (!string.IsNullOrEmpty(delta))
                                        {
                                            var e = factory.Create<TextDeltaEvent>();
                                            e.TextDelta = delta;
                                            onEvent(e);
                                        }
                                        break;
                                    }
                                    case "response.reasoning_summary_text.delta":
                                    {
                                        if (!inThinking)
                                        {
                                            inThinking = true;
                                            var ts = factory.Create<ThinkingStartEvent>();
                                            ts.Label = "Reasoning";
                                            onEvent(ts);
                                        }
                                        string delta = evt["delta"]?.ToString() ?? "";
                                        if (!string.IsNullOrEmpty(delta))
                                        {
                                            var td = factory.Create<ThinkingDeltaEvent>();
                                            td.TextDelta = delta;
                                            onEvent(td);
                                        }
                                        break;
                                    }
                                    case "response.reasoning_summary_text.done":
                                    {
                                        if (inThinking)
                                        {
                                            inThinking = false;
                                            onEvent(factory.Create<ThinkingEndEvent>());
                                        }
                                        break;
                                    }
                                    case "response.output_item.added":
                                    {
                                        var item = evt["item"];
                                        if (item != null && item["type"]?.ToString() == "function_call")
                                        {
                                            string callId = item["call_id"]?.ToString() ?? "";
                                            string name = item["name"]?.ToString() ?? "";
                                            if (!string.IsNullOrEmpty(callId))
                                            {
                                                argsBuffers[callId] = new StringBuilder();
                                                pendingToolCalls.Add(new PendingToolCall
                                                {
                                                    CallId = callId,
                                                    Name = name
                                                });
                                            }
                                        }
                                        break;
                                    }
                                    case "response.function_call_arguments.delta":
                                    {
                                        string callId = evt["call_id"]?.ToString() ??
                                                        evt["item_id"]?.ToString() ?? "";
                                        string delta = evt["delta"]?.ToString() ?? "";
                                        if (!string.IsNullOrEmpty(callId) &&
                                            argsBuffers.ContainsKey(callId))
                                        {
                                            argsBuffers[callId].Append(delta);
                                        }
                                        break;
                                    }
                                    case "response.function_call_arguments.done":
                                    {
                                        string callId = evt["call_id"]?.ToString() ??
                                                        evt["item_id"]?.ToString() ?? "";
                                        if (argsBuffers.ContainsKey(callId))
                                        {
                                            string argsStr = argsBuffers[callId].ToString();
                                            foreach (var pc in pendingToolCalls)
                                            {
                                                if (pc.CallId == callId)
                                                {
                                                    try { pc.Args = JObject.Parse(argsStr); }
                                                    catch { pc.Args = new JObject(); }
                                                    break;
                                                }
                                            }
                                        }
                                        break;
                                    }
                                }
                            }

                            // Close any open thinking block
                            if (inThinking)
                            {
                                onEvent(factory.Create<ThinkingEndEvent>());
                            }
                        }
                    }
                }

                // If no tool calls, we're done
                if (pendingToolCalls.Count == 0) break;

                // Execute tool calls and feed results back
                foreach (var tc in pendingToolCalls)
                {
                    var tcEvent = factory.Create<ToolCallEvent>();
                    tcEvent.ToolCallId = tc.CallId;
                    tcEvent.Name = tc.Name;
                    tcEvent.Args = tc.Args ?? new JObject();
                    onEvent(tcEvent);

                    var result = _tools.Execute(tc.Name, tc.Args ?? new JObject());

                    var trEvent = factory.Create<ToolResultEvent>();
                    trEvent.ToolCallId = tc.CallId;
                    trEvent.Ok = result.Ok;
                    trEvent.Output = result.Output;
                    onEvent(trEvent);

                    // Check if tool produced an artifact
                    if (result.Artifact != null)
                    {
                        onEvent(result.Artifact);
                    }

                    // Add to input for next step
                    input.Add(new JObject
                    {
                        ["type"] = "function_call",
                        ["call_id"] = tc.CallId,
                        ["name"] = tc.Name,
                        ["arguments"] = (tc.Args ?? new JObject()).ToString(Formatting.None)
                    });
                    input.Add(new JObject
                    {
                        ["type"] = "function_call_output",
                        ["call_id"] = tc.CallId,
                        ["output"] = result.Output != null
                            ? result.Output.ToString(Formatting.None)
                            : ""
                    });
                }
            }

            // Complete
            var complete = factory.Create<CompleteEvent>();
            complete.FinishReason = "stop";
            onEvent(complete);
        }

        private JObject BuildRequestBody(JArray input)
        {
            string resolvedModel = AiConfig.ResolveModel(Model);

            var body = new JObject
            {
                ["model"] = resolvedModel,
                ["instructions"] = SystemPrompt.Text,
                ["input"] = input,
                ["max_output_tokens"] = MaxOutputTokens,
                ["parallel_tool_calls"] = true,
                ["store"] = false,
                ["stream"] = true,
                ["tools"] = _tools.GetDefinitionsArray()
            };

            // Add reasoning for capable models
            if (Regex.IsMatch(resolvedModel, @"^(o\d|gpt-5)", RegexOptions.IgnoreCase))
            {
                body["reasoning"] = new JObject
                {
                    ["effort"] = "medium",
                    ["summary"] = "auto"
                };
            }

            return body;
        }

        private static HttpClient CreateHttpClient()
        {
            var http = new HttpClient();
            http.Timeout = TimeSpan.FromMinutes(5);
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

            return http;
        }

        private static async Task<string> ReadLineAsync(StreamReader reader, CancellationToken ct)
        {
            try
            {
                return await reader.ReadLineAsync();
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (IOException)
            {
                return null;
            }
        }

        private class PendingToolCall
        {
            public string CallId;
            public string Name;
            public JObject Args;
        }
    }
}
