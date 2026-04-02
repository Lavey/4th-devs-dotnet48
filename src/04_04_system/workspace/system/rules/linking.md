---
title: "Linking Rules"
description: "Conventions for wikilinks and external links"
status: evergreen
---

# Linking Rules

## Format

Use wikilinks with workspace-root relative paths:

```
[[craft/knowledge/AI/transformers]]
[[world/people/marcin-kowalski|Marcin]]
```

## When to link

- People mentioned by name → link to their note in `world/people/`
- Tools or services mentioned → link to `world/tools/`
- Projects referenced → link to `craft/projects/`
- Concepts that have their own knowledge note → link to `craft/knowledge/`

## When not to link

- Common words that happen to match a note title
- Circular link chains (A→B→C→A)
- Self-links

## External links

- Use full URLs: `https://example.com/page`
- Prefer permanent URLs over shortened ones
- Include link text: `[Source name](url)`
