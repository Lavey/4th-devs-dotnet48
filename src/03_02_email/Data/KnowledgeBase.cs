using System.Collections.Generic;
using FourthDevs.Email.Models;

namespace FourthDevs.Email.Data
{
    /// <summary>
    /// Static knowledge base: shared entries + per-account entries.
    /// </summary>
    public static class KnowledgeBase
    {
        public static readonly List<KnowledgeEntry> Entries = new List<KnowledgeEntry>
        {
            // ─── Shared knowledge ───────────────────────────────────────────────

            new KnowledgeEntry
            {
                Id = "kb-shared-001",
                Account = "shared",
                Title = "Email response guidelines",
                Category = "communication",
                Content = "## Response Guidelines\n\n" +
                    "- Reply within 24h on business days, 48h if research is needed.\n" +
                    "- Always address the sender by first name.\n" +
                    "- Keep responses concise — under 150 words for simple queries.\n" +
                    "- For client emails: professional but warm tone, no jargon.\n" +
                    "- For internal emails: casual tone is fine, be direct.\n" +
                    "- Always close with a clear next step or question.\n" +
                    "- Never commit to deadlines without checking with the relevant team first.",
                UpdatedAt = "2026-01-15T10:00:00Z",
            },
            new KnowledgeEntry
            {
                Id = "kb-shared-002",
                Account = "shared",
                Title = "Labeling policy",
                Category = "organization",
                Content = "## Labeling Policy\n\n" +
                    "### Priority labels\n" +
                    "- **Urgent**: requires action within 4 hours (client escalations, outages).\n" +
                    "- **Client**: any email from or about an external client.\n" +
                    "- **Internal**: team communication, sprint planning, etc.\n\n" +
                    "### Content labels\n" +
                    "- **Invoice**: anything billing/payment related.\n" +
                    "- **Newsletter**: automated digests, can batch-process weekly.\n" +
                    "- **Recruitment**: unsolicited job offers — low priority unless actively looking.\n\n" +
                    "### Rules\n" +
                    "- Every email should have at least one label.\n" +
                    "- System labels (INBOX, SENT, etc.) don't count toward this rule.\n" +
                    "- Prefer existing labels over creating new ones.",
                UpdatedAt = "2026-02-01T08:00:00Z",
            },
            new KnowledgeEntry
            {
                Id = "kb-shared-003",
                Account = "shared",
                Title = "Adam Kowalski — personal context",
                Category = "owner",
                Content = "## About the owner\n\n" +
                    "Adam Kowalski runs two businesses:\n" +
                    "- **TechVolt** — B2B SaaS, monitoring/observability platform.\n" +
                    "- **CreativeSpark** — creative agency, branding & design services.\n\n" +
                    "Timezone: CET (Europe/Warsaw).\n" +
                    "Preferred meeting times: Tue–Thu, 10:00–16:00.\n" +
                    "Currently not looking for new employment (ignore recruiter emails).",
                UpdatedAt = "2026-02-10T12:00:00Z",
            },

            // ─── TechVolt knowledge ─────────────────────────────────────────────

            new KnowledgeEntry
            {
                Id = "kb-tv-001",
                Account = "adam@techvolt.io",
                Title = "TechVolt — product overview",
                Category = "product",
                Content = "## TechVolt Platform\n\n" +
                    "Real-time infrastructure monitoring SaaS.\n\n" +
                    "### Core features\n" +
                    "- API monitoring (REST, GraphQL, gRPC)\n" +
                    "- Webhook delivery tracking with retry logic\n" +
                    "- Database replication monitoring\n" +
                    "- Custom dashboards and alerting\n\n" +
                    "### API versions\n" +
                    "- v2 (stable, current) — full feature set\n" +
                    "- v3 (migration in progress) — 78% endpoint coverage, target: Q2 2026\n\n" +
                    "### Pricing tiers\n" +
                    "- Starter: up to 50k requests/month — $99/mo\n" +
                    "- Growth: up to 200k requests/month — $349/mo\n" +
                    "- Enterprise: 500k+ requests/month — custom pricing, starts at $899/mo\n" +
                    "- All tiers include 99.9% SLA, Enterprise gets 99.95%",
                UpdatedAt = "2026-02-05T14:00:00Z",
            },
            new KnowledgeEntry
            {
                Id = "kb-tv-002",
                Account = "adam@techvolt.io",
                Title = "TechVolt — key clients",
                Category = "clients",
                Content = "## Key Clients\n\n" +
                    "### Nexon (maria.jensen@nexon.com)\n" +
                    "- Partnership discussion for Q2 integration\n" +
                    "- Volume: ~500k requests/month (Enterprise tier)\n" +
                    "- Status: pricing proposal pending\n" +
                    "- Priority: HIGH — strategic partnership\n\n" +
                    "### ShopFlow (tomek.brandt@shopflow.de)\n" +
                    "- Current customer, Growth tier\n" +
                    "- Ongoing issue: ~3% webhook delivery failures (503s)\n" +
                    "- Likely related to db-replica-03 replication lag\n" +
                    "- Status: awaiting resolution, Tomek following up\n" +
                    "- Priority: HIGH — retention risk",
                UpdatedAt = "2026-02-18T10:00:00Z",
            },
            new KnowledgeEntry
            {
                Id = "kb-tv-003",
                Account = "adam@techvolt.io",
                Title = "TechVolt — team",
                Category = "team",
                Content = "## Team\n\n" +
                    "- **Kasia Nowak** (kasia.nowak@techvolt.io) — Engineering Lead, runs sprint planning\n" +
                    "- **Ops team** (ops@techvolt.io) — on-call rotation, infra alerts\n" +
                    "- **Sales** (sales@techvolt.io) — CC on client pricing discussions\n\n" +
                    "Sprint cycle: 2-week sprints, planning on Wednesdays.\n" +
                    "Current sprint: Sprint 14.",
                UpdatedAt = "2026-02-10T09:00:00Z",
            },

            // ─── CreativeSpark knowledge ────────────────────────────────────────

            new KnowledgeEntry
            {
                Id = "kb-cs-001",
                Account = "adam@creativespark.co",
                Title = "CreativeSpark — services & pricing",
                Category = "product",
                Content = "## CreativeSpark Services\n\n" +
                    "### Services offered\n" +
                    "- Brand identity (logo, palette, typography)\n" +
                    "- Event branding (stage, digital, print)\n" +
                    "- Web design & microsites\n" +
                    "- Social media asset packs\n\n" +
                    "### Pricing ranges\n" +
                    "- Brand identity: €3,000–8,000\n" +
                    "- Event branding (full): €12,000–25,000\n" +
                    "- Microsite design: €5,000–10,000\n" +
                    "- Social media pack: €1,500–3,000\n\n" +
                    "### Capacity\n" +
                    "- Max 3 concurrent projects\n" +
                    "- Current load: 1 active (brand refresh wrapping up), 1 slot open",
                UpdatedAt = "2026-02-12T11:00:00Z",
            },
            new KnowledgeEntry
            {
                Id = "kb-cs-002",
                Account = "adam@creativespark.co",
                Title = "CreativeSpark — freelancers & vendors",
                Category = "vendors",
                Content = "## Freelancers\n\n" +
                    "### Luiza Kowalczyk (luiza.kowalczyk@freelance.design)\n" +
                    "- Role: Senior graphic designer\n" +
                    "- Rate: 320 zł/hour\n" +
                    "- Status: just delivered brand refresh assets, invoice pending (19 200 zł)\n" +
                    "- Availability: next week\n" +
                    "- Quality: excellent, long-term collaborator\n" +
                    "- Language: Polish\n\n" +
                    "### Tools & subscriptions\n" +
                    "- Figma Organization (5 seats) — $75/mo, renews March 1\n" +
                    "- Adobe Creative Cloud (2 seats) — $110/mo",
                UpdatedAt = "2026-02-17T16:30:00Z",
            },
            new KnowledgeEntry
            {
                Id = "kb-cs-003",
                Account = "adam@creativespark.co",
                Title = "CreativeSpark — team",
                Category = "team",
                Content = "## Team\n\n" +
                    "- **Patryk Wiśniewski** (patryk.wisniewski@creativespark.co) — Junior designer, handles web and social\n\n" +
                    "For larger projects, freelancers are brought in on a per-project basis.\n" +
                    "Creative direction always goes through Adam.",
                UpdatedAt = "2026-02-08T10:00:00Z",
            },
            new KnowledgeEntry
            {
                Id = "kb-cs-004",
                Account = "adam@creativespark.co",
                Title = "CreativeSpark — language & tone policy",
                Category = "communication",
                Content = "## Language Policy\n\n" +
                    "CreativeSpark operates on the Polish market.\n\n" +
                    "- **Default communication language: Polish.**\n" +
                    "- All replies to Polish-speaking contacts (team, freelancers, local clients) MUST be written in Polish.\n" +
                    "- Replies to international contacts who write in English should be in English.\n" +
                    "- Greeting for familiar contacts: \"Cześć\" or \"Hej\".\n" +
                    "- Greeting for new/formal contacts: \"Dzień dobry\".\n" +
                    "- Tone: casual and creative, matching the agency's brand personality.\n" +
                    "- Currency: PLN (zł) for local invoices and pricing discussions.",
                UpdatedAt = "2026-01-20T10:00:00Z",
            },
        };
    }
}
