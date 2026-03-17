# FilesMcp

A stdio-based [Model Context Protocol (MCP)](https://modelcontextprotocol.io/) server written in C# targeting .NET Framework 4.8.  
It exposes four tools that let an AI assistant read, search, write, and manage files within a sandboxed set of mount points.

---

## Prerequisites

| Requirement | Notes |
|---|---|
| .NET Framework 4.8 | Runtime for the compiled executable |
| .NET SDK 6+ (or MSBuild) | Only needed to build from source |

---

## Configuration

Copy `App.config.example` to `App.config` (in the project directory) and edit the values:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<configuration>
  <appSettings>
    <!-- One or more mount points, comma-separated -->
    <!-- Format: "alias:/absolute/path" or just "/absolute/path" -->
    <add key="FS_ROOTS" value="docs:C:\Users\YourUser\Documents,code:C:\Projects" />

    <!-- Log level: debug | info | warning | error  (default: info) -->
    <add key="LOG_LEVEL" value="info" />

    <!-- Maximum file size the server will read, in bytes (default: 1048576 = 1 MB) -->
    <add key="MAX_FILE_SIZE" value="1048576" />
  </appSettings>
</configuration>
```

All settings can also be provided as **environment variables** (they take precedence over `App.config`):

| Variable | Description |
|---|---|
| `FS_ROOTS` | Comma-separated mount points |
| `FS_ROOT` | Single mount point (backward compat) |
| `LOG_LEVEL` | Logging verbosity |
| `MAX_FILE_SIZE` | Max readable file size in bytes |

If neither `FS_ROOTS` nor `FS_ROOT` is set, the server defaults to the **current working directory**.

---

## Build

```bash
dotnet build mcp/FilesMcp/FilesMcp.csproj -c Release
```

The output executable will be placed in `mcp/FilesMcp/bin/Release/net48/FilesMcp.exe`.

---

## Run

```bash
# Windows
mcp\FilesMcp\bin\Release\net48\FilesMcp.exe

# Linux / macOS (via Mono or Wine)
mono mcp/FilesMcp/bin/Release/net48/FilesMcp.exe
```

The server communicates over **stdin / stdout** using newline-delimited JSON-RPC 2.0.  
Log output goes to **stderr** and does not interfere with the protocol.

---

## MCP Client Configuration

### Claude Desktop (`claude_desktop_config.json`)

```json
{
  "mcpServers": {
    "files": {
      "command": "C:\\path\\to\\FilesMcp.exe",
      "env": {
        "FS_ROOTS": "docs:C:\\Users\\YourUser\\Documents,code:C:\\Projects",
        "LOG_LEVEL": "info"
      }
    }
  }
}
```

### Generic stdio MCP client

```json
{
  "command": "FilesMcp.exe",
  "args": [],
  "env": {
    "FS_ROOTS": "/home/user/projects"
  }
}
```

---

## Tools Reference

### `fs_read` — Read files or explore directories

| Parameter | Type | Required | Description |
|---|---|---|---|
| `path` | string | ✓ | File or directory path |
| `depth` | integer | | Max directory traversal depth (default: 2) |
| `lines` | string | | Line range, e.g. `"10-20"` or `"5"` |
| `glob` | string | | Glob filter for directory listing |
| `exclude` | string | | Glob pattern to exclude from listing |
| `respectIgnore` | boolean | | Respect `.gitignore` files (default: true) |

**Directory listing** returns a visual tree with file sizes.  
**File reading** returns line-numbered content with a SHA256 checksum header.

---

### `fs_search` — Search by filename or content

| Parameter | Type | Required | Description |
|---|---|---|---|
| `path` | string | ✓ | Directory to search in |
| `query` | string | ✓ | Search query |
| `target` | string | | `"filename"` \| `"content"` \| `"all"` (default: `"all"`) |
| `patternMode` | string | | `"literal"` \| `"regex"` \| `"fuzzy"` (default: `"literal"`) |
| `contextLines` | integer | | Context lines around content matches (default: 2) |
| `maxResults` | integer | | Maximum results to return (default: 50) |

---

### `fs_write` — Create or edit files

| Parameter | Type | Required | Description |
|---|---|---|---|
| `path` | string | ✓ | File path |
| `operation` | string | ✓ | `"create"` or `"update"` |
| `content` | string | | File content (or new lines for line edit) |
| `lines` | string | | Line range for line-based edit, e.g. `"10-20"` |
| `action` | string | | `"replace"` \| `"insert_before"` \| `"insert_after"` \| `"delete_lines"` |
| `checksum` | string | | Expected SHA256 of current file (safety check) |
| `dryRun` | boolean | | Return unified diff without writing (default: false) |

**create** — creates a new file; fails if the file already exists.  
**update** without `lines` — replaces the entire file.  
**update** with `lines` + `action` — performs a surgical line-range edit.

---

### `fs_manage` — File system management

| Parameter | Type | Required | Description |
|---|---|---|---|
| `operation` | string | ✓ | `"delete"` \| `"rename"` \| `"move"` \| `"copy"` \| `"mkdir"` \| `"stat"` |
| `path` | string | ✓ | Source path |
| `target` | string | | Target path (rename / move / copy) |
| `recursive` | boolean | | Recursive delete (default: false) |
| `force` | boolean | | Overwrite existing target (default: false) |

---

## Security

All file operations are sandboxed to the configured mount points.  
Any path that resolves outside the mounts is rejected with an **Access denied** error.  
`.gitignore` patterns are optionally respected to skip ignored files during directory listing and search.
