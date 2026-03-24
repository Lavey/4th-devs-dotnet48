using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FourthDevs.Email.Core;
using FourthDevs.Email.Data;
using FourthDevs.Email.Models;
using FourthDevs.Email.Phases;
using FourthDevs.Email.Tools;

namespace FourthDevs.Email.Agent
{
    /// <summary>
    /// Two-phase agent orchestrator:
    ///   Phase 1: Triage — read, classify, label emails, mark those needing replies
    ///   Phase 2: Draft Sessions — isolated completion for each reply plan
    /// </summary>
    public class AgentResult
    {
        public string Response { get; set; }
        public int Turns { get; set; }
        public int DraftSessions { get; set; }
    }

    public static class AgentRunner
    {
        public static async Task<AgentResult> RunAsync(string model, string task)
        {
            var tracker = new StateTracker();

            // Banner
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine();
            Console.WriteLine("  ╔══════════════════════════════════════════════════════╗");
            Console.WriteLine("  ║              03_02_email — Email Agent               ║");
            Console.WriteLine("  ╚══════════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine($"  Model:    {model}");
            Console.WriteLine($"  Accounts: {string.Join(", ", MockInbox.Accounts.Select(a => a.EmailAddress))}");
            Console.WriteLine($"  Tools:    {ToolRegistry.AllTools.Count + 1} (including mark_for_reply)");
            Console.WriteLine($"  Task:     {task}");
            Console.WriteLine();

            // Inbox overview
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("  Inbox Overview:");
            foreach (var acct in MockInbox.Accounts)
            {
                var acctEmails = MockInbox.Emails.Where(e => e.Account == acct.EmailAddress).ToList();
                int unread = acctEmails.Count(e => !e.IsRead);
                Console.WriteLine($"    {acct.EmailAddress}: {acctEmails.Count} emails ({unread} unread)");
            }
            Console.ResetColor();

            // Phase 1: Triage
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("\n  ═══ Phase 1: Triage ═══");
            Console.WriteLine("  Read, classify, label — no drafts");
            Console.ResetColor();

            using (var completion = new Completion())
            {
                var triage = await TriagePhase.RunAsync(model, task, tracker, completion)
                    .ConfigureAwait(false);

                // Phase 2: Isolated draft sessions
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine($"\n  ═══ Phase 2: Draft Sessions ({triage.ReplyPlans.Count} isolated contexts) ═══");
                Console.ResetColor();

                var draftResults = new List<DraftSessionResult>();
                foreach (var plan in triage.ReplyPlans)
                {
                    tracker.TakeSnapshotForTurn();
                    var draftResult = await DraftPhase.RunSessionAsync(model, plan, completion)
                        .ConfigureAwait(false);
                    draftResults.Add(draftResult);
                    tracker.CollectChanges();
                }

                // Final summary
                PrintSummary(tracker, draftResults);

                string response = $"Triage completed in {triage.Turns} turns. " +
                                  $"{triage.ReplyPlans.Count} draft sessions executed with isolated KB scoping. " +
                                  $"{draftResults.Count} drafts created.";

                return new AgentResult
                {
                    Response = response,
                    Turns = triage.Turns,
                    DraftSessions = draftResults.Count,
                };
            }
        }

        private static void PrintSummary(StateTracker tracker, List<DraftSessionResult> draftResults)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("\n  ═══ Summary ═══");
            Console.ResetColor();

            var allChanges = tracker.AllChanges();
            if (allChanges.Count > 0)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"  Total changes: {allChanges.Count}");
                foreach (var c in allChanges)
                {
                    string detail = c.Type == "draft_created"
                        ? $"Draft to {string.Join(", ", c.DraftTo ?? new List<string>())}: {c.DraftSubject}"
                        : $"{c.LabelName} on {c.EmailSubject ?? c.Account}";
                    Console.WriteLine($"    [{c.Type}] {detail}");
                }
                Console.ResetColor();
            }

            if (draftResults.Count > 0)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"\n  Drafts created: {draftResults.Count}");
                foreach (var d in draftResults)
                {
                    Console.WriteLine($"    {d.DraftId} → {d.Plan.RecipientEmail} ({d.Plan.ContactType})");
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"      KB loaded: {d.KBEntriesLoaded.Count}, blocked: {d.KBEntriesBlocked.Count}");
                    Console.ForegroundColor = ConsoleColor.Cyan;
                }
                Console.ResetColor();
            }

            var kbAccesses = tracker.AllKnowledgeAccess();
            if (kbAccesses.Count > 0)
            {
                Console.ForegroundColor = ConsoleColor.DarkCyan;
                Console.WriteLine($"\n  Total KB accesses: {kbAccesses.Count}");
                Console.ResetColor();
            }
        }
    }
}
