using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using FourthDevs.Calendar.Data;
using FourthDevs.Calendar.Models;
using FourthDevs.Calendar.Prompts;
using FourthDevs.Calendar.Tools;
using FourthDevs.Common;
using FourthDevs.Common.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Calendar.Agent
{
    public class RunStepResult
    {
        public string Id { get; set; }
        public int Turns { get; set; }
        public int ToolCalls { get; set; }
        public string Response { get; set; }
    }

    public class AgentResult
    {
        public string Model { get; set; }
        public List<RunStepResult> AddPhase { get; set; }
        public List<RunStepResult> NotificationPhase { get; set; }
        public int EventsCreated { get; set; }
        public int NotificationsSent { get; set; }
    }

    public static class AgentRunner
    {
        private const int MaxTurns = 12;

        public static async Task<AgentResult> RunAsync(string model)
        {
            string resolvedModel = AiConfig.ResolveModel(model);

            // Assemble tool sets
            var addPhaseTools = new List<LocalToolDefinition>();
            addPhaseTools.AddRange(ContactTools.GetTools());
            addPhaseTools.AddRange(PlaceTools.GetTools());
            addPhaseTools.AddRange(WebSearchTools.GetTools());
            addPhaseTools.AddRange(CalendarTools.GetTools());

            var notificationPhaseTools = new List<LocalToolDefinition>();
            notificationPhaseTools.AddRange(CalendarTools.GetTools());
            notificationPhaseTools.AddRange(MapTools.GetTools());
            notificationPhaseTools.AddRange(NotificationTools.GetTools());

            int totalToolCount = addPhaseTools.Count + MapTools.GetTools().Count + NotificationTools.GetTools().Count;

            // Banner
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine();
            Console.WriteLine("  ╔════════════════════════════════════════════════════════╗");
            Console.WriteLine("  ║               📅  Calendar Agent                      ║");
            Console.WriteLine("  ╚════════════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine(string.Format("  Model: {0}", model));
            Console.WriteLine(string.Format("  Tools: {0} available", totalToolCount));
            Console.WriteLine();

            var addPhaseResults = new List<RunStepResult>();
            var notificationPhaseResults = new List<RunStepResult>();

            // Phase 1: Add Events
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(string.Format("\n  ╔══ Phase 1: Add Events — {0} user requests ══", ScenariosData.AddScenario.Count));
            Console.ResetColor();

            foreach (var step in ScenariosData.AddScenario)
            {
                EnvironmentStore.SetTime(step.At);
                EnvironmentStore.SetUserLocation(step.LocationId);

                string meta = EnvironmentStore.BuildMetadata();

                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine(string.Format("\n  ┌─ {0} ─────────────────────────────────", step.Id));
                Console.ResetColor();
                Console.WriteLine(string.Format("  │ {0}", step.Message));
                Console.WriteLine("  │");

                foreach (string line in meta.Split('\n'))
                    Console.WriteLine(string.Format("  │   {0}", line));

                var result = await RunToolLoop(
                    resolvedModel,
                    AgentPrompts.BuildAddPhasePrompt(),
                    meta + "\n\n" + step.Message,
                    addPhaseTools);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(string.Format("  │ ✔ {0}", Truncate(result.Text, 80)));
                Console.WriteLine(string.Format("  │ {0} turns, {1} tool calls", result.Turns, result.ToolCalls));
                Console.ResetColor();

                addPhaseResults.Add(new RunStepResult
                {
                    Id = step.Id,
                    Turns = result.Turns,
                    ToolCalls = result.ToolCalls,
                    Response = result.Text,
                });
            }

            // Phase 2: Notifications
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(string.Format("\n  ╔══ Phase 2: Notifications — {0} webhooks ══", ScenariosData.NotificationWebhooks.Count));
            Console.ResetColor();

            foreach (var webhook in ScenariosData.NotificationWebhooks)
            {
                EnvironmentStore.SetTime(webhook.At);
                EnvironmentStore.SetUserLocation(webhook.LocationId);

                string meta = EnvironmentStore.BuildMetadata();
                string payloadText = JsonConvert.SerializeObject(webhook.Payload, Formatting.Indented);
                string label = string.Format("{0} (starts {1})",
                    webhook.Payload.EventTitle,
                    webhook.Payload.StartsAt.Length > 15 ? webhook.Payload.StartsAt.Substring(11, 5) : webhook.Payload.StartsAt);

                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine(string.Format("\n  ┌─ {0} ─────────────────────────────────", webhook.Id));
                Console.ResetColor();
                Console.WriteLine(string.Format("  │ {0}", label));

                string message = meta + "\n\nWebhook payload:\n" + payloadText +
                    "\n\nUse tools, send exactly one notification, then summarize what you sent.";

                var result = await RunToolLoop(
                    resolvedModel,
                    AgentPrompts.BuildNotificationPhasePrompt(),
                    message,
                    notificationPhaseTools);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(string.Format("  │ ✔ {0}", Truncate(result.Text, 80)));
                Console.WriteLine(string.Format("  │ {0} turns, {1} tool calls", result.Turns, result.ToolCalls));
                Console.ResetColor();

                notificationPhaseResults.Add(new RunStepResult
                {
                    Id = webhook.Id,
                    Turns = result.Turns,
                    ToolCalls = result.ToolCalls,
                    Response = result.Text,
                });
            }

            // Final tables
            PrintEventTable(CalendarStore.GetEvents());
            PrintNotificationTable(NotificationStore.ListNotifications());

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\n  Done.");
            Console.ResetColor();

            return new AgentResult
            {
                Model = model,
                AddPhase = addPhaseResults,
                NotificationPhase = notificationPhaseResults,
                EventsCreated = CalendarStore.GetEvents().Count,
                NotificationsSent = NotificationStore.ListNotifications().Count,
            };
        }

        private static async Task<(string Text, int Turns, int ToolCalls)> RunToolLoop(
            string model,
            string instructions,
            string message,
            List<LocalToolDefinition> tools)
        {
            var input = new JArray
            {
                new JObject { ["role"] = "user", ["content"] = message }
            };

            JArray toolsArray = BuildToolsArray(tools);

            var handlers = new Dictionary<string, Func<JObject, Task<object>>>(StringComparer.OrdinalIgnoreCase);
            foreach (var tool in tools)
                handlers[tool.Name] = tool.Handler;

            int totalToolCalls = 0;

            for (int turn = 1; turn <= MaxTurns; turn++)
            {
                ColorLine(string.Format("  [agent] Turn {0}/{1}", turn, MaxTurns), ConsoleColor.DarkCyan);

                var body = new JObject
                {
                    ["model"] = model,
                    ["instructions"] = instructions,
                    ["input"] = input,
                    ["tools"] = toolsArray,
                    ["store"] = false,
                };

                string responseJson = await PostRawAsync(body.ToString(Formatting.None));

                ResponsesResponse parsed;
                try
                {
                    parsed = JsonConvert.DeserializeObject<ResponsesResponse>(responseJson);
                }
                catch (Exception ex)
                {
                    return ("Agent error: failed to parse API response – " + ex.Message, turn, totalToolCalls);
                }

                if (parsed == null || parsed.Error != null)
                    return ("Agent error: " + (parsed?.Error?.Message ?? "null response"), turn, totalToolCalls);

                if (parsed.Usage != null)
                {
                    ColorLine(string.Format("  [agent] Tokens: in={0} out={1}",
                        parsed.Usage.InputTokens, parsed.Usage.OutputTokens), ConsoleColor.DarkGray);
                }

                List<OutputItem> toolCalls = ResponsesApiClient.GetToolCalls(parsed);

                if (toolCalls.Count == 0)
                {
                    string text = ResponsesApiClient.ExtractText(parsed);
                    ColorLine("  [agent] Completed", ConsoleColor.Green);
                    return (text, turn, totalToolCalls);
                }

                // Append all output items to input
                if (parsed.Output != null)
                {
                    foreach (OutputItem item in parsed.Output)
                    {
                        if (item.Type == "function_call")
                        {
                            input.Add(new JObject
                            {
                                ["type"] = "function_call",
                                ["call_id"] = item.CallId,
                                ["name"] = item.Name,
                                ["arguments"] = item.Arguments,
                            });
                        }
                    }
                }

                // Execute tool calls
                foreach (OutputItem call in toolCalls)
                {
                    totalToolCalls++;

                    JObject args;
                    try { args = JObject.Parse(call.Arguments ?? "{}"); }
                    catch { args = new JObject(); }

                    ColorLine(string.Format("  [agent] Tool: {0}({1})",
                        call.Name, Truncate(args.ToString(Formatting.None), 100)), ConsoleColor.DarkYellow);

                    string result;
                    try
                    {
                        Func<JObject, Task<object>> handler;
                        if (!handlers.TryGetValue(call.Name, out handler))
                        {
                            result = JsonConvert.SerializeObject(new { error = "Unknown tool: " + call.Name });
                        }
                        else
                        {
                            object resultObj = await handler(args);
                            result = resultObj is string s ? s : JsonConvert.SerializeObject(resultObj, Formatting.None);
                        }
                    }
                    catch (Exception ex)
                    {
                        result = JsonConvert.SerializeObject(new { error = ex.Message });
                    }

                    ColorLine(string.Format("  [agent]   -> {0}", Truncate(result, 200)), ConsoleColor.DarkGray);

                    input.Add(new JObject
                    {
                        ["type"] = "function_call_output",
                        ["call_id"] = call.CallId,
                        ["output"] = result,
                    });
                }
            }

            return ("Reached max turn limit before completion.", MaxTurns, totalToolCalls);
        }

        private static async Task<string> PostRawAsync(string jsonBody)
        {
            using (var http = new HttpClient())
            {
                http.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", AiConfig.ApiKey);

                if (AiConfig.Provider == "openrouter")
                {
                    if (!string.IsNullOrWhiteSpace(AiConfig.HttpReferer))
                        http.DefaultRequestHeaders.TryAddWithoutValidation("HTTP-Referer", AiConfig.HttpReferer);
                    if (!string.IsNullOrWhiteSpace(AiConfig.AppName))
                        http.DefaultRequestHeaders.TryAddWithoutValidation("X-Title", AiConfig.AppName);
                }

                using (var content = new StringContent(jsonBody, Encoding.UTF8, "application/json"))
                using (var response = await http.PostAsync(AiConfig.ApiEndpoint, content))
                {
                    return await response.Content.ReadAsStringAsync();
                }
            }
        }

        private static JArray BuildToolsArray(List<LocalToolDefinition> tools)
        {
            var arr = new JArray();
            foreach (var tool in tools)
            {
                arr.Add(new JObject
                {
                    ["type"] = "function",
                    ["name"] = tool.Name,
                    ["description"] = tool.Description,
                    ["parameters"] = tool.Parameters != null
                        ? JToken.FromObject(tool.Parameters)
                        : new JObject { ["type"] = "object", ["properties"] = new JObject(), ["additionalProperties"] = false },
                });
            }
            return arr;
        }

        private static void PrintEventTable(List<CalendarEvent> events)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(string.Format("\n  ┌────────────────────────────────────────────────────────────────────────────┐"));
            Console.WriteLine(string.Format("  │  📅  Calendar — {0} events{1}│",
                events.Count, new string(' ', Math.Max(0, 57 - events.Count.ToString().Length))));
            Console.WriteLine("  └────────────────────────────────────────────────────────────────────────────┘");
            Console.ResetColor();
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine(string.Format("  {0,-18}{1,-36}{2,-20}LOCATION",
                "ID", "TITLE", "WHEN"));
            Console.WriteLine("  " + new string('─', 96));
            Console.ResetColor();

            foreach (var evt in events)
            {
                string when = evt.Start.Length >= 16 ? evt.Start.Replace("T", " ").Substring(0, 16) : evt.Start;
                string loc = evt.IsVirtual ? "virtual" : evt.LocationName ?? "—";
                Console.WriteLine(string.Format("  {0,-18}{1,-36}{2,-20}{3}",
                    evt.Id, Truncate(evt.Title, 34), when, loc));
            }
        }

        private static void PrintNotificationTable(List<NotificationRecord> notifications)
        {
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine(string.Format("\n  ┌────────────────────────────────────────────────────────────────────────────┐"));
            Console.WriteLine(string.Format("  │  🔔  Notifications — {0} sent{1}│",
                notifications.Count, new string(' ', Math.Max(0, 53 - notifications.Count.ToString().Length))));
            Console.WriteLine("  └────────────────────────────────────────────────────────────────────────────┘");
            Console.ResetColor();
            Console.WriteLine();

            foreach (var n in notifications)
            {
                string when = n.CreatedAt.Length >= 16 ? n.CreatedAt.Replace("T", " ").Substring(0, 16) : n.CreatedAt;
                Console.WriteLine(string.Format("  {0}  [{1}] {2}", when, n.Channel, n.Title));
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine(string.Format("  {0}{1}", new string(' ', 17), Truncate(n.Message, 72)));
                Console.ResetColor();
                Console.WriteLine();
            }
        }

        private static string Truncate(string s, int max)
        {
            return s != null && s.Length > max ? s.Substring(0, max) + "..." : s ?? string.Empty;
        }

        private static void ColorLine(string text, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(text);
            Console.ResetColor();
        }
    }
}
