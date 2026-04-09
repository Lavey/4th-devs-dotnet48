# 05_04_ui — Chat Application UI

Port of **`i-am-alice/4th-devs/05_04_ui`** (Svelte 5 → .NET 4.8 + HttpListener + static HTML).

## Overview

Chat application frontend with thread management, agent selection, streaming AI responses, and memory display. Connects to the `05_04_api` backend.

## Prerequisites

- The `05_04_api` server must be running (default: http://127.0.0.1:3000)
- Run database migrations and seed before first use (see 05_04_api README)

## Configuration

Copy `App.config.example` to `App.config` and set:
- `UI_PORT` — UI server port (default: 5173)
- `API_BASE_URL` — API backend URL (default: http://127.0.0.1:3000/v1)

## Running

```powershell
# Terminal 1: Start the API server
dotnet run --project src/05_04_api/05_04_api.csproj

# Terminal 2: Start the UI
dotnet run --project src/05_04_ui/05_04_ui.csproj
```

The UI opens automatically at http://localhost:5173.

## Features

- Thread-based conversations (create, switch, delete)
- Agent selection from available agents
- Streaming AI responses via SSE
- Thread memory display (observations & reflections)
- Dark theme UI
- Markdown rendering in messages
