# 05_04_api — Multi-Agent API Server

.NET Framework 4.8 port of the [i-am-alice/4th-devs](https://github.com/i-am-alice/4th-devs) `05_04_api` TypeScript project.

A multi-agent API server with SQLite persistence, multiple AI providers, threads, sessions, runs, memory, event streaming, and authentication.

## Setup

1. Copy `App.config.example` to `App.config` and fill in your API keys.
2. Build: `dotnet build`
3. Run: `dotnet run`

## Default Credentials

- **Email:** admin@localhost
- **Password:** password
- **API Key:** `sk_local_dev_key`

## API Endpoints

### System
| Method | Path | Description |
|--------|------|-------------|
| GET | `/v1/system/health` | Health check |
| GET | `/v1/system/models` | Available AI models |

### Authentication
| Method | Path | Description |
|--------|------|-------------|
| POST | `/v1/auth/login` | Email/password login |
| POST | `/v1/auth/logout` | Session invalidation |
| GET | `/v1/auth/session` | Get current session |

### Account
| Method | Path | Description |
|--------|------|-------------|
| GET | `/v1/account` | Get account info |

### Agents
| Method | Path | Description |
|--------|------|-------------|
| GET | `/v1/agents` | List agents |
| POST | `/v1/agents` | Create agent |
| GET | `/v1/agents/:id` | Get agent |
| PUT | `/v1/agents/:id` | Update agent |
| DELETE | `/v1/agents/:id` | Delete agent |

### Sessions
| Method | Path | Description |
|--------|------|-------------|
| POST | `/v1/sessions` | Create work session |
| POST | `/v1/sessions/bootstrap` | Bootstrap session with optional first message |

### Threads
| Method | Path | Description |
|--------|------|-------------|
| GET | `/v1/threads` | List threads (requires session_id query param) |
| POST | `/v1/threads` | Create thread |
| POST | `/v1/threads/:id/interact` | Start AI interaction |
| POST | `/v1/threads/:id/messages` | Post message |
| GET | `/v1/threads/:id/messages` | Get messages |
| POST | `/v1/threads/:id/memory` | Update memory |
| GET | `/v1/threads/:id/memory` | Get memory |

### Runs
| Method | Path | Description |
|--------|------|-------------|
| POST | `/v1/runs/:id/execute` | Execute run |
| POST | `/v1/runs/:id/resume` | Resume waiting run |
| POST | `/v1/runs/:id/cancel` | Cancel run |

### Events
| Method | Path | Description |
|--------|------|-------------|
| GET | `/v1/events/stream` | SSE event stream |

### Files
| Method | Path | Description |
|--------|------|-------------|
| GET | `/v1/files` | List files |
| GET | `/v1/files/:id/content` | Download file |

## Authentication

The server supports multiple authentication modes:

- **`dev_headers`** (default): Trusts `X-Account-Id` and `X-Tenant-Id` headers. Falls back to the seed admin account.
- **`api_key`**: Requires `Authorization: Bearer sk_local_...` header.
- **Session auth**: Cookie-based auth sessions via `/v1/auth/login`.

## Architecture

- **HttpListener** for HTTP serving (no ASP.NET dependency)
- **SQLite** for persistence (System.Data.SQLite.Core)
- **Newtonsoft.Json** for serialization
- **Common project** for AI provider configuration and API client
- **Prefixed UUIDs** for all entity IDs (acc_, ten_, agt_, etc.)
