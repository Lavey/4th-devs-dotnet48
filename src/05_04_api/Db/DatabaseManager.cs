using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using FourthDevs.MultiAgentApi.Models;
using AgentModel = FourthDevs.MultiAgentApi.Models.Agent;

namespace FourthDevs.MultiAgentApi.Db
{
    /// <summary>
    /// SQLite persistence layer for the multi-agent API.
    /// Creates all tables, handles CRUD, and manages the connection.
    /// </summary>
    internal sealed class DatabaseManager : IDisposable
    {
        private readonly SQLiteConnection _conn;

        internal DatabaseManager(string databasePath)
        {
            string dir = Path.GetDirectoryName(databasePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            _conn = new SQLiteConnection("Data Source=" + databasePath + ";Version=3;");
            _conn.Open();

            // Enable WAL mode and foreign keys
            ExecuteNonQuery("PRAGMA journal_mode=WAL;");
            ExecuteNonQuery("PRAGMA foreign_keys=ON;");

            EnsureSchema();
        }

        // ────────────────────────────────────────────────────────────
        // Schema
        // ────────────────────────────────────────────────────────────

        private void EnsureSchema()
        {
            string sql = @"
CREATE TABLE IF NOT EXISTS accounts (
    id            TEXT PRIMARY KEY NOT NULL,
    display_name  TEXT NOT NULL DEFAULT '',
    email         TEXT NOT NULL,
    avatar_url    TEXT,
    status        TEXT NOT NULL DEFAULT 'active',
    created_at    TEXT NOT NULL,
    updated_at    TEXT NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS idx_accounts_email ON accounts (email);

CREATE TABLE IF NOT EXISTS api_keys (
    id            TEXT PRIMARY KEY NOT NULL,
    account_id    TEXT NOT NULL REFERENCES accounts(id),
    tenant_id     TEXT NOT NULL,
    key_prefix    TEXT NOT NULL,
    key_hash      TEXT NOT NULL,
    label         TEXT NOT NULL DEFAULT '',
    scopes        TEXT NOT NULL DEFAULT '[]',
    expires_at    TEXT,
    last_used_at  TEXT,
    revoked_at    TEXT,
    created_at    TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_api_keys_account ON api_keys (account_id);
CREATE INDEX IF NOT EXISTS idx_api_keys_hash    ON api_keys (key_hash);

CREATE TABLE IF NOT EXISTS auth_sessions (
    id            TEXT PRIMARY KEY NOT NULL,
    account_id    TEXT NOT NULL REFERENCES accounts(id),
    tenant_id     TEXT NOT NULL,
    token_hash    TEXT NOT NULL,
    ip_address    TEXT,
    user_agent    TEXT,
    expires_at    TEXT NOT NULL,
    created_at    TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_auth_sessions_token ON auth_sessions (token_hash);

CREATE TABLE IF NOT EXISTS password_credentials (
    account_id    TEXT PRIMARY KEY NOT NULL REFERENCES accounts(id),
    password_hash TEXT NOT NULL,
    updated_at    TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS tenants (
    id            TEXT PRIMARY KEY NOT NULL,
    slug          TEXT NOT NULL,
    display_name  TEXT NOT NULL DEFAULT '',
    status        TEXT NOT NULL DEFAULT 'active',
    created_at    TEXT NOT NULL,
    updated_at    TEXT NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS idx_tenants_slug ON tenants (slug);

CREATE TABLE IF NOT EXISTS tenant_memberships (
    id            TEXT PRIMARY KEY NOT NULL,
    tenant_id     TEXT NOT NULL REFERENCES tenants(id),
    account_id    TEXT NOT NULL REFERENCES accounts(id),
    role          TEXT NOT NULL DEFAULT 'member',
    created_at    TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_memberships_tenant  ON tenant_memberships (tenant_id);
CREATE INDEX IF NOT EXISTS idx_memberships_account ON tenant_memberships (account_id);

CREATE TABLE IF NOT EXISTS agents (
    id            TEXT PRIMARY KEY NOT NULL,
    tenant_id     TEXT NOT NULL REFERENCES tenants(id),
    slug          TEXT NOT NULL,
    display_name  TEXT NOT NULL DEFAULT '',
    description   TEXT,
    status        TEXT NOT NULL DEFAULT 'active',
    created_by    TEXT REFERENCES accounts(id),
    created_at    TEXT NOT NULL,
    updated_at    TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_agents_tenant ON agents (tenant_id);

CREATE TABLE IF NOT EXISTS agent_revisions (
    id            TEXT PRIMARY KEY NOT NULL,
    agent_id      TEXT NOT NULL REFERENCES agents(id),
    version       INTEGER NOT NULL DEFAULT 1,
    frontmatter   TEXT,
    instructions  TEXT,
    model         TEXT NOT NULL DEFAULT 'gpt-4.1-mini',
    temperature   REAL NOT NULL DEFAULT 0.7,
    max_tokens    INTEGER NOT NULL DEFAULT 4096,
    tool_policy   TEXT NOT NULL DEFAULT 'auto',
    memory_policy TEXT NOT NULL DEFAULT 'none',
    is_active     INTEGER NOT NULL DEFAULT 1,
    created_by    TEXT REFERENCES accounts(id),
    created_at    TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_revisions_agent ON agent_revisions (agent_id);

CREATE TABLE IF NOT EXISTS agent_subagent_links (
    parent_agent_id TEXT NOT NULL REFERENCES agents(id),
    child_agent_id  TEXT NOT NULL REFERENCES agents(id),
    alias           TEXT,
    created_at      TEXT NOT NULL,
    PRIMARY KEY (parent_agent_id, child_agent_id)
);

CREATE TABLE IF NOT EXISTS workspaces (
    id            TEXT PRIMARY KEY NOT NULL,
    tenant_id     TEXT NOT NULL REFERENCES tenants(id),
    slug          TEXT NOT NULL,
    display_name  TEXT NOT NULL DEFAULT '',
    status        TEXT NOT NULL DEFAULT 'active',
    created_at    TEXT NOT NULL,
    updated_at    TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_workspaces_tenant ON workspaces (tenant_id);

CREATE TABLE IF NOT EXISTS work_sessions (
    id            TEXT PRIMARY KEY NOT NULL,
    tenant_id     TEXT NOT NULL REFERENCES tenants(id),
    workspace_id  TEXT REFERENCES workspaces(id),
    agent_id      TEXT REFERENCES agents(id),
    title         TEXT,
    status        TEXT NOT NULL DEFAULT 'active',
    created_by    TEXT REFERENCES accounts(id),
    created_at    TEXT NOT NULL,
    updated_at    TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_work_sessions_tenant ON work_sessions (tenant_id);

CREATE TABLE IF NOT EXISTS session_threads (
    id            TEXT PRIMARY KEY NOT NULL,
    session_id    TEXT NOT NULL REFERENCES work_sessions(id),
    agent_id      TEXT REFERENCES agents(id),
    title         TEXT,
    status        TEXT NOT NULL DEFAULT 'active',
    created_at    TEXT NOT NULL,
    updated_at    TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_threads_session ON session_threads (session_id);

CREATE TABLE IF NOT EXISTS session_messages (
    id            TEXT PRIMARY KEY NOT NULL,
    thread_id     TEXT NOT NULL REFERENCES session_threads(id),
    role          TEXT NOT NULL,
    content       TEXT,
    metadata      TEXT,
    created_by    TEXT,
    created_at    TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_messages_thread ON session_messages (thread_id);

CREATE TABLE IF NOT EXISTS jobs (
    id            TEXT PRIMARY KEY NOT NULL,
    tenant_id     TEXT NOT NULL,
    session_id    TEXT,
    thread_id     TEXT,
    run_id        TEXT,
    type          TEXT NOT NULL,
    status        TEXT NOT NULL DEFAULT 'pending',
    payload       TEXT,
    result        TEXT,
    error         TEXT,
    created_at    TEXT NOT NULL,
    started_at    TEXT,
    completed_at  TEXT
);
CREATE INDEX IF NOT EXISTS idx_jobs_status ON jobs (status);

CREATE TABLE IF NOT EXISTS runs (
    id            TEXT PRIMARY KEY NOT NULL,
    tenant_id     TEXT NOT NULL,
    session_id    TEXT,
    thread_id     TEXT REFERENCES session_threads(id),
    agent_id      TEXT REFERENCES agents(id),
    revision_id   TEXT REFERENCES agent_revisions(id),
    status        TEXT NOT NULL DEFAULT 'pending',
    turn_count    INTEGER NOT NULL DEFAULT 0,
    max_turns     INTEGER NOT NULL DEFAULT 10,
    error         TEXT,
    created_at    TEXT NOT NULL,
    started_at    TEXT,
    completed_at  TEXT
);
CREATE INDEX IF NOT EXISTS idx_runs_thread ON runs (thread_id);
CREATE INDEX IF NOT EXISTS idx_runs_status ON runs (status);

CREATE TABLE IF NOT EXISTS items (
    id            TEXT PRIMARY KEY NOT NULL,
    run_id        TEXT REFERENCES runs(id),
    thread_id     TEXT REFERENCES session_threads(id),
    sequence      INTEGER NOT NULL DEFAULT 0,
    type          TEXT NOT NULL,
    role          TEXT,
    content       TEXT,
    call_id       TEXT,
    tool_name     TEXT,
    arguments     TEXT,
    output        TEXT,
    is_error      INTEGER NOT NULL DEFAULT 0,
    created_at    TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_items_run ON items (run_id);

CREATE TABLE IF NOT EXISTS tool_executions (
    id            TEXT PRIMARY KEY NOT NULL,
    run_id        TEXT REFERENCES runs(id),
    call_id       TEXT,
    tool_name     TEXT NOT NULL,
    arguments     TEXT,
    output        TEXT,
    status        TEXT NOT NULL DEFAULT 'pending',
    duration_ms   INTEGER NOT NULL DEFAULT 0,
    created_at    TEXT NOT NULL,
    completed_at  TEXT
);
CREATE INDEX IF NOT EXISTS idx_tool_execs_run ON tool_executions (run_id);

CREATE TABLE IF NOT EXISTS waits (
    id            TEXT PRIMARY KEY NOT NULL,
    run_id        TEXT REFERENCES runs(id),
    type          TEXT NOT NULL DEFAULT 'user_input',
    prompt        TEXT,
    response      TEXT,
    status        TEXT NOT NULL DEFAULT 'pending',
    created_at    TEXT NOT NULL,
    resolved_at   TEXT
);
CREATE INDEX IF NOT EXISTS idx_waits_run ON waits (run_id);

CREATE TABLE IF NOT EXISTS domain_events (
    id              TEXT PRIMARY KEY NOT NULL,
    tenant_id       TEXT NOT NULL,
    aggregate_type  TEXT NOT NULL,
    aggregate_id    TEXT NOT NULL,
    event_type      TEXT NOT NULL,
    payload         TEXT,
    created_at      TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_events_aggregate ON domain_events (aggregate_type, aggregate_id);
CREATE INDEX IF NOT EXISTS idx_events_type      ON domain_events (event_type);

CREATE TABLE IF NOT EXISTS event_outbox (
    id            TEXT PRIMARY KEY NOT NULL,
    event_id      TEXT NOT NULL REFERENCES domain_events(id),
    delivered     INTEGER NOT NULL DEFAULT 0,
    delivered_at  TEXT
);
CREATE INDEX IF NOT EXISTS idx_outbox_delivered ON event_outbox (delivered);

CREATE TABLE IF NOT EXISTS memory_records (
    id            TEXT PRIMARY KEY NOT NULL,
    tenant_id     TEXT NOT NULL,
    agent_id      TEXT,
    thread_id     TEXT,
    session_id    TEXT,
    run_id        TEXT,
    scope         TEXT NOT NULL DEFAULT 'thread_shared',
    type          TEXT NOT NULL DEFAULT 'observation',
    content       TEXT NOT NULL,
    keywords      TEXT,
    confidence    REAL NOT NULL DEFAULT 1.0,
    created_at    TEXT NOT NULL,
    updated_at    TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_memory_agent   ON memory_records (agent_id);
CREATE INDEX IF NOT EXISTS idx_memory_thread  ON memory_records (thread_id);
CREATE INDEX IF NOT EXISTS idx_memory_scope   ON memory_records (scope);

CREATE TABLE IF NOT EXISTS memory_record_sources (
    id                TEXT PRIMARY KEY NOT NULL,
    memory_record_id  TEXT NOT NULL REFERENCES memory_records(id),
    message_id        TEXT,
    created_at        TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_mem_sources_record ON memory_record_sources (memory_record_id);

CREATE TABLE IF NOT EXISTS files (
    id            TEXT PRIMARY KEY NOT NULL,
    tenant_id     TEXT NOT NULL,
    filename      TEXT NOT NULL,
    mime_type     TEXT NOT NULL DEFAULT 'application/octet-stream',
    size_bytes    INTEGER NOT NULL DEFAULT 0,
    storage_path  TEXT NOT NULL,
    created_by    TEXT,
    created_at    TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_files_tenant ON files (tenant_id);

CREATE TABLE IF NOT EXISTS uploads (
    id            TEXT PRIMARY KEY NOT NULL,
    file_id       TEXT NOT NULL REFERENCES files(id),
    tenant_id     TEXT NOT NULL,
    status        TEXT NOT NULL DEFAULT 'pending',
    created_at    TEXT NOT NULL,
    completed_at  TEXT
);

CREATE TABLE IF NOT EXISTS file_links (
    id            TEXT PRIMARY KEY NOT NULL,
    file_id       TEXT NOT NULL REFERENCES files(id),
    linked_type   TEXT NOT NULL,
    linked_id     TEXT NOT NULL,
    created_at    TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_file_links_file ON file_links (file_id);

CREATE TABLE IF NOT EXISTS http_idempotency_keys (
    id            TEXT PRIMARY KEY NOT NULL,
    key_value     TEXT NOT NULL,
    response_code INTEGER,
    response_body TEXT,
    created_at    TEXT NOT NULL,
    expires_at    TEXT NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS idx_idempotency_key ON http_idempotency_keys (key_value);

CREATE TABLE IF NOT EXISTS tool_access_profiles (
    id            TEXT PRIMARY KEY NOT NULL,
    agent_id      TEXT NOT NULL REFERENCES agents(id),
    tool_name     TEXT NOT NULL,
    allowed       INTEGER NOT NULL DEFAULT 1,
    config        TEXT,
    created_at    TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_tool_profiles_agent ON tool_access_profiles (agent_id);

CREATE TABLE IF NOT EXISTS mcp_servers (
    id            TEXT PRIMARY KEY NOT NULL,
    tenant_id     TEXT NOT NULL,
    name          TEXT NOT NULL,
    url           TEXT NOT NULL,
    status        TEXT NOT NULL DEFAULT 'active',
    config        TEXT,
    created_at    TEXT NOT NULL,
    updated_at    TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS mcp_server_secrets (
    id            TEXT PRIMARY KEY NOT NULL,
    mcp_server_id TEXT NOT NULL REFERENCES mcp_servers(id),
    key           TEXT NOT NULL,
    value         TEXT NOT NULL,
    created_at    TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS preferences (
    id            TEXT PRIMARY KEY NOT NULL,
    tenant_id     TEXT NOT NULL,
    account_id    TEXT,
    scope         TEXT NOT NULL DEFAULT 'tenant',
    key           TEXT NOT NULL,
    value         TEXT,
    created_at    TEXT NOT NULL,
    updated_at    TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_preferences_tenant ON preferences (tenant_id);
";
            ExecuteNonQuery(sql);
        }

        // ────────────────────────────────────────────────────────────
        // Seed data
        // ────────────────────────────────────────────────────────────

        internal void SeedIfEmpty()
        {
            // Only seed if no accounts exist
            using (var cmd = new SQLiteCommand("SELECT COUNT(*) FROM accounts", _conn))
            {
                long count = (long)cmd.ExecuteScalar();
                if (count > 0) return;
            }

            string now = UtcNow();
            string accountId = "acc_seed_admin";
            string tenantId = "ten_seed_default";
            string membershipId = IdGenerator.NewMembershipId();
            string workspaceId = IdGenerator.NewWorkspaceId();
            string agentId = "agt_seed_assistant";
            string revisionId = IdGenerator.NewRevisionId();

            // Seed account
            ExecuteNonQuery(
                "INSERT INTO accounts (id, display_name, email, status, created_at, updated_at) VALUES (@id, @name, @email, 'active', @now, @now)",
                P("@id", accountId), P("@name", "Admin"), P("@email", "admin@localhost"), P("@now", now));

            // Seed password: "password"
            string passwordHash = Auth.AuthManager.HashPassword("password");
            ExecuteNonQuery(
                "INSERT INTO password_credentials (account_id, password_hash, updated_at) VALUES (@id, @hash, @now)",
                P("@id", accountId), P("@hash", passwordHash), P("@now", now));

            // Seed API key: sk_local_dev_key
            string apiKeyId = IdGenerator.NewApiKeyId();
            string apiKeyHash = Auth.AuthManager.HashToken("sk_local_dev_key");
            ExecuteNonQuery(
                "INSERT INTO api_keys (id, account_id, tenant_id, key_prefix, key_hash, label, scopes, created_at) VALUES (@id, @aid, @tid, @prefix, @hash, @label, '[]', @now)",
                P("@id", apiKeyId), P("@aid", accountId), P("@tid", tenantId),
                P("@prefix", "sk_local_"), P("@hash", apiKeyHash), P("@label", "Development Key"), P("@now", now));

            // Seed tenant
            ExecuteNonQuery(
                "INSERT INTO tenants (id, slug, display_name, status, created_at, updated_at) VALUES (@id, @slug, @name, 'active', @now, @now)",
                P("@id", tenantId), P("@slug", "default"), P("@name", "Default Workspace"), P("@now", now));

            // Seed membership
            ExecuteNonQuery(
                "INSERT INTO tenant_memberships (id, tenant_id, account_id, role, created_at) VALUES (@id, @tid, @aid, 'owner', @now)",
                P("@id", membershipId), P("@tid", tenantId), P("@aid", accountId), P("@now", now));

            // Seed workspace
            ExecuteNonQuery(
                "INSERT INTO workspaces (id, tenant_id, slug, display_name, status, created_at, updated_at) VALUES (@id, @tid, @slug, @name, 'active', @now, @now)",
                P("@id", workspaceId), P("@tid", tenantId), P("@slug", "main"), P("@name", "Main Workspace"), P("@now", now));

            // Seed agent
            ExecuteNonQuery(
                "INSERT INTO agents (id, tenant_id, slug, display_name, description, status, created_by, created_at, updated_at) VALUES (@id, @tid, @slug, @name, @desc, 'active', @aid, @now, @now)",
                P("@id", agentId), P("@tid", tenantId), P("@slug", "assistant"),
                P("@name", "Assistant"), P("@desc", "Default AI assistant"), P("@aid", accountId), P("@now", now));

            // Seed agent revision
            ExecuteNonQuery(
                "INSERT INTO agent_revisions (id, agent_id, version, instructions, model, temperature, max_tokens, tool_policy, memory_policy, is_active, created_by, created_at) VALUES (@id, @agentId, 1, @instr, @model, 0.7, 4096, 'auto', 'observe', 1, @aid, @now)",
                P("@id", revisionId), P("@agentId", agentId),
                P("@instr", "You are a helpful AI assistant. Be concise and accurate."),
                P("@model", "gpt-4.1-mini"), P("@aid", accountId), P("@now", now));

            Console.WriteLine("[DB] Seeded default account (admin@localhost / password), tenant, and agent.");
        }

        // ────────────────────────────────────────────────────────────
        // Account operations
        // ────────────────────────────────────────────────────────────

        internal Account GetAccountByEmail(string email)
        {
            using (var cmd = new SQLiteCommand("SELECT * FROM accounts WHERE email = @email", _conn))
            {
                cmd.Parameters.AddWithValue("@email", email);
                using (var r = cmd.ExecuteReader())
                {
                    if (!r.Read()) return null;
                    return ReadAccount(r);
                }
            }
        }

        internal Account GetAccountById(string id)
        {
            using (var cmd = new SQLiteCommand("SELECT * FROM accounts WHERE id = @id", _conn))
            {
                cmd.Parameters.AddWithValue("@id", id);
                using (var r = cmd.ExecuteReader())
                {
                    if (!r.Read()) return null;
                    return ReadAccount(r);
                }
            }
        }

        internal PasswordCredential GetPasswordCredential(string accountId)
        {
            using (var cmd = new SQLiteCommand("SELECT * FROM password_credentials WHERE account_id = @id", _conn))
            {
                cmd.Parameters.AddWithValue("@id", accountId);
                using (var r = cmd.ExecuteReader())
                {
                    if (!r.Read()) return null;
                    return new PasswordCredential
                    {
                        AccountId = r["account_id"].ToString(),
                        PasswordHash = r["password_hash"].ToString(),
                        UpdatedAt = r["updated_at"].ToString()
                    };
                }
            }
        }

        // ────────────────────────────────────────────────────────────
        // API Key operations
        // ────────────────────────────────────────────────────────────

        internal ApiKey GetApiKeyByHash(string keyHash)
        {
            using (var cmd = new SQLiteCommand("SELECT * FROM api_keys WHERE key_hash = @hash AND revoked_at IS NULL", _conn))
            {
                cmd.Parameters.AddWithValue("@hash", keyHash);
                using (var r = cmd.ExecuteReader())
                {
                    if (!r.Read()) return null;
                    return new ApiKey
                    {
                        Id = r["id"].ToString(),
                        AccountId = r["account_id"].ToString(),
                        TenantId = r["tenant_id"].ToString(),
                        KeyPrefix = r["key_prefix"].ToString(),
                        KeyHash = r["key_hash"].ToString(),
                        Label = r["label"].ToString(),
                        Scopes = r["scopes"].ToString(),
                        ExpiresAt = r["expires_at"] as string,
                        LastUsedAt = r["last_used_at"] as string,
                        RevokedAt = r["revoked_at"] as string,
                        CreatedAt = r["created_at"].ToString()
                    };
                }
            }
        }

        internal void UpdateApiKeyLastUsed(string keyId)
        {
            ExecuteNonQuery("UPDATE api_keys SET last_used_at = @now WHERE id = @id",
                P("@now", UtcNow()), P("@id", keyId));
        }

        // ────────────────────────────────────────────────────────────
        // Auth session operations
        // ────────────────────────────────────────────────────────────

        internal void InsertAuthSession(AuthSession session)
        {
            ExecuteNonQuery(
                "INSERT INTO auth_sessions (id, account_id, tenant_id, token_hash, ip_address, user_agent, expires_at, created_at) VALUES (@id, @aid, @tid, @hash, @ip, @ua, @exp, @now)",
                P("@id", session.Id), P("@aid", session.AccountId), P("@tid", session.TenantId),
                P("@hash", session.TokenHash), P("@ip", session.IpAddress),
                P("@ua", session.UserAgent), P("@exp", session.ExpiresAt), P("@now", session.CreatedAt));
        }

        internal AuthSession GetAuthSessionByToken(string tokenHash)
        {
            using (var cmd = new SQLiteCommand("SELECT * FROM auth_sessions WHERE token_hash = @hash", _conn))
            {
                cmd.Parameters.AddWithValue("@hash", tokenHash);
                using (var r = cmd.ExecuteReader())
                {
                    if (!r.Read()) return null;
                    return new AuthSession
                    {
                        Id = r["id"].ToString(),
                        AccountId = r["account_id"].ToString(),
                        TenantId = r["tenant_id"].ToString(),
                        TokenHash = r["token_hash"].ToString(),
                        IpAddress = r["ip_address"] as string,
                        UserAgent = r["user_agent"] as string,
                        ExpiresAt = r["expires_at"].ToString(),
                        CreatedAt = r["created_at"].ToString()
                    };
                }
            }
        }

        internal void DeleteAuthSession(string sessionId)
        {
            ExecuteNonQuery("DELETE FROM auth_sessions WHERE id = @id", P("@id", sessionId));
        }

        // ────────────────────────────────────────────────────────────
        // Tenant operations
        // ────────────────────────────────────────────────────────────

        internal Tenant GetTenantById(string id)
        {
            using (var cmd = new SQLiteCommand("SELECT * FROM tenants WHERE id = @id", _conn))
            {
                cmd.Parameters.AddWithValue("@id", id);
                using (var r = cmd.ExecuteReader())
                {
                    if (!r.Read()) return null;
                    return new Tenant
                    {
                        Id = r["id"].ToString(),
                        Slug = r["slug"].ToString(),
                        DisplayName = r["display_name"].ToString(),
                        Status = r["status"].ToString(),
                        CreatedAt = r["created_at"].ToString(),
                        UpdatedAt = r["updated_at"].ToString()
                    };
                }
            }
        }

        internal TenantMembership GetMembership(string tenantId, string accountId)
        {
            using (var cmd = new SQLiteCommand("SELECT * FROM tenant_memberships WHERE tenant_id = @tid AND account_id = @aid", _conn))
            {
                cmd.Parameters.AddWithValue("@tid", tenantId);
                cmd.Parameters.AddWithValue("@aid", accountId);
                using (var r = cmd.ExecuteReader())
                {
                    if (!r.Read()) return null;
                    return new TenantMembership
                    {
                        Id = r["id"].ToString(),
                        TenantId = r["tenant_id"].ToString(),
                        AccountId = r["account_id"].ToString(),
                        Role = r["role"].ToString(),
                        CreatedAt = r["created_at"].ToString()
                    };
                }
            }
        }

        internal string GetFirstTenantIdForAccount(string accountId)
        {
            using (var cmd = new SQLiteCommand("SELECT tenant_id FROM tenant_memberships WHERE account_id = @aid LIMIT 1", _conn))
            {
                cmd.Parameters.AddWithValue("@aid", accountId);
                object result = cmd.ExecuteScalar();
                return result as string;
            }
        }

        // ────────────────────────────────────────────────────────────
        // Agent operations
        // ────────────────────────────────────────────────────────────

        internal List<AgentModel> ListAgents(string tenantId)
        {
            var list = new List<AgentModel>();
            using (var cmd = new SQLiteCommand("SELECT * FROM agents WHERE tenant_id = @tid ORDER BY created_at DESC", _conn))
            {
                cmd.Parameters.AddWithValue("@tid", tenantId);
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                        list.Add(ReadAgent(r));
                }
            }
            return list;
        }

        internal AgentModel GetAgent(string id)
        {
            using (var cmd = new SQLiteCommand("SELECT * FROM agents WHERE id = @id", _conn))
            {
                cmd.Parameters.AddWithValue("@id", id);
                using (var r = cmd.ExecuteReader())
                {
                    if (!r.Read()) return null;
                    return ReadAgent(r);
                }
            }
        }

        internal void InsertAgent(AgentModel agent)
        {
            ExecuteNonQuery(
                "INSERT INTO agents (id, tenant_id, slug, display_name, description, status, created_by, created_at, updated_at) VALUES (@id, @tid, @slug, @name, @desc, @status, @by, @now, @now)",
                P("@id", agent.Id), P("@tid", agent.TenantId), P("@slug", agent.Slug),
                P("@name", agent.DisplayName), P("@desc", (object)agent.Description ?? DBNull.Value),
                P("@status", agent.Status ?? "active"), P("@by", (object)agent.CreatedBy ?? DBNull.Value),
                P("@now", UtcNow()));
        }

        internal void UpdateAgent(AgentModel agent)
        {
            ExecuteNonQuery(
                "UPDATE agents SET slug = @slug, display_name = @name, description = @desc, status = @status, updated_at = @now WHERE id = @id",
                P("@id", agent.Id), P("@slug", agent.Slug), P("@name", agent.DisplayName),
                P("@desc", (object)agent.Description ?? DBNull.Value), P("@status", agent.Status),
                P("@now", UtcNow()));
        }

        internal void DeleteAgent(string id)
        {
            ExecuteNonQuery("UPDATE agents SET status = 'deleted', updated_at = @now WHERE id = @id",
                P("@id", id), P("@now", UtcNow()));
        }

        // ────────────────────────────────────────────────────────────
        // Agent revision operations
        // ────────────────────────────────────────────────────────────

        internal AgentRevision GetActiveRevision(string agentId)
        {
            using (var cmd = new SQLiteCommand("SELECT * FROM agent_revisions WHERE agent_id = @aid AND is_active = 1 ORDER BY version DESC LIMIT 1", _conn))
            {
                cmd.Parameters.AddWithValue("@aid", agentId);
                using (var r = cmd.ExecuteReader())
                {
                    if (!r.Read()) return null;
                    return ReadRevision(r);
                }
            }
        }

        internal void InsertRevision(AgentRevision rev)
        {
            ExecuteNonQuery(
                "INSERT INTO agent_revisions (id, agent_id, version, frontmatter, instructions, model, temperature, max_tokens, tool_policy, memory_policy, is_active, created_by, created_at) VALUES (@id, @agentId, @ver, @front, @instr, @model, @temp, @maxTok, @toolPol, @memPol, @active, @by, @now)",
                P("@id", rev.Id), P("@agentId", rev.AgentId), P("@ver", rev.Version),
                P("@front", (object)rev.Frontmatter ?? DBNull.Value),
                P("@instr", (object)rev.Instructions ?? DBNull.Value),
                P("@model", rev.Model), P("@temp", rev.Temperature), P("@maxTok", rev.MaxTokens),
                P("@toolPol", rev.ToolPolicy), P("@memPol", rev.MemoryPolicy),
                P("@active", rev.IsActive ? 1 : 0), P("@by", (object)rev.CreatedBy ?? DBNull.Value),
                P("@now", UtcNow()));
        }

        // ────────────────────────────────────────────────────────────
        // Session operations
        // ────────────────────────────────────────────────────────────

        internal void InsertWorkSession(WorkSession session)
        {
            string now = UtcNow();
            ExecuteNonQuery(
                "INSERT INTO work_sessions (id, tenant_id, workspace_id, agent_id, title, status, created_by, created_at, updated_at) VALUES (@id, @tid, @wid, @aid, @title, @status, @by, @now, @now)",
                P("@id", session.Id), P("@tid", session.TenantId),
                P("@wid", (object)session.WorkspaceId ?? DBNull.Value),
                P("@aid", (object)session.AgentId ?? DBNull.Value),
                P("@title", (object)session.Title ?? DBNull.Value),
                P("@status", session.Status ?? "active"),
                P("@by", (object)session.CreatedBy ?? DBNull.Value), P("@now", now));
        }

        internal WorkSession GetWorkSession(string id)
        {
            using (var cmd = new SQLiteCommand("SELECT * FROM work_sessions WHERE id = @id", _conn))
            {
                cmd.Parameters.AddWithValue("@id", id);
                using (var r = cmd.ExecuteReader())
                {
                    if (!r.Read()) return null;
                    return ReadWorkSession(r);
                }
            }
        }

        // ────────────────────────────────────────────────────────────
        // Thread operations
        // ────────────────────────────────────────────────────────────

        internal void InsertThread(SessionThread thread)
        {
            string now = UtcNow();
            ExecuteNonQuery(
                "INSERT INTO session_threads (id, session_id, agent_id, title, status, created_at, updated_at) VALUES (@id, @sid, @aid, @title, @status, @now, @now)",
                P("@id", thread.Id), P("@sid", thread.SessionId),
                P("@aid", (object)thread.AgentId ?? DBNull.Value),
                P("@title", (object)thread.Title ?? DBNull.Value),
                P("@status", thread.Status ?? "active"), P("@now", now));
        }

        internal SessionThread GetThread(string id)
        {
            using (var cmd = new SQLiteCommand("SELECT * FROM session_threads WHERE id = @id", _conn))
            {
                cmd.Parameters.AddWithValue("@id", id);
                using (var r = cmd.ExecuteReader())
                {
                    if (!r.Read()) return null;
                    return ReadThread(r);
                }
            }
        }

        internal List<SessionThread> ListThreadsBySession(string sessionId)
        {
            var list = new List<SessionThread>();
            using (var cmd = new SQLiteCommand("SELECT * FROM session_threads WHERE session_id = @sid ORDER BY created_at", _conn))
            {
                cmd.Parameters.AddWithValue("@sid", sessionId);
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                        list.Add(ReadThread(r));
                }
            }
            return list;
        }

        // ────────────────────────────────────────────────────────────
        // Message operations
        // ────────────────────────────────────────────────────────────

        internal void InsertMessage(SessionMessage msg)
        {
            ExecuteNonQuery(
                "INSERT INTO session_messages (id, thread_id, role, content, metadata, created_by, created_at) VALUES (@id, @tid, @role, @content, @meta, @by, @now)",
                P("@id", msg.Id), P("@tid", msg.ThreadId), P("@role", msg.Role),
                P("@content", (object)msg.Content ?? DBNull.Value),
                P("@meta", (object)msg.Metadata ?? DBNull.Value),
                P("@by", (object)msg.CreatedBy ?? DBNull.Value), P("@now", UtcNow()));
        }

        internal List<SessionMessage> ListMessagesByThread(string threadId)
        {
            var list = new List<SessionMessage>();
            using (var cmd = new SQLiteCommand("SELECT * FROM session_messages WHERE thread_id = @tid ORDER BY created_at", _conn))
            {
                cmd.Parameters.AddWithValue("@tid", threadId);
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                        list.Add(ReadMessage(r));
                }
            }
            return list;
        }

        // ────────────────────────────────────────────────────────────
        // Run operations
        // ────────────────────────────────────────────────────────────

        internal void InsertRun(Run run)
        {
            ExecuteNonQuery(
                "INSERT INTO runs (id, tenant_id, session_id, thread_id, agent_id, revision_id, status, turn_count, max_turns, error, created_at, started_at, completed_at) VALUES (@id, @tid, @sid, @thid, @aid, @rid, @status, @turns, @max, @err, @now, @start, @comp)",
                P("@id", run.Id), P("@tid", run.TenantId),
                P("@sid", (object)run.SessionId ?? DBNull.Value),
                P("@thid", (object)run.ThreadId ?? DBNull.Value),
                P("@aid", (object)run.AgentId ?? DBNull.Value),
                P("@rid", (object)run.RevisionId ?? DBNull.Value),
                P("@status", run.Status ?? "pending"),
                P("@turns", run.TurnCount), P("@max", run.MaxTurns),
                P("@err", (object)run.Error ?? DBNull.Value), P("@now", UtcNow()),
                P("@start", (object)run.StartedAt ?? DBNull.Value),
                P("@comp", (object)run.CompletedAt ?? DBNull.Value));
        }

        internal Run GetRun(string id)
        {
            using (var cmd = new SQLiteCommand("SELECT * FROM runs WHERE id = @id", _conn))
            {
                cmd.Parameters.AddWithValue("@id", id);
                using (var r = cmd.ExecuteReader())
                {
                    if (!r.Read()) return null;
                    return ReadRun(r);
                }
            }
        }

        internal void UpdateRunStatus(string id, string status, string error)
        {
            string now = UtcNow();
            string completedAt = (status == "completed" || status == "failed" || status == "cancelled") ? now : null;
            ExecuteNonQuery(
                "UPDATE runs SET status = @status, error = @err, completed_at = @comp, started_at = COALESCE(started_at, @now) WHERE id = @id",
                P("@id", id), P("@status", status),
                P("@err", (object)error ?? DBNull.Value),
                P("@comp", (object)completedAt ?? DBNull.Value), P("@now", now));
        }

        internal void IncrementRunTurnCount(string id)
        {
            ExecuteNonQuery("UPDATE runs SET turn_count = turn_count + 1 WHERE id = @id", P("@id", id));
        }

        // ────────────────────────────────────────────────────────────
        // Item operations
        // ────────────────────────────────────────────────────────────

        internal void InsertItem(Item item)
        {
            ExecuteNonQuery(
                "INSERT INTO items (id, run_id, thread_id, sequence, type, role, content, call_id, tool_name, arguments, output, is_error, created_at) VALUES (@id, @rid, @tid, @seq, @type, @role, @content, @callId, @tool, @args, @out, @err, @now)",
                P("@id", item.Id), P("@rid", (object)item.RunId ?? DBNull.Value),
                P("@tid", (object)item.ThreadId ?? DBNull.Value),
                P("@seq", item.Sequence), P("@type", item.Type),
                P("@role", (object)item.Role ?? DBNull.Value),
                P("@content", (object)item.Content ?? DBNull.Value),
                P("@callId", (object)item.CallId ?? DBNull.Value),
                P("@tool", (object)item.ToolName ?? DBNull.Value),
                P("@args", (object)item.Arguments ?? DBNull.Value),
                P("@out", (object)item.Output ?? DBNull.Value),
                P("@err", item.IsError ? 1 : 0), P("@now", UtcNow()));
        }

        internal List<Item> ListItemsByRun(string runId)
        {
            var list = new List<Item>();
            using (var cmd = new SQLiteCommand("SELECT * FROM items WHERE run_id = @rid ORDER BY sequence", _conn))
            {
                cmd.Parameters.AddWithValue("@rid", runId);
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                        list.Add(ReadItem(r));
                }
            }
            return list;
        }

        // ────────────────────────────────────────────────────────────
        // Domain event operations
        // ────────────────────────────────────────────────────────────

        internal void InsertDomainEvent(DomainEvent evt)
        {
            ExecuteNonQuery(
                "INSERT INTO domain_events (id, tenant_id, aggregate_type, aggregate_id, event_type, payload, created_at) VALUES (@id, @tid, @at, @aid, @et, @payload, @now)",
                P("@id", evt.Id), P("@tid", evt.TenantId), P("@at", evt.AggregateType),
                P("@aid", evt.AggregateId), P("@et", evt.EventType),
                P("@payload", (object)evt.Payload ?? DBNull.Value), P("@now", UtcNow()));

            // Also insert into outbox
            string outboxId = IdGenerator.NewEventId();
            ExecuteNonQuery(
                "INSERT INTO event_outbox (id, event_id, delivered) VALUES (@id, @eid, 0)",
                P("@id", outboxId), P("@eid", evt.Id));
        }

        internal List<DomainEvent> GetUndeliveredEvents(int limit)
        {
            var list = new List<DomainEvent>();
            string sql = "SELECT de.* FROM domain_events de INNER JOIN event_outbox eo ON de.id = eo.event_id WHERE eo.delivered = 0 ORDER BY de.created_at LIMIT @limit";
            using (var cmd = new SQLiteCommand(sql, _conn))
            {
                cmd.Parameters.AddWithValue("@limit", limit);
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        list.Add(new DomainEvent
                        {
                            Id = r["id"].ToString(),
                            TenantId = r["tenant_id"].ToString(),
                            AggregateType = r["aggregate_type"].ToString(),
                            AggregateId = r["aggregate_id"].ToString(),
                            EventType = r["event_type"].ToString(),
                            Payload = r["payload"] as string,
                            CreatedAt = r["created_at"].ToString()
                        });
                    }
                }
            }
            return list;
        }

        internal void MarkEventDelivered(string eventId)
        {
            ExecuteNonQuery("UPDATE event_outbox SET delivered = 1, delivered_at = @now WHERE event_id = @eid",
                P("@now", UtcNow()), P("@eid", eventId));
        }

        // ────────────────────────────────────────────────────────────
        // Memory operations
        // ────────────────────────────────────────────────────────────

        internal void InsertMemoryRecord(MemoryRecord record)
        {
            string now = UtcNow();
            ExecuteNonQuery(
                "INSERT INTO memory_records (id, tenant_id, agent_id, thread_id, session_id, run_id, scope, type, content, keywords, confidence, created_at, updated_at) VALUES (@id, @tid, @aid, @thid, @sid, @rid, @scope, @type, @content, @kw, @conf, @now, @now)",
                P("@id", record.Id), P("@tid", record.TenantId),
                P("@aid", (object)record.AgentId ?? DBNull.Value),
                P("@thid", (object)record.ThreadId ?? DBNull.Value),
                P("@sid", (object)record.SessionId ?? DBNull.Value),
                P("@rid", (object)record.RunId ?? DBNull.Value),
                P("@scope", record.Scope), P("@type", record.Type),
                P("@content", record.Content),
                P("@kw", (object)record.Keywords ?? DBNull.Value),
                P("@conf", record.Confidence), P("@now", now));
        }

        internal List<MemoryRecord> GetMemoryByThread(string threadId)
        {
            var list = new List<MemoryRecord>();
            using (var cmd = new SQLiteCommand("SELECT * FROM memory_records WHERE thread_id = @tid ORDER BY created_at", _conn))
            {
                cmd.Parameters.AddWithValue("@tid", threadId);
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                        list.Add(ReadMemoryRecord(r));
                }
            }
            return list;
        }

        internal List<MemoryRecord> GetMemoryByAgent(string agentId, string scope)
        {
            var list = new List<MemoryRecord>();
            string sql = scope != null
                ? "SELECT * FROM memory_records WHERE agent_id = @aid AND scope = @scope ORDER BY created_at"
                : "SELECT * FROM memory_records WHERE agent_id = @aid ORDER BY created_at";
            using (var cmd = new SQLiteCommand(sql, _conn))
            {
                cmd.Parameters.AddWithValue("@aid", agentId);
                if (scope != null)
                    cmd.Parameters.AddWithValue("@scope", scope);
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                        list.Add(ReadMemoryRecord(r));
                }
            }
            return list;
        }

        // ────────────────────────────────────────────────────────────
        // File operations
        // ────────────────────────────────────────────────────────────

        internal void InsertFile(FileRecord file)
        {
            ExecuteNonQuery(
                "INSERT INTO files (id, tenant_id, filename, mime_type, size_bytes, storage_path, created_by, created_at) VALUES (@id, @tid, @name, @mime, @size, @path, @by, @now)",
                P("@id", file.Id), P("@tid", file.TenantId), P("@name", file.Filename),
                P("@mime", file.MimeType), P("@size", file.SizeBytes), P("@path", file.StoragePath),
                P("@by", (object)file.CreatedBy ?? DBNull.Value), P("@now", UtcNow()));
        }

        internal List<FileRecord> ListFiles(string tenantId)
        {
            var list = new List<FileRecord>();
            using (var cmd = new SQLiteCommand("SELECT * FROM files WHERE tenant_id = @tid ORDER BY created_at DESC", _conn))
            {
                cmd.Parameters.AddWithValue("@tid", tenantId);
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        list.Add(new FileRecord
                        {
                            Id = r["id"].ToString(),
                            TenantId = r["tenant_id"].ToString(),
                            Filename = r["filename"].ToString(),
                            MimeType = r["mime_type"].ToString(),
                            SizeBytes = Convert.ToInt64(r["size_bytes"]),
                            StoragePath = r["storage_path"].ToString(),
                            CreatedBy = r["created_by"] as string,
                            CreatedAt = r["created_at"].ToString()
                        });
                    }
                }
            }
            return list;
        }

        internal FileRecord GetFile(string id)
        {
            using (var cmd = new SQLiteCommand("SELECT * FROM files WHERE id = @id", _conn))
            {
                cmd.Parameters.AddWithValue("@id", id);
                using (var r = cmd.ExecuteReader())
                {
                    if (!r.Read()) return null;
                    return new FileRecord
                    {
                        Id = r["id"].ToString(),
                        TenantId = r["tenant_id"].ToString(),
                        Filename = r["filename"].ToString(),
                        MimeType = r["mime_type"].ToString(),
                        SizeBytes = Convert.ToInt64(r["size_bytes"]),
                        StoragePath = r["storage_path"].ToString(),
                        CreatedBy = r["created_by"] as string,
                        CreatedAt = r["created_at"].ToString()
                    };
                }
            }
        }

        // ────────────────────────────────────────────────────────────
        // Row readers
        // ────────────────────────────────────────────────────────────

        private static Account ReadAccount(IDataReader r)
        {
            return new Account
            {
                Id = r["id"].ToString(),
                DisplayName = r["display_name"].ToString(),
                Email = r["email"].ToString(),
                AvatarUrl = r["avatar_url"] as string,
                Status = r["status"].ToString(),
                CreatedAt = r["created_at"].ToString(),
                UpdatedAt = r["updated_at"].ToString()
            };
        }

        private static AgentModel ReadAgent(IDataReader r)
        {
            return new AgentModel
            {
                Id = r["id"].ToString(),
                TenantId = r["tenant_id"].ToString(),
                Slug = r["slug"].ToString(),
                DisplayName = r["display_name"].ToString(),
                Description = r["description"] as string,
                Status = r["status"].ToString(),
                CreatedBy = r["created_by"] as string,
                CreatedAt = r["created_at"].ToString(),
                UpdatedAt = r["updated_at"].ToString()
            };
        }

        private static AgentRevision ReadRevision(IDataReader r)
        {
            return new AgentRevision
            {
                Id = r["id"].ToString(),
                AgentId = r["agent_id"].ToString(),
                Version = Convert.ToInt32(r["version"]),
                Frontmatter = r["frontmatter"] as string,
                Instructions = r["instructions"] as string,
                Model = r["model"].ToString(),
                Temperature = Convert.ToDouble(r["temperature"]),
                MaxTokens = Convert.ToInt32(r["max_tokens"]),
                ToolPolicy = r["tool_policy"].ToString(),
                MemoryPolicy = r["memory_policy"].ToString(),
                IsActive = Convert.ToInt32(r["is_active"]) != 0,
                CreatedBy = r["created_by"] as string,
                CreatedAt = r["created_at"].ToString()
            };
        }

        private static WorkSession ReadWorkSession(IDataReader r)
        {
            return new WorkSession
            {
                Id = r["id"].ToString(),
                TenantId = r["tenant_id"].ToString(),
                WorkspaceId = r["workspace_id"] as string,
                AgentId = r["agent_id"] as string,
                Title = r["title"] as string,
                Status = r["status"].ToString(),
                CreatedBy = r["created_by"] as string,
                CreatedAt = r["created_at"].ToString(),
                UpdatedAt = r["updated_at"].ToString()
            };
        }

        private static SessionThread ReadThread(IDataReader r)
        {
            return new SessionThread
            {
                Id = r["id"].ToString(),
                SessionId = r["session_id"].ToString(),
                AgentId = r["agent_id"] as string,
                Title = r["title"] as string,
                Status = r["status"].ToString(),
                CreatedAt = r["created_at"].ToString(),
                UpdatedAt = r["updated_at"].ToString()
            };
        }

        private static SessionMessage ReadMessage(IDataReader r)
        {
            return new SessionMessage
            {
                Id = r["id"].ToString(),
                ThreadId = r["thread_id"].ToString(),
                Role = r["role"].ToString(),
                Content = r["content"] as string,
                Metadata = r["metadata"] as string,
                CreatedBy = r["created_by"] as string,
                CreatedAt = r["created_at"].ToString()
            };
        }

        private static Run ReadRun(IDataReader r)
        {
            return new Run
            {
                Id = r["id"].ToString(),
                TenantId = r["tenant_id"].ToString(),
                SessionId = r["session_id"] as string,
                ThreadId = r["thread_id"] as string,
                AgentId = r["agent_id"] as string,
                RevisionId = r["revision_id"] as string,
                Status = r["status"].ToString(),
                TurnCount = Convert.ToInt32(r["turn_count"]),
                MaxTurns = Convert.ToInt32(r["max_turns"]),
                Error = r["error"] as string,
                CreatedAt = r["created_at"].ToString(),
                StartedAt = r["started_at"] as string,
                CompletedAt = r["completed_at"] as string
            };
        }

        private static Item ReadItem(IDataReader r)
        {
            return new Item
            {
                Id = r["id"].ToString(),
                RunId = r["run_id"] as string,
                ThreadId = r["thread_id"] as string,
                Sequence = Convert.ToInt32(r["sequence"]),
                Type = r["type"].ToString(),
                Role = r["role"] as string,
                Content = r["content"] as string,
                CallId = r["call_id"] as string,
                ToolName = r["tool_name"] as string,
                Arguments = r["arguments"] as string,
                Output = r["output"] as string,
                IsError = Convert.ToInt32(r["is_error"]) != 0,
                CreatedAt = r["created_at"].ToString()
            };
        }

        private static MemoryRecord ReadMemoryRecord(IDataReader r)
        {
            return new MemoryRecord
            {
                Id = r["id"].ToString(),
                TenantId = r["tenant_id"].ToString(),
                AgentId = r["agent_id"] as string,
                ThreadId = r["thread_id"] as string,
                SessionId = r["session_id"] as string,
                RunId = r["run_id"] as string,
                Scope = r["scope"].ToString(),
                Type = r["type"].ToString(),
                Content = r["content"].ToString(),
                Keywords = r["keywords"] as string,
                Confidence = Convert.ToDouble(r["confidence"]),
                CreatedAt = r["created_at"].ToString(),
                UpdatedAt = r["updated_at"].ToString()
            };
        }

        // ────────────────────────────────────────────────────────────
        // Helpers
        // ────────────────────────────────────────────────────────────

        private void ExecuteNonQuery(string sql, params SQLiteParameter[] parameters)
        {
            using (var cmd = new SQLiteCommand(sql, _conn))
            {
                if (parameters != null)
                    cmd.Parameters.AddRange(parameters);
                cmd.ExecuteNonQuery();
            }
        }

        private static SQLiteParameter P(string name, object value)
        {
            return new SQLiteParameter(name, value ?? DBNull.Value);
        }

        internal static string UtcNow()
        {
            return DateTime.UtcNow.ToString("o");
        }

        public void Dispose()
        {
            _conn?.Dispose();
        }
    }
}
