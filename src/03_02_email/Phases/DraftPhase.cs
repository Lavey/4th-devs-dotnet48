using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FourthDevs.Email.Core;
using FourthDevs.Email.Data;
using FourthDevs.Email.Knowledge;
using FourthDevs.Email.Models;
using FourthDevs.Email.Prompts;
using FourthDevs.Common.Models;

namespace FourthDevs.Email.Phases
{
    /// <summary>
    /// Draft phase: for each ReplyPlan, runs an isolated completion session
    /// with the KB locked to the plan's account and scoped to the contact type.
    /// </summary>
    public static class DraftPhase
    {
        private static int _draftCounter = 0;

        public static async Task<DraftSessionResult> RunSessionAsync(
            string model,
            ReplyPlan plan,
            Completion completion)
        {
            AccessLock.LockKnowledgeToAccount(plan.Account);
            try
            {
                var ctx = DraftPrompt.BuildContext(plan);

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"\n  ── Draft Session: {plan.RecipientEmail} ({plan.ContactType}) ──");
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"  KB loaded: {ctx.Scoped.Loaded.Count}, blocked: {ctx.Scoped.Blocked.Count}");
                Console.ResetColor();

                string prompt = DraftPrompt.Build(ctx);

                var input = new List<object>
                {
                    new InputMessage { Role = "user", Content = "Write the reply." },
                };

                var result = await completion.CompleteAsync(model, prompt, input)
                    .ConfigureAwait(false);

                string body = result.OutputText ?? "";

                _draftCounter++;
                var draft = new Draft
                {
                    Id = $"draft-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-{_draftCounter}",
                    Account = plan.Account,
                    To = new List<string> { ctx.Email.From },
                    Cc = new List<string>(),
                    Subject = $"Re: {ctx.Email.Subject}",
                    Body = body,
                    InReplyTo = ctx.Email.Id,
                    CreatedAt = DateTime.UtcNow.ToString("o"),
                };
                MockInbox.Drafts.Add(draft);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"  ✓ Draft {draft.Id} created ({body.Length} chars)");
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"  Preview: {Truncate(body, 200)}");
                Console.ResetColor();

                var entriesLoaded = new List<KBEntryInfo>();
                foreach (var e in ctx.Scoped.Loaded)
                {
                    entriesLoaded.Add(new KBEntryInfo { Id = e.Id, Title = e.Title, Category = e.Category });
                }

                return new DraftSessionResult
                {
                    Plan = plan,
                    DraftId = draft.Id,
                    KBEntriesLoaded = entriesLoaded,
                    KBEntriesBlocked = ctx.Scoped.Blocked,
                    DraftBody = body,
                };
            }
            finally
            {
                AccessLock.UnlockKnowledge();
            }
        }

        private static string Truncate(string s, int maxLen)
        {
            if (s == null) return "";
            return s.Length <= maxLen ? s : s.Substring(0, maxLen) + "…";
        }
    }
}
