# 04_05_review

Port of [`04_05_review`](https://github.com/i-am-alice/4th-devs/tree/main/04_05_review) to C# / .NET Framework 4.8.

Markdown review lab — an AI agent reviews documents block by block, adding inline comment suggestions. A web UI lets you browse, accept, reject, and revert review comments.

## Prerequisites

- .NET Framework 4.8 SDK
- OpenAI or OpenRouter API key

## Configuration

Copy `App.example.config` from the repository root into this project folder as `App.config` and fill in your API key:

```xml
<appSettings>
  <add key="AI_PROVIDER" value="openai" />
  <add key="OPENAI_API_KEY" value="sk-..." />
</appSettings>
```

## Running

```powershell
dotnet run --project src\04_05_review\04_05_review.csproj
```

The server starts on `http://localhost:4405/` and opens the review UI in your browser.

## Usage

1. Select a document and a review prompt from the dropdowns.
2. Choose review mode: **Paragraph** (block-by-block with concurrency) or **At Once** (whole document).
3. Click **Run Review** — the agent streams comments as NDJSON events.
4. Click highlighted text to view comments. Use **Accept** / **Reject** / **Revert** buttons.
5. Keyboard shortcuts: `j`/`k` navigate, `a` accept, `r` reject, `u` revert, `Esc` dismiss.

## Architecture

```
Program.cs            ─ Entry point, starts HTTP server
Core/ReviewServer.cs  ─ HttpListener server, API endpoints, inline HTML UI
Core/ReviewEngine.cs  ─ Review orchestration (paragraph/at_once modes)
Core/MarkdownParser.cs─ Markdown → blocks → markdown
Core/Store.cs         ─ Workspace I/O, frontmatter parsing, review persistence
Agent/AgentRunner.cs  ─ Tool-calling loop via Responses API
Tools/ReviewTools.cs  ─ add_comment tool definition and handler
Models/ReviewModels.cs─ Data models
```

## Workspace

```
workspace/
├── documents/    ─ Markdown documents to review
├── prompts/      ─ Review prompt definitions
├── reference/    ─ Reference files (e.g. sitemap for internal linking)
├── system/agents/─ Agent profiles
└── reviews/      ─ Saved review JSON files
```

## Differences from the JS original

- Svelte 5 frontend replaced with self-contained inline HTML + vanilla JS
- YAML frontmatter parsed with a lightweight built-in parser (no external lib)
- .NET `HttpListener` replaces Express/Node HTTP server
- `SemaphoreSlim` for paragraph-mode concurrency instead of JS `Promise.all` with limit
- Agent loop uses `JObject` for Responses API calls (same pattern as `03_05_apps`)
