# MCP — Unified MCP Server (.NET Framework 4.8)

C# 7.3 / .NET Framework 4.8 port of the MCP (Model Context Protocol) servers from
[i-am-alice/4th-devs](https://github.com/i-am-alice/4th-devs/tree/main/mcp).

This single project combines the two MCP servers:

| Mode | Transport | Description | Source |
|------|-----------|-------------|--------|
| `files` | stdio | Sandboxed file-system access with read, search, write, and manage tools | [mcp/files-mcp](https://github.com/i-am-alice/4th-devs/tree/main/mcp/files-mcp) |
| `uploadthing` | HTTP | UploadThing file-hosting service with upload, list, and manage tools | [mcp/uploadthing-mcp](https://github.com/i-am-alice/4th-devs/tree/main/mcp/uploadthing-mcp) |

---

## Quick Start

### Prerequisites

- [.NET Framework 4.8](https://dotnet.microsoft.com/download/dotnet-framework/net48) (runtime)
- [.NET SDK 6+](https://dotnet.microsoft.com/download) (to build)

### Build

```bash
dotnet build mcp/MCP/MCP.csproj
```

### Configure

```bash
cd mcp/MCP
copy App.config.example App.config
# Edit App.config to set MCP_MODE and the relevant settings
```

### Run — FilesMcp mode (stdio)

```bash
# via CLI argument
MCP.exe files

# via environment variable
set MCP_MODE=files
MCP.exe

# via App.config (MCP_MODE = files)
MCP.exe
```

### Run — UploadThingMcp mode (HTTP)

```bash
# via CLI argument
set UPLOADTHING_TOKEN=your_token_here
MCP.exe uploadthing

# via environment variable
set MCP_MODE=uploadthing
set UPLOADTHING_TOKEN=your_token_here
MCP.exe
```

The HTTP server prints:
```
MCP server starting in [uploadthing] mode
Listening on http://127.0.0.1:3000/
Press Ctrl+C to stop.
```

---

## MCP Client Configuration

### Claude Desktop — files mode (stdio)

Add to `%APPDATA%\Claude\claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "files": {
      "command": "C:\\path\\to\\mcp\\MCP\\bin\\Release\\net48\\MCP.exe",
      "args": ["files"],
      "env": {
        "FS_ROOTS": "C:\\Users\\YourUser\\Documents,C:\\Projects"
      }
    }
  }
}
```

### Claude Desktop — uploadthing mode (HTTP)

Start the server as a background service:

```bash
set UPLOADTHING_TOKEN=your_token_here
MCP.exe uploadthing
```

Then configure your MCP client:

```json
{
  "mcpServers": {
    "uploadthing": {
      "type": "http",
      "url": "http://127.0.0.1:3000/mcp"
    }
  }
}
```

### Use both servers simultaneously

Run two separate instances — one per mode:

```bash
# Terminal 1 — files server (stdio, managed by MCP client)
# Terminal 2 — uploadthing server (HTTP)
set UPLOADTHING_TOKEN=your_token_here
MCP.exe uploadthing
```

```json
{
  "mcpServers": {
    "files": {
      "command": "C:\\path\\to\\MCP.exe",
      "args": ["files"],
      "env": { "FS_ROOTS": "C:\\Projects" }
    },
    "uploadthing": {
      "type": "http",
      "url": "http://127.0.0.1:3000/mcp"
    }
  }
}
```

---

## Architecture

The server implements the [MCP specification](https://spec.modelcontextprotocol.io/) (protocol version `2024-11-05`):

- **JSON-RPC 2.0** message format
- **files mode**: stdio transport — reads from `stdin`, writes to `stdout`, logs to `stderr`
- **uploadthing mode**: HTTP transport — `POST /mcp` endpoint via `HttpListener`

---

## Tools Reference

### files mode tools

| Tool | Description |
|------|-------------|
| `fs_read` | Read files or explore directory tree |
| `fs_search` | Search by filename or content (literal/regex/fuzzy) |
| `fs_write` | Create/update files with optional line-range editing and dry-run |
| `fs_manage` | Delete, rename, move, copy, mkdir, stat |

### uploadthing mode tools

| Tool | Description |
|------|-------------|
| `upload_files` | Upload files from URLs or base64 content |
| `list_files` | List uploaded files with metadata |
| `manage_files` | Rename or delete files |

---

## Source Reference

- Original TypeScript implementations: [i-am-alice/4th-devs/mcp](https://github.com/i-am-alice/4th-devs/tree/main/mcp)
- MCP Specification: [spec.modelcontextprotocol.io](https://spec.modelcontextprotocol.io/)
- UploadThing API: [docs.uploadthing.com](https://docs.uploadthing.com/api-reference)
