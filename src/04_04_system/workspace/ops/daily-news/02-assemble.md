---
title: "Phase 2: Assemble"
agent: tony
---

# Phase 2 — Assemble HTML Digest

## Agent

Tony (writer/assembler).

## Steps

1. List the `ops/daily-news/{date}/` directory to find topic markdown files.
2. Read each topic file.
3. Assemble a single HTML digest file: `ops/daily-news/{date}/digest.html`.

## HTML template

```html
<!DOCTYPE html>
<html><head><meta charset="utf-8"><title>Daily News — {date}</title>
<style>body{font-family:system-ui;max-width:700px;margin:2em auto;padding:0 1em}
h1{border-bottom:2px solid #333}h2{color:#555;margin-top:1.5em}
ul{padding-left:1.2em}li{margin-bottom:0.5em}a{color:#0066cc}</style>
</head><body>
<h1>Daily News — {date}</h1>
{sections}
</body></html>
```

Each section: `<h2>{Topic}</h2><ul>{items as <li>}</ul>`.

## Rules

- Do NOT invent content. Only use what's in the topic files.
- Preserve all URLs exactly as they appear in source notes.
- If no topic files exist, write minimal HTML with "No news today."
- Valid HTML only — no markdown inside the HTML file.
