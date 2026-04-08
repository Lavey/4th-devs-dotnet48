using System.Collections.Generic;
using FourthDevs.AxClassifier.Models;

namespace FourthDevs.AxClassifier.Data
{
    public static class FallbackExamples
    {
        public static readonly List<LabeledEmail> Examples = new List<LabeledEmail>
        {
            new LabeledEmail
            {
                EmailFrom = "jan.kowalski@acme.com",
                EmailSubject = "Should we migrate to Drizzle ORM?",
                EmailBody = "Hey, I've been looking at Drizzle vs Prisma for the new service. What do you think? Can we chat before standup?",
                Labels = new[] { "internal", "needs-reply" },
                Priority = "medium",
                NeedsReply = true,
                Summary = "Teammate asks for opinion on ORM choice before standup."
            },
            new LabeledEmail
            {
                EmailFrom = "no-reply@linear.app",
                EmailSubject = "You were assigned: PLAT-99 \u2014 Hotfix: auth token leak in prod",
                EmailBody = "Priority: Urgent. Sprint: current. Due: today. Description: Auth tokens are leaking in error responses on /api/login. Fix immediately.",
                Labels = new[] { "automated", "urgent" },
                Priority = "high",
                NeedsReply = false,
                Summary = "Urgent task assignment to hotfix auth token leak in production."
            },
            new LabeledEmail
            {
                EmailFrom = "recruiter@hireme.io",
                EmailSubject = "Amazing CTO opportunity \u2014 $500k+ equity",
                EmailBody = "Hi! Your GitHub repos are incredible. I have a once-in-a-lifetime CTO role at a funded startup. 15 min call?",
                Labels = new[] { "spam" },
                Priority = "low",
                NeedsReply = false,
                Summary = "Unsolicited recruiter outreach for a CTO position."
            },
            new LabeledEmail
            {
                EmailFrom = "notifications@github.com",
                EmailSubject = "[org/repo] Issue #88: Memory leak in worker pool",
                EmailBody = "@you was mentioned in org/repo#88. \"Can you take a look at this? Seems related to the pool changes you merged last week.\"",
                Labels = new[] { "github", "automated", "needs-reply" },
                Priority = "medium",
                NeedsReply = true,
                Summary = "GitHub issue mention requesting investigation of a memory leak."
            },
            new LabeledEmail
            {
                EmailFrom = "billing@stripe.com",
                EmailSubject = "Invoice #INV-2026-0312 for Stripe Billing",
                EmailBody = "Your monthly invoice for $249.00 is ready. Payment will be charged to Visa ending 1234 on Apr 1.",
                Labels = new[] { "billing", "automated" },
                Priority = "low",
                NeedsReply = false,
                Summary = "Monthly Stripe billing invoice notification."
            },
        };
    }
}
