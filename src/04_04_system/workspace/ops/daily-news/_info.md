---
title: "Daily News"
description: "Automated news digest workflow"
status: evergreen
trigger: "cron(0 7 * * *)"
---

# Daily News Workflow

Three-phase pipeline. Each phase is a separate agent delegation.

## Topics

- **ai** — artificial intelligence, machine learning, LLMs, agents
- **dev** — programming, frameworks, infrastructure, open source
- **startups** — funding, launches, acquisitions

## Sources per topic

| Topic    | Source                            |
|----------|-----------------------------------|
| ai       | [[world/sources/hacker-news-ai]]  |
| dev      | [[world/sources/hacker-news-dev]] |
| startups | [[world/sources/techcrunch]]      |

## Phases

1. **[[ops/daily-news/01-research]]** → Agent: Ellie. Search + write per-topic notes.
2. **[[ops/daily-news/02-assemble]]** → Agent: Tony. Merge notes into HTML digest.
3. **[[ops/daily-news/03-deliver]]** → Agent: Rose. Verify + send email.

## Output structure

```
ops/daily-news/{yyyy-mm-dd}/
├── ai.md          ← Ellie's research
├── dev.md         ← Ellie's research
├── startups.md    ← Ellie's research
├── digest.html    ← Tony's assembled output
└── status.md      ← Rose's delivery log
```
