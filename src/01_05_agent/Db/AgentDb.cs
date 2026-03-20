using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;

namespace FourthDevs.Lesson05_Agent.Db
{
    /// <summary>
    /// SQLite persistence layer for sessions, agents, and items.
    ///
    /// Mirrors the Drizzle schema from 01_05_agent/drizzle/0000_romantic_union_jack.sql
    /// and the repository layer in 01_05_agent/src/repositories/sqlite/ (i-am-alice/4th-devs).
    /// </summary>
    internal class AgentDb : IDisposable
    {
        private readonly SQLiteConnection _conn;

        internal AgentDb(string databaseUrl)
        {
            // databaseUrl may be "file:.data/agent.db" or a plain path
            string path = databaseUrl;
            if (path.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
                path = path.Substring("file:".Length);

            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            _conn = new SQLiteConnection("Data Source=" + path + ";Version=3;");
            _conn.Open();

            EnsureSchema();
        }

        // ────────────────────────────────────────────────────────────
        // Schema
        // ────────────────────────────────────────────────────────────

        private void EnsureSchema()
        {
            string sql = @"
CREATE TABLE IF NOT EXISTS users (
    id            TEXT PRIMARY KEY NOT NULL,
    email         TEXT NOT NULL,
    api_key_hash  TEXT NOT NULL,
    created_at    INTEGER NOT NULL,
    updated_at    INTEGER
);
CREATE UNIQUE INDEX IF NOT EXISTS users_email_idx       ON users (email);
CREATE UNIQUE INDEX IF NOT EXISTS users_api_key_hash_idx ON users (api_key_hash);

CREATE TABLE IF NOT EXISTS sessions (
    id             TEXT PRIMARY KEY NOT NULL,
    user_id        TEXT,
    root_agent_id  TEXT,
    title          TEXT,
    summary        TEXT,
    status         TEXT NOT NULL DEFAULT 'active',
    created_at     INTEGER NOT NULL,
    updated_at     INTEGER
);
CREATE INDEX IF NOT EXISTS sessions_user_idx ON sessions (user_id);

CREATE TABLE IF NOT EXISTS agents (
    id              TEXT PRIMARY KEY NOT NULL,
    session_id      TEXT NOT NULL REFERENCES sessions(id),
    root_agent_id   TEXT NOT NULL,
    parent_id       TEXT,
    source_call_id  TEXT,
    depth           INTEGER NOT NULL DEFAULT 0,
    task            TEXT NOT NULL,
    config          TEXT NOT NULL,
    status          TEXT NOT NULL DEFAULT 'pending',
    waiting_for     TEXT NOT NULL DEFAULT '[]',
    result          TEXT,
    error           TEXT,
    turn_count      INTEGER NOT NULL DEFAULT 0,
    created_at      INTEGER NOT NULL,
    started_at      INTEGER,
    completed_at    INTEGER
);
CREATE INDEX IF NOT EXISTS agents_session_idx ON agents (session_id);
CREATE INDEX IF NOT EXISTS agents_parent_idx  ON agents (parent_id);
CREATE INDEX IF NOT EXISTS agents_status_idx  ON agents (status);

CREATE TABLE IF NOT EXISTS items (
    id          TEXT PRIMARY KEY NOT NULL,
    agent_id    TEXT NOT NULL REFERENCES agents(id),
    sequence    INTEGER NOT NULL,
    type        TEXT NOT NULL,
    role        TEXT,
    content     TEXT,
    call_id     TEXT,
    name        TEXT,
    arguments   TEXT,
    output      TEXT,
    is_error    INTEGER,
    summary     TEXT,
    signature   TEXT,
    created_at  INTEGER NOT NULL
);
CREATE INDEX IF NOT EXISTS items_agent_seq_idx ON items (agent_id, sequence);
CREATE INDEX IF NOT EXISTS items_call_id_idx   ON items (call_id);
";
            using (var cmd = new SQLiteCommand(sql, _conn))
                cmd.ExecuteNonQuery();
        }

        // ────────────────────────────────────────────────────────────
        // Sessions
        // ────────────────────────────────────────────────────────────

        internal void UpsertSession(string id, string userId, string rootAgentId, string status)
        {
            long now = ToUnixMs(DateTime.UtcNow);
            const string sql = @"
INSERT INTO sessions (id, user_id, root_agent_id, status, created_at, updated_at)
VALUES (@id, @userId, @rootAgentId, @status, @now, @now)
ON CONFLICT(id) DO UPDATE SET
    root_agent_id = @rootAgentId,
    status        = @status,
    updated_at    = @now;";

            using (var cmd = new SQLiteCommand(sql, _conn))
            {
                cmd.Parameters.AddWithValue("@id", id);
                cmd.Parameters.AddWithValue("@userId", (object)userId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@rootAgentId", (object)rootAgentId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@status", status ?? "active");
                cmd.Parameters.AddWithValue("@now", now);
                cmd.ExecuteNonQuery();
            }
        }

        internal SessionRow GetSession(string id)
        {
            const string sql = "SELECT * FROM sessions WHERE id = @id";
            using (var cmd = new SQLiteCommand(sql, _conn))
            {
                cmd.Parameters.AddWithValue("@id", id);
                using (var reader = cmd.ExecuteReader())
                {
                    if (!reader.Read()) return null;
                    return ReadSession(reader);
                }
            }
        }

        // ────────────────────────────────────────────────────────────
        // Agents
        // ────────────────────────────────────────────────────────────

        internal void UpsertAgent(AgentRow agent)
        {
            const string sql = @"
INSERT INTO agents (id, session_id, root_agent_id, parent_id, source_call_id, depth,
                    task, config, status, waiting_for, result, error, turn_count,
                    created_at, started_at, completed_at)
VALUES (@id, @sessionId, @rootAgentId, @parentId, @sourceCallId, @depth,
        @task, @config, @status, @waitingFor, @result, @error, @turnCount,
        @createdAt, @startedAt, @completedAt)
ON CONFLICT(id) DO UPDATE SET
    status       = @status,
    waiting_for  = @waitingFor,
    result       = @result,
    error        = @error,
    turn_count   = @turnCount,
    started_at   = @startedAt,
    completed_at = @completedAt;";

            using (var cmd = new SQLiteCommand(sql, _conn))
            {
                cmd.Parameters.AddWithValue("@id", agent.Id);
                cmd.Parameters.AddWithValue("@sessionId", agent.SessionId);
                cmd.Parameters.AddWithValue("@rootAgentId", agent.RootAgentId);
                cmd.Parameters.AddWithValue("@parentId", (object)agent.ParentId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@sourceCallId", (object)agent.SourceCallId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@depth", agent.Depth);
                cmd.Parameters.AddWithValue("@task", agent.Task ?? string.Empty);
                cmd.Parameters.AddWithValue("@config", agent.Config ?? "{}");
                cmd.Parameters.AddWithValue("@status", agent.Status ?? "pending");
                cmd.Parameters.AddWithValue("@waitingFor", agent.WaitingFor ?? "[]");
                cmd.Parameters.AddWithValue("@result", (object)agent.Result ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@error", (object)agent.Error ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@turnCount", agent.TurnCount);
                cmd.Parameters.AddWithValue("@createdAt", agent.CreatedAt);
                cmd.Parameters.AddWithValue("@startedAt", (object)agent.StartedAt ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@completedAt", (object)agent.CompletedAt ?? DBNull.Value);
                cmd.ExecuteNonQuery();
            }
        }

        internal AgentRow GetAgent(string id)
        {
            const string sql = "SELECT * FROM agents WHERE id = @id";
            using (var cmd = new SQLiteCommand(sql, _conn))
            {
                cmd.Parameters.AddWithValue("@id", id);
                using (var reader = cmd.ExecuteReader())
                {
                    if (!reader.Read()) return null;
                    return ReadAgent(reader);
                }
            }
        }

        internal List<AgentRow> ListAgentsBySession(string sessionId)
        {
            var list = new List<AgentRow>();
            const string sql = "SELECT * FROM agents WHERE session_id = @sid ORDER BY created_at";
            using (var cmd = new SQLiteCommand(sql, _conn))
            {
                cmd.Parameters.AddWithValue("@sid", sessionId);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                        list.Add(ReadAgent(reader));
                }
            }
            return list;
        }

        // ────────────────────────────────────────────────────────────
        // Items
        // ────────────────────────────────────────────────────────────

        internal void InsertItem(ItemRow item)
        {
            const string sql = @"
INSERT INTO items (id, agent_id, sequence, type, role, content, call_id,
                   name, arguments, output, is_error, summary, signature, created_at)
VALUES (@id, @agentId, @seq, @type, @role, @content, @callId,
        @name, @args, @output, @isError, @summary, @signature, @createdAt);";

            using (var cmd = new SQLiteCommand(sql, _conn))
            {
                cmd.Parameters.AddWithValue("@id", item.Id);
                cmd.Parameters.AddWithValue("@agentId", item.AgentId);
                cmd.Parameters.AddWithValue("@seq", item.Sequence);
                cmd.Parameters.AddWithValue("@type", item.Type);
                cmd.Parameters.AddWithValue("@role", (object)item.Role ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@content", (object)item.Content ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@callId", (object)item.CallId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@name", (object)item.Name ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@args", (object)item.Arguments ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@output", (object)item.Output ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@isError", item.IsError.HasValue ? (object)(item.IsError.Value ? 1 : 0) : DBNull.Value);
                cmd.Parameters.AddWithValue("@summary", (object)item.Summary ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@signature", (object)item.Signature ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@createdAt", item.CreatedAt);
                cmd.ExecuteNonQuery();
            }
        }

        internal List<ItemRow> ListItemsByAgent(string agentId)
        {
            var list = new List<ItemRow>();
            const string sql = "SELECT * FROM items WHERE agent_id = @aid ORDER BY sequence";
            using (var cmd = new SQLiteCommand(sql, _conn))
            {
                cmd.Parameters.AddWithValue("@aid", agentId);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                        list.Add(ReadItem(reader));
                }
            }
            return list;
        }

        internal int GetNextSequence(string agentId)
        {
            const string sql = "SELECT MAX(sequence) FROM items WHERE agent_id = @aid";
            using (var cmd = new SQLiteCommand(sql, _conn))
            {
                cmd.Parameters.AddWithValue("@aid", agentId);
                object result = cmd.ExecuteScalar();
                if (result == null || result == DBNull.Value) return 0;
                return Convert.ToInt32(result) + 1;
            }
        }

        // ────────────────────────────────────────────────────────────
        // Row readers
        // ────────────────────────────────────────────────────────────

        private static SessionRow ReadSession(IDataReader r)
        {
            return new SessionRow
            {
                Id          = r["id"].ToString(),
                UserId      = r["user_id"] as string,
                RootAgentId = r["root_agent_id"] as string,
                Title       = r["title"] as string,
                Summary     = r["summary"] as string,
                Status      = r["status"].ToString(),
                CreatedAt   = Convert.ToInt64(r["created_at"]),
                UpdatedAt   = r["updated_at"] == DBNull.Value ? (long?)null : Convert.ToInt64(r["updated_at"])
            };
        }

        private static AgentRow ReadAgent(IDataReader r)
        {
            return new AgentRow
            {
                Id            = r["id"].ToString(),
                SessionId     = r["session_id"].ToString(),
                RootAgentId   = r["root_agent_id"].ToString(),
                ParentId      = r["parent_id"] as string,
                SourceCallId  = r["source_call_id"] as string,
                Depth         = Convert.ToInt32(r["depth"]),
                Task          = r["task"].ToString(),
                Config        = r["config"].ToString(),
                Status        = r["status"].ToString(),
                WaitingFor    = r["waiting_for"].ToString(),
                Result        = r["result"] as string,
                Error         = r["error"] as string,
                TurnCount     = Convert.ToInt32(r["turn_count"]),
                CreatedAt     = Convert.ToInt64(r["created_at"]),
                StartedAt     = r["started_at"] == DBNull.Value ? (long?)null : Convert.ToInt64(r["started_at"]),
                CompletedAt   = r["completed_at"] == DBNull.Value ? (long?)null : Convert.ToInt64(r["completed_at"])
            };
        }

        private static ItemRow ReadItem(IDataReader r)
        {
            return new ItemRow
            {
                Id        = r["id"].ToString(),
                AgentId   = r["agent_id"].ToString(),
                Sequence  = Convert.ToInt32(r["sequence"]),
                Type      = r["type"].ToString(),
                Role      = r["role"] as string,
                Content   = r["content"] as string,
                CallId    = r["call_id"] as string,
                Name      = r["name"] as string,
                Arguments = r["arguments"] as string,
                Output    = r["output"] as string,
                IsError   = r["is_error"] == DBNull.Value ? (bool?)null : Convert.ToInt32(r["is_error"]) != 0,
                Summary   = r["summary"] as string,
                Signature = r["signature"] as string,
                CreatedAt = Convert.ToInt64(r["created_at"])
            };
        }

        // ────────────────────────────────────────────────────────────
        // Helpers
        // ────────────────────────────────────────────────────────────

        internal static long ToUnixMs(DateTime utc)
        {
            return (long)(utc - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
        }

        public void Dispose()
        {
            _conn?.Dispose();
        }
    }

    // ────────────────────────────────────────────────────────────────
    // Row types (mirror drizzle schema)
    // ────────────────────────────────────────────────────────────────

    internal class SessionRow
    {
        public string Id          { get; set; }
        public string UserId      { get; set; }
        public string RootAgentId { get; set; }
        public string Title       { get; set; }
        public string Summary     { get; set; }
        public string Status      { get; set; }
        public long   CreatedAt   { get; set; }
        public long?  UpdatedAt   { get; set; }
    }

    internal class AgentRow
    {
        public string Id           { get; set; }
        public string SessionId    { get; set; }
        public string RootAgentId  { get; set; }
        public string ParentId     { get; set; }
        public string SourceCallId { get; set; }
        public int    Depth        { get; set; }
        public string Task         { get; set; }
        public string Config       { get; set; }
        public string Status       { get; set; }
        public string WaitingFor   { get; set; }
        public string Result       { get; set; }
        public string Error        { get; set; }
        public int    TurnCount    { get; set; }
        public long   CreatedAt    { get; set; }
        public long?  StartedAt    { get; set; }
        public long?  CompletedAt  { get; set; }
    }

    internal class ItemRow
    {
        public string Id        { get; set; }
        public string AgentId   { get; set; }
        public int    Sequence  { get; set; }
        public string Type      { get; set; }
        public string Role      { get; set; }
        public string Content   { get; set; }
        public string CallId    { get; set; }
        public string Name      { get; set; }
        public string Arguments { get; set; }
        public string Output    { get; set; }
        public bool?  IsError   { get; set; }
        public string Summary   { get; set; }
        public string Signature { get; set; }
        public long   CreatedAt { get; set; }
    }
}
