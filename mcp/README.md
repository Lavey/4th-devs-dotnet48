# MCP Servers — .NET Framework 4.8 Port

![4th-devs logo](assets/logo.svg)

C# 7.3 / .NET Framework 4.8 ports of the MCP (Model Context Protocol) servers from
[i-am-alice/4th-devs](https://github.com/i-am-alice/4th-devs/tree/main/mcp).

---

## Projects

| Project | Transport | Description |
|---------|-----------|-------------|
| [FilesMcp](FilesMcp/README.md) | stdio | Sandboxed file-system access with read, search, write, and manage tools |
| [UploadThingMcp](UploadThingMcp/README.md) | HTTP | UploadThing file-hosting service with upload, list, and manage tools |

---

## Quick Start

### Prerequisites

- [.NET Framework 4.8](https://dotnet.microsoft.com/download/dotnet-framework/net48) (runtime)
- [.NET SDK 6+](https://dotnet.microsoft.com/download) (to build)

### Build Both Servers

```bash
dotnet build mcp/FilesMcp/FilesMcp.csproj
dotnet build mcp/UploadThingMcp/UploadThingMcp.csproj
```

### Run FilesMcp

```bash
# Set mount points via environment variable
set FS_ROOTS=C:\Users\YourUser\Documents

# Or copy and edit App.config.example → App.config in the FilesMcp project directory
cd mcp/FilesMcp
copy App.config.example App.config
# Edit App.config to set FS_ROOTS, then:
dotnet run
```

### Run UploadThingMcp

```bash
# Set API token via environment variable
set UPLOADTHING_TOKEN=your_token_here
set PORT=3000

# Or copy and edit App.config.example → App.config
cd mcp/UploadThingMcp
copy App.config.example App.config
# Edit App.config to add your UPLOADTHING_TOKEN, then:
dotnet run
```

---

## MCP Client Configuration

### Claude Desktop — FilesMcp (stdio)

Add to `%APPDATA%\Claude\claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "files": {
      "command": "C:\\path\\to\\mcp\\FilesMcp\\bin\\Release\\net48\\FilesMcp.exe",
      "env": {
        "FS_ROOTS": "C:\\Users\\YourUser\\Documents,C:\\Projects"
      }
    }
  }
}
```

### Claude Desktop — UploadThingMcp (HTTP)

The UploadThing MCP server uses HTTP transport. Start it as a background service:

```bash
set UPLOADTHING_TOKEN=your_token_here
FilesMcp\bin\Release\net48\UploadThingMcp.exe
```

Then configure your MCP client to connect to `http://localhost:3000/mcp`.

---

## Architecture

Both servers implement the [MCP specification](https://spec.modelcontextprotocol.io/) (protocol version `2024-11-05`):

- **JSON-RPC 2.0** message format
- **FilesMcp**: stdio transport — reads from `stdin`, writes to `stdout`, logs to `stderr`
- **UploadThingMcp**: HTTP transport — `POST /mcp` endpoint via `HttpListener`

---

## Tools Reference

### FilesMcp Tools

| Tool | Description |
|------|-------------|
| `fs_read` | Read files or explore directory tree |
| `fs_search` | Search by filename or content (literal/regex/fuzzy) |
| `fs_write` | Create/update files with optional line-range editing and dry-run |
| `fs_manage` | Delete, rename, move, copy, mkdir, stat |

### UploadThingMcp Tools

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
