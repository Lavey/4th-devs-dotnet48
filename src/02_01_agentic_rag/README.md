# Lesson 06 – Agentic RAG

Agentic Retrieval-Augmented Generation with multi-step search over local documents.

## Run

```
dotnet run --project src/Lesson06_AgenticRag
```

## Required setup

1. Copy `App.config.example` to `App.config` in this folder.
2. Set one Responses API key: `OPENAI_API_KEY` or `OPENROUTER_API_KEY`.
3. Drop your documents (`.txt`, `.md`, or any text format) into `workspace/docs/`.

## What it does

1. Loads documents from `workspace/docs/`
2. Runs an agentic loop — the model decides which files to list, search, and read
3. Iterates through multiple search angles (synonyms, related terms) before answering
4. Maintains conversation history for follow-up questions

## File structure

```
src/Lesson06_AgenticRag/
├── Program.cs            # Entry point — startup confirmation and REPL launch
├── AgentConfig.cs        # Model name, max steps, system instructions
├── Agent.cs              # Agent loop with conversation history
├── Repl.cs               # Interactive REPL (exit, clear)
├── Tools/
│   ├── ToolDefinitions.cs  # Tool schema definitions (list_files, search_files, read_file)
│   └── ToolExecutors.cs    # In-process file tool implementations
└── workspace/
    └── docs/             # Place your documents here
```

## REPL commands

| Command | Effect                           |
|---------|----------------------------------|
| `exit`  | Quit the application             |
| `clear` | Reset the conversation history   |

## Notes

- The agent always searches before reading to avoid loading irrelevant files.
- On startup the agent asks for confirmation because it can consume a noticeable number of tokens.
- Source repo: [i-am-alice/4th-devs → 02_01_agentic_rag](https://github.com/i-am-alice/4th-devs/tree/main/02_01_agentic_rag)
