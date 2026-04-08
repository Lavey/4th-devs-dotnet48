using System.Collections.Generic;
using FourthDevs.AxClassifier.Models;

namespace FourthDevs.AxClassifier.Data
{
    public static class TrainingData
    {
        public static readonly List<LabeledEmail> TrainingSet = new List<LabeledEmail>
        {
            new LabeledEmail
            {
                EmailFrom = "notifications@github.com",
                EmailSubject = "[acme/api-gateway] PR #347: Fix race condition in connection pool",
                EmailBody = "@mkowalski requested your review on acme/api-gateway#347\n\nChanges:\n- Replaced mutex with RWLock in pool.rs\n- Added regression test for concurrent checkout\n\nFiles changed: 3  Additions: 87  Deletions: 24",
                Labels = new[] { "github", "automated", "needs-reply" },
                Priority = "medium",
                NeedsReply = true
            },
            new LabeledEmail
            {
                EmailFrom = "ci@github.com",
                EmailSubject = "[acme/web-app] CI failed on main \u2014 build #1892",
                EmailBody = "Build #1892 on branch main failed.\n\nJob: test-integration\nError: ECONNREFUSED 127.0.0.1:5432 \u2014 database container did not start in time\n\nAuthor: dependabot[bot]",
                Labels = new[] { "github", "automated" },
                Priority = "high",
                NeedsReply = false
            },
            new LabeledEmail
            {
                EmailFrom = "newsletter@javascriptweekly.com",
                EmailSubject = "JavaScript Weekly #721: Node 24 LTS, Bun 1.3, and V8 perf deep-dive",
                EmailBody = "JavaScript Weekly \u2014 Issue #721\n\n\u25b8 Node.js 24 enters LTS\n\u25b8 Bun 1.3 ships native S3 client\n\u25b8 V8 deep-dive: Maglev JIT\n\nUnsubscribe: https://javascriptweekly.com/unsubscribe",
                Labels = new[] { "newsletter", "automated" },
                Priority = "low",
                NeedsReply = false
            },
            new LabeledEmail
            {
                EmailFrom = "anna.berg@northstar.io",
                EmailSubject = "API integration \u2014 timeline question",
                EmailBody = "Hi,\n\nWe're planning to integrate your events API. Questions:\n1. Is the v2 webhooks endpoint stable?\n2. Do you support batch delivery?\n3. Any rate limits?\n\nCould you hop on a 30-min call Thursday?\n\nAnna Berg\nNorthstar Analytics",
                Labels = new[] { "client", "needs-reply" },
                Priority = "medium",
                NeedsReply = true
            },
            new LabeledEmail
            {
                EmailFrom = "billing@vercel.com",
                EmailSubject = "Your invoice for March 2026 is ready",
                EmailBody = "Your Vercel Pro invoice for March 2026 is available.\n\nAmount: $42.00\nPlan: Pro (2 members)\n\nView invoice: https://vercel.com/account/billing",
                Labels = new[] { "billing", "automated" },
                Priority = "low",
                NeedsReply = false
            },
            new LabeledEmail
            {
                EmailFrom = "kasia.dev@acme.com",
                EmailSubject = "Quick sync on caching strategy",
                EmailBody = "Hey,\n\nBefore sprint planning \u2014 I've been looking at Redis vs in-memory cache for the session store.\n\nRedis: shared state, TTL built-in. In-memory: zero latency.\n\nLeaning Redis since we're going multi-pod. Thoughts?\n\nKasia",
                Labels = new[] { "internal", "needs-reply" },
                Priority = "medium",
                NeedsReply = true
            },
            new LabeledEmail
            {
                EmailFrom = "recruiter@talentforge.io",
                EmailSubject = "Exclusive: Staff Engineer at stealth AI unicorn \u2014 $400k TC",
                EmailBody = "Your GitHub profile caught our eye! Staff Engineer role, $380-420k + equity, fully remote. 15 minutes for a quick chat?\n\nJake Miller\nTalentForge Recruiting",
                Labels = new[] { "spam" },
                Priority = "low",
                NeedsReply = false
            },
            new LabeledEmail
            {
                EmailFrom = "security@github.com",
                EmailSubject = "[Security Alert] Dependabot found 2 high-severity vulnerabilities in acme/api-gateway",
                EmailBody = "GitHub found 2 high-severity vulnerabilities:\n\n1. CVE-2026-1234 \u2014 Prototype pollution in lodash\n2. CVE-2026-5678 \u2014 ReDoS in semver\n\nDependabot PRs opened automatically.",
                Labels = new[] { "security", "github", "automated", "urgent" },
                Priority = "high",
                NeedsReply = false
            },
            new LabeledEmail
            {
                EmailFrom = "no-reply@linear.app",
                EmailSubject = "You were assigned: ACME-412 \u2014 Implement rate limiter middleware",
                EmailBody = "You were assigned to ACME-412.\n\nPriority: High\nDue: Apr 1, 2026\n\nAdd token-bucket rate limiter to API gateway.",
                Labels = new[] { "automated", "urgent" },
                Priority = "high",
                NeedsReply = false
            },
            new LabeledEmail
            {
                EmailFrom = "noreply@aws.amazon.com",
                EmailSubject = "AWS Budget Alert: 80% of monthly budget reached",
                EmailBody = "Budget: Monthly Infrastructure\nThreshold: 80% ($4,000 of $5,000)\nCurrent spend: $4,127.43\nForecasted: $5,480.00",
                Labels = new[] { "automated", "billing" },
                Priority = "medium",
                NeedsReply = false
            },
        };

        public static readonly List<LabeledEmail> ValidationSet = new List<LabeledEmail>
        {
            new LabeledEmail
            {
                EmailFrom = "notifications@github.com",
                EmailSubject = "[acme/core] Issue #201: Memory leak in worker thread pool",
                EmailBody = "@you was mentioned: \"Can you look at this? Seems related to the pool changes from last week.\" Memory grows ~50MB/hour under load.",
                Labels = new[] { "github", "automated", "needs-reply" },
                Priority = "medium",
                NeedsReply = true
            },
            new LabeledEmail
            {
                EmailFrom = "sales@saasplatform.io",
                EmailSubject = "Your trial expires in 3 days \u2014 upgrade now!",
                EmailBody = "Hi there,\n\nYour SaasPlatform trial ends March 28. Upgrade to Pro for $29/mo and keep all your data.\n\nDon't miss out!\nThe SaasPlatform Team",
                Labels = new[] { "spam", "automated" },
                Priority = "low",
                NeedsReply = false
            },
            new LabeledEmail
            {
                EmailFrom = "tomek.brandt@shopflow.de",
                EmailSubject = "Re: Webhook delivery failures \u2014 any update?",
                EmailBody = "We're still seeing ~3% webhook delivery failures, primarily 503s. Our integration team needs a status update. Is this related to the replication issues?\n\nTomek Brandt\nCTO, ShopFlow",
                Labels = new[] { "client", "needs-reply", "urgent" },
                Priority = "high",
                NeedsReply = true
            },
            new LabeledEmail
            {
                EmailFrom = "noreply@sentry.io",
                EmailSubject = "[Sentry] ACME-API: RangeError: Maximum call stack size exceeded (142 events)",
                EmailBody = "Issue: RangeError: Maximum call stack size exceeded\nProject: acme-api\nEvents: 142 in last hour\nFirst seen: 10 min ago\nAffects: /api/v2/webhooks endpoint",
                Labels = new[] { "automated", "urgent" },
                Priority = "high",
                NeedsReply = false
            },
            new LabeledEmail
            {
                EmailFrom = "patryk.wisniewski@acme.com",
                EmailSubject = "Portfolio page \u2014 tone of voice direction?",
                EmailBody = "Hey,\n\nWorking on the new portfolio page. Should we go:\nA) Professional/corporate\nB) Casual/confident\nC) Minimal \u2014 let work speak for itself\n\nLeaning B. Thoughts?\n\nPatryk",
                Labels = new[] { "internal", "needs-reply" },
                Priority = "low",
                NeedsReply = true
            },
        };
    }
}
