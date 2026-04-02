---
title: "Phase 3: Deliver"
agent: rose
---

# Phase 3 — Verify & Send

## Agent

Rose (delivery specialist).

## Steps

1. Read `ops/daily-news/{date}/digest.html`.
2. Read recipient email from `me/preferences.md` (the `Email` field).
3. Verify the digest:
   - Non-empty body
   - No placeholder text (e.g. `{date}`, `{sections}`)
   - Contains at least one `<h2>` section
4. If verification passes: send the digest using `send_email` tool.
5. Write delivery status to `ops/daily-news/{date}/status.md`:

```yaml
---
title: "Delivery Status"
delivered: true
recipient: {email}
timestamp: {ISO timestamp}
issues: []
---
```

## Rules

- Never use `dryRun`. Always send when verification passes.
- If verification fails, write status with `delivered: false` and list issues.
- Do NOT modify the digest — only read and verify it.
