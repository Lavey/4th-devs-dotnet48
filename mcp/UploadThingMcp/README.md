# UploadThing MCP Server

An HTTP-based [Model Context Protocol (MCP)](https://modelcontextprotocol.io/) server that exposes [UploadThing](https://uploadthing.com/) file management as MCP tools.

MCP clients (Claude Desktop, Cursor, etc.) connect via HTTP `POST /mcp` and can upload, list, rename, and delete files in your UploadThing storage.

## Prerequisites

- [.NET Framework 4.8](https://dotnet.microsoft.com/download/dotnet-framework/net48)
- An [UploadThing](https://uploadthing.com/) account with an API key

## UploadThing API Key

1. Sign in to [https://uploadthing.com/dashboard](https://uploadthing.com/dashboard)
2. Navigate to **API Keys**
3. Copy your secret token

## Configuration

Copy `App.config.example` to `App.config` and fill in your values:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<configuration>
  <appSettings>
    <add key="UPLOADTHING_TOKEN" value="your-secret-token-here" />
    <add key="PORT"  value="3000" />
    <add key="HOST"  value="127.0.0.1" />
  </appSettings>
</configuration>
```

You can also configure via environment variables (they take precedence over `App.config`):

| Variable            | Default     | Description                        |
|---------------------|-------------|------------------------------------|
| `UPLOADTHING_TOKEN` | *(required)*| UploadThing API secret token       |
| `PORT`              | `3000`      | HTTP port to listen on             |
| `HOST`              | `127.0.0.1` | Bind address                       |

## Build & Run

```bash
# Build
dotnet build mcp/UploadThingMcp/UploadThingMcp.csproj

# Run (after copying and editing App.config)
dotnet run --project mcp/UploadThingMcp/UploadThingMcp.csproj
```

The server prints:
```
UploadThing MCP Server
Listening on http://127.0.0.1:3000/
Press Ctrl+C to stop.
```

Open `http://127.0.0.1:3000/` in a browser to verify it is running.

## MCP Client Configuration

Because this server uses **HTTP** (not stdio), configure your MCP client with the HTTP transport.

### Claude Desktop (`claude_desktop_config.json`)

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

### Cursor / VS Code (`.cursor/mcp.json` or `mcp.json`)

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

## API Endpoints

| Method | Path   | Description              |
|--------|--------|--------------------------|
| GET    | `/`    | Server status page       |
| POST   | `/mcp` | MCP JSON-RPC endpoint    |
| OPTIONS| `/mcp` | CORS preflight           |

## Tools Reference

### `upload_files`

Upload one or more files to UploadThing from URLs or base64-encoded content.

**Input:**
```json
{
  "files": [
    { "name": "photo.jpg", "url": "https://example.com/photo.jpg" },
    { "name": "doc.pdf",   "base64": "<base64-encoded content>" }
  ]
}
```

**Returns:** list of uploaded files with `key`, `url`, `name`, and `size`.

---

### `list_files`

List files stored in UploadThing with optional pagination.

**Input:**
```json
{ "limit": 50, "offset": 0 }
```

**Returns:** list of files with `key`, `name`, `url`, `size`, `status`, and `uploadedAt`.

---

### `manage_files`

Rename or delete files in UploadThing.

**Rename a file:**
```json
{ "action": "rename", "fileKeys": ["file_key_here"], "newName": "new-name.jpg" }
```

**Delete one or more files:**
```json
{ "action": "delete", "fileKeys": ["key1", "key2"] }
```

**Returns:** success or error message.
