using System.Collections.Generic;
using FourthDevs.AxClassifier.Models;

namespace FourthDevs.AxClassifier.Data
{
    public static class EmailData
    {
        public static readonly List<Email> Emails = new List<Email>
        {
            new Email
            {
                Id = "e001",
                From = "notifications@github.com",
                Subject = "[acme/api-gateway] PR #347: Fix race condition in connection pool",
                Body = "@mkowalski requested your review on acme/api-gateway#347\n\n" +
                       "Changes:\n" +
                       "- Replaced mutex with RWLock in pool.rs\n" +
                       "- Added regression test for concurrent checkout\n" +
                       "- Benchmark shows 12% throughput improvement under contention\n\n" +
                       "Files changed: 3  Additions: 87  Deletions: 24\n\n" +
                       "View PR: https://github.com/acme/api-gateway/pull/347"
            },
            new Email
            {
                Id = "e002",
                From = "ci@github.com",
                Subject = "[acme/web-app] CI failed on main \u2014 build #1892",
                Body = "Build #1892 on branch main failed.\n\n" +
                       "Job: test-integration\n" +
                       "Step: Run integration tests\n" +
                       "Error: ECONNREFUSED 127.0.0.1:5432 \u2014 database container did not start in time\n\n" +
                       "Commit: abc1234 \"update deps and fix lint warnings\"\n" +
                       "Author: dependabot[bot]\n\n" +
                       "View logs: https://github.com/acme/web-app/actions/runs/1892"
            },
            new Email
            {
                Id = "e003",
                From = "newsletter@javascriptweekly.com",
                Subject = "JavaScript Weekly #721: Node 24 LTS, Bun 1.3, and V8 perf deep-dive",
                Body = "JavaScript Weekly \u2014 Issue #721\n\n" +
                       "\u25b8 Node.js 24 enters LTS with require(esm) enabled by default\n" +
                       "\u25b8 Bun 1.3 ships native S3 client and cross-compile support\n" +
                       "\u25b8 V8 deep-dive: how Maglev JIT reduces cold-start latency by 30%\n" +
                       "\u25b8 TC39: Iterator helpers and Temporal reach Stage 4\n" +
                       "\u25b8 Tutorial: building a real-time dashboard with Hono + htmx\n\n" +
                       "Read online: https://javascriptweekly.com/issues/721\n" +
                       "Unsubscribe: https://javascriptweekly.com/unsubscribe"
            },
            new Email
            {
                Id = "e004",
                From = "anna.berg@northstar.io",
                Subject = "API integration \u2014 timeline question",
                Body = "Hi,\n\n" +
                       "We're planning to integrate your events API into our analytics pipeline. " +
                       "Our PM is asking for a realistic go-live date.\n\n" +
                       "Questions:\n" +
                       "1. Is the v2 webhooks endpoint stable or still in beta?\n" +
                       "2. Do you support batch delivery (we process ~50k events/hour)?\n" +
                       "3. Any rate limits we should plan around?\n\n" +
                       "We'd like to kick off next sprint if possible. " +
                       "Could you hop on a 30-min call Thursday?\n\n" +
                       "Thanks,\n" +
                       "Anna Berg\n" +
                       "Senior Engineer, Northstar Analytics"
            },
            new Email
            {
                Id = "e005",
                From = "billing@vercel.com",
                Subject = "Your invoice for March 2026 is ready",
                Body = "Hi,\n\n" +
                       "Your Vercel Pro invoice for March 2026 is available.\n\n" +
                       "Amount: $42.00\n" +
                       "Plan: Pro (2 members)\n" +
                       "Bandwidth: 312 GB used of unlimited\n" +
                       "Serverless executions: 1.2M\n\n" +
                       "View invoice: https://vercel.com/account/billing\n" +
                       "Payment method: Visa ending in 8821\n\n" +
                       "Thanks,\n" +
                       "Vercel Billing"
            },
            new Email
            {
                Id = "e006",
                From = "kasia.dev@acme.com",
                Subject = "Quick sync on caching strategy",
                Body = "Hey,\n\n" +
                       "Before sprint planning tomorrow \u2014 I've been looking at the Redis vs " +
                       "in-memory cache question for the session store.\n\n" +
                       "Redis pros: shared state across pods, TTL built-in, persistence option.\n" +
                       "Downside: extra infra cost (~$45/mo for managed), adds network hop (~2ms p99).\n\n" +
                       "In-memory (Map + LRU): zero latency, dead simple.\n" +
                       "Downside: no sharing between pods, lost on restart.\n\n" +
                       "I'm leaning Redis since we're about to go multi-pod. Thoughts?\n\n" +
                       "Kasia"
            },
            new Email
            {
                Id = "e007",
                From = "recruiter@talentforge.io",
                Subject = "Exclusive: Staff Engineer at stealth AI unicorn \u2014 $400k TC",
                Body = "Hi there,\n\n" +
                       "Your GitHub profile caught our eye! We have an incredible opportunity:\n\n" +
                       "Role: Staff Engineer (Platform)\n" +
                       "Company: Stealth AI startup (Series C, $2B valuation)\n" +
                       "TC: $380-420k + equity\n" +
                       "Location: Fully remote\n\n" +
                       "The founders are ex-Google DeepMind and they're building the future of AI agents.\n\n" +
                       "15 minutes for a quick chat? I promise it'll be worth your time!\n\n" +
                       "Best,\n" +
                       "Jake Miller\n" +
                       "TalentForge Recruiting"
            },
            new Email
            {
                Id = "e008",
                From = "security@github.com",
                Subject = "[Security Alert] Dependabot found 2 high-severity vulnerabilities in acme/api-gateway",
                Body = "GitHub found 2 high-severity vulnerabilities in your repository acme/api-gateway.\n\n" +
                       "1. CVE-2026-1234 \u2014 Prototype pollution in lodash < 4.17.22\n" +
                       "   Severity: High | CVSS: 8.1\n" +
                       "   Fix: upgrade lodash to >= 4.17.22\n\n" +
                       "2. CVE-2026-5678 \u2014 ReDoS in semver < 7.5.5\n" +
                       "   Severity: High | CVSS: 7.5\n" +
                       "   Fix: upgrade semver to >= 7.5.5\n\n" +
                       "Dependabot PRs have been opened automatically.\n\n" +
                       "Review alerts: https://github.com/acme/api-gateway/security/dependabot"
            },
            new Email
            {
                Id = "e009",
                From = "no-reply@linear.app",
                Subject = "You were assigned: ACME-412 \u2014 Implement rate limiter middleware",
                Body = "You were assigned to ACME-412.\n\n" +
                       "Title: Implement rate limiter middleware\n" +
                       "Priority: High\n" +
                       "Sprint: Sprint 22 (Mar 24 \u2013 Apr 4)\n" +
                       "Due: Apr 1, 2026\n\n" +
                       "Description:\n" +
                       "Add token-bucket rate limiter to API gateway. Must support per-tenant " +
                       "limits from config, return 429 with Retry-After header, and emit metrics " +
                       "to Prometheus.\n\n" +
                       "Acceptance criteria:\n" +
                       "- Configurable per-tenant limits\n" +
                       "- 429 response with correct headers\n" +
                       "- Grafana dashboard panel\n" +
                       "- Load test passing at 10k req/s\n\n" +
                       "View issue: https://linear.app/acme/issue/ACME-412"
            },
            new Email
            {
                Id = "e010",
                From = "noreply@aws.amazon.com",
                Subject = "AWS Budget Alert: 80% of monthly budget reached",
                Body = "AWS Budgets Notification\n\n" +
                       "Account: acme-production (123456789012)\n" +
                       "Budget: Monthly Infrastructure\n" +
                       "Threshold: 80% ($4,000.00 of $5,000.00)\n" +
                       "Current spend: $4,127.43\n" +
                       "Forecasted end-of-month: $5,480.00\n\n" +
                       "Top cost drivers:\n" +
                       "- EC2: $1,890 (46%)\n" +
                       "- RDS: $1,120 (27%)\n" +
                       "- Data Transfer: $580 (14%)\n\n" +
                       "Review your costs: https://console.aws.amazon.com/billing/home"
            },
        };
    }
}
