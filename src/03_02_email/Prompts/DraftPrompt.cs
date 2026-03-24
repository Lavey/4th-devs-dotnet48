using System.Collections.Generic;
using System.Linq;
using FourthDevs.Email.Data;
using FourthDevs.Email.Knowledge;
using FourthDevs.Email.Models;

namespace FourthDevs.Email.Prompts
{
    /// <summary>
    /// Builds the context and system prompt for draft sessions.
    /// </summary>
    public class DraftPromptContext
    {
        public ReplyPlan Plan { get; set; }
        public Models.Email Email { get; set; }
        public List<Models.Email> Thread { get; set; }
        public ScopedKBResult Scoped { get; set; }
    }

    public static class DraftPrompt
    {
        public static DraftPromptContext BuildContext(ReplyPlan plan)
        {
            var email = MockInbox.Emails.First(e => e.Id == plan.EmailId);
            var thread = MockInbox.Emails
                .Where(e => e.ThreadId == email.ThreadId && e.Id != email.Id)
                .OrderBy(e => System.DateTime.Parse(e.Date))
                .ToList();
            var scoped = Scoping.GetScopedKnowledge(plan.Account, plan.ContactType);

            return new DraftPromptContext
            {
                Plan = plan,
                Email = email,
                Thread = thread,
                Scoped = scoped,
            };
        }

        public static string Build(DraftPromptContext ctx)
        {
            string kb = RenderKB(ctx.Scoped);
            string threadSection = RenderThread(ctx.Thread);
            string threadBlock = threadSection.Length > 0
                ? $"\n## Previous messages in thread\n\n{threadSection}"
                : "";

            return $@"You are drafting a reply to an email. Write ONLY the reply body — no subject line, no metadata.

## Context
- You are: {ctx.Plan.Account}
- Replying to: {ctx.Email.From} (contact type: {ctx.Plan.ContactType})
- Reason for reply: {ctx.Plan.Reason}

## Email to reply to
From: {ctx.Email.From}
Subject: {ctx.Email.Subject}
Date: {ctx.Email.Date}

{ctx.Email.Body}
{threadBlock}
## Available Knowledge
{kb}

## Rules
- Use ONLY the knowledge provided above. Do not reference information not present here.
- Match the language of the original email (if they wrote in Polish, reply in Polish).
- If you cannot fulfill a request (e.g. no banking access, no attachment access), explicitly say so.
- If the email requests data you don't have in the knowledge above, say you cannot share that information.
- Be concise: under 150 words.";
        }

        private static string RenderKB(ScopedKBResult scoped)
        {
            if (scoped.Loaded.Count > 0)
            {
                return string.Join("\n\n",
                    scoped.Loaded.Select(e => $"### {e.Title}\n{e.Content}"));
            }
            return "No knowledge base entries available for this contact type.";
        }

        private static string RenderThread(List<Models.Email> thread)
        {
            if (thread.Count > 0)
            {
                return string.Join("\n\n---\n\n",
                    thread.Select(m => $"From: {m.From}\nDate: {m.Date}\n\n{m.Body}"));
            }
            return "";
        }
    }
}
