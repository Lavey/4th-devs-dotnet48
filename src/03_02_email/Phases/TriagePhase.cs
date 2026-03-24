using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FourthDevs.Email.Core;
using FourthDevs.Email.Data;
using FourthDevs.Email.Models;
using FourthDevs.Email.Prompts;
using FourthDevs.Email.Tools;
using FourthDevs.Common.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Email.Phases
{
    /// <summary>
    /// Result of the triage phase.
    /// </summary>
    public class TriageResult
    {
        public int Turns { get; set; }
        public List<ReplyPlan> ReplyPlans { get; set; } = new List<ReplyPlan>();
    }

    /// <summary>
    /// Triage phase: Responses API agentic loop with tools + mark_for_reply pseudo-tool.
    /// Reads emails, classifies, labels, and marks emails needing replies.
    /// </summary>
    public static class TriagePhase
    {
        private const int MaxTurns = 12;

        private static ToolDefinition MarkForReplySchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Name = "mark_for_reply",
                Description =
                    "Mark an email as needing a reply. A separate, isolated draft session will be created for it. " +
                    "KB access in the draft session will be scoped based on the sender's contact type.",
                Parameters = JObject.Parse(@"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""email_id"": { ""type"": ""string"", ""description"": ""ID of the email to reply to"" },
                        ""account"": { ""type"": ""string"", ""description"": ""Account that received the email"" },
                        ""reason"": { ""type"": ""string"", ""description"": ""Brief reason a reply is needed"" }
                    },
                    ""required"": [""email_id"", ""account"", ""reason""],
                    ""additionalProperties"": false
                }"),
            };
        }

        private static string HandleMarkForReply(JObject args, List<ReplyPlan> plans)
        {
            string emailId = args.Value<string>("email_id");
            string account = args.Value<string>("account");
            string reason = args.Value<string>("reason");

            var email = MockInbox.Emails.FirstOrDefault(e => e.Id == emailId);
            if (email == null)
                return JsonConvert.SerializeObject(new { error = $"Email not found: {emailId}" });

            string contactType = Contacts.ClassifyContact(account, email.From);
            string[] categories;
            Contacts.KBCategories.TryGetValue(contactType, out categories);
            if (categories == null) categories = new string[0];

            plans.Add(new ReplyPlan
            {
                EmailId = emailId,
                Account = account,
                RecipientEmail = email.From,
                ContactType = contactType,
                Reason = reason,
                Categories = categories.ToList(),
            });

            return JsonConvert.SerializeObject(new
            {
                marked = true,
                email_id = emailId,
                recipient = email.From,
                contact_type = contactType,
                kb_categories_allowed = categories,
            });
        }

        public static async Task<TriageResult> RunAsync(
            string model,
            string task,
            StateTracker tracker,
            Completion completion)
        {
            var replyPlans = new List<ReplyPlan>();
            string instructions = TriagePrompt.Build();

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"  [Triage] System prompt ({instructions.Length} chars)");
            Console.ResetColor();

            // Build tool definitions for the API
            var tools = ToolRegistry.AllTools.Select(t => new ToolDefinition
            {
                Type = "function",
                Name = t.Name,
                Description = t.Description,
                Parameters = t.Parameters,
            }).ToList();
            tools.Add(MarkForReplySchema());

            // Heterogeneous input list
            var input = new List<object>
            {
                new InputMessage { Role = "user", Content = task },
            };

            for (int turn = 0; turn < MaxTurns; turn++)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"\n  ── Turn {turn + 1} ──");
                Console.ResetColor();

                tracker.TakeSnapshotForTurn();

                var result = await completion.CompleteAsync(model, instructions, input, tools)
                    .ConfigureAwait(false);

                if (result.ToolCalls.Count == 0)
                {
                    // No more tool calls — triage complete
                    if (!string.IsNullOrEmpty(result.OutputText))
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"  [Triage] Final: {Truncate(result.OutputText, 200)}");
                        Console.ResetColor();
                    }
                    LogChanges(tracker);
                    return new TriageResult { Turns = turn + 1, ReplyPlans = replyPlans };
                }

                // Append all output items as input for the next turn
                foreach (var item in result.Output)
                {
                    input.Add(item);
                }

                // Process each tool call
                foreach (var tc in result.ToolCalls)
                {
                    JObject args;
                    try
                    {
                        args = JObject.Parse(tc.Arguments);
                    }
                    catch
                    {
                        input.Add(new ToolCallInput
                        {
                            Type = "function_call_output",
                            CallId = tc.CallId,
                            Output = "Error: Invalid JSON",
                        });
                        continue;
                    }

                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"  ⚡ {tc.Name}({Truncate(tc.Arguments, 120)})");
                    Console.ResetColor();

                    if (tc.Name == "mark_for_reply")
                    {
                        string output = HandleMarkForReply(args, replyPlans);
                        Console.ForegroundColor = ConsoleColor.Magenta;
                        Console.WriteLine($"    → {Truncate(output, 160)}");
                        Console.ResetColor();
                        input.Add(new ToolCallInput
                        {
                            Type = "function_call_output",
                            CallId = tc.CallId,
                            Output = output,
                        });
                        continue;
                    }

                    ToolDef tool;
                    if (!ToolRegistry.ToolMap.TryGetValue(tc.Name, out tool))
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"    ✗ Unknown tool: {tc.Name}");
                        Console.ResetColor();
                        input.Add(new ToolCallInput
                        {
                            Type = "function_call_output",
                            CallId = tc.CallId,
                            Output = $"Unknown tool: {tc.Name}",
                        });
                        continue;
                    }

                    try
                    {
                        var toolResult = await tool.Handler(args).ConfigureAwait(false);
                        string output = JsonConvert.SerializeObject(toolResult,
                            new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.WriteLine($"    → {Truncate(output, 160)}");
                        Console.ResetColor();
                        input.Add(new ToolCallInput
                        {
                            Type = "function_call_output",
                            CallId = tc.CallId,
                            Output = output,
                        });
                    }
                    catch (Exception ex)
                    {
                        string msg = ex.Message;
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"    ✗ Error: {msg}");
                        Console.ResetColor();
                        input.Add(new ToolCallInput
                        {
                            Type = "function_call_output",
                            CallId = tc.CallId,
                            Output = $"Error: {msg}",
                        });
                    }
                }

                LogChanges(tracker);
            }

            return new TriageResult { Turns = MaxTurns, ReplyPlans = replyPlans };
        }

        private static void LogChanges(StateTracker tracker)
        {
            var kbAccesses = tracker.CollectKnowledgeAccess();
            if (kbAccesses.Count > 0)
            {
                Console.ForegroundColor = ConsoleColor.DarkCyan;
                Console.WriteLine($"  [KB] {kbAccesses.Count} knowledge access(es) this turn");
                Console.ResetColor();
            }

            var changes = tracker.CollectChanges();
            if (changes.Count > 0)
            {
                Console.ForegroundColor = ConsoleColor.DarkGreen;
                foreach (var c in changes)
                {
                    Console.WriteLine($"  [Change] {c.Type}: {c.LabelName ?? c.DraftSubject ?? c.EmailId}");
                }
                Console.ResetColor();
            }
        }

        private static string Truncate(string s, int maxLen)
        {
            if (s == null) return "";
            return s.Length <= maxLen ? s : s.Substring(0, maxLen) + "…";
        }
    }
}
