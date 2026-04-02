---
title: "Agents"
description: "Agent profiles and coordination rules"
status: evergreen
---

# Agents

Each agent has its own `.md` file with frontmatter (model, tools, description) and a system prompt body.

## Conventions

- Agents must respect `access.write` in note frontmatter.
- Agents must not modify files in `me/` unless explicitly asked.
- When creating notes, agents must follow templates from [[system/templates/_index]].
- When linking, agents must follow rules from [[system/rules/linking]].
