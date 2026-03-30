# Workspace

This is the awareness agent's workspace. It stores memory, context, and conversation history.

## Structure

- `profile/user/` – User identity, preferences, and important dates
- `profile/agent/` – Agent persona and character
- `environment/` – Current environment context
- `memory/episodic/` – Specific past events and experiences
- `memory/factual/` – Facts and reference information
- `memory/procedural/` – Procedures, routines, and how-tos
- `notes/scout/` – Notes saved by the scout sub-agent
- `system/chat/` – Conversation history (JSONL)
- `system/awareness/` – Awareness state tracking
- `traces/` – Debug traces
