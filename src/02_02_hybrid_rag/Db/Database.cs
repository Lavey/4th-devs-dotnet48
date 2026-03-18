using System;
using System.Data.SQLite;
using System.IO;

namespace FourthDevs.Lesson07_HybridRag.Db
{
    /// <summary>
    /// Initialises the SQLite database with the tables needed for the hybrid RAG pipeline:
    /// <list type="bullet">
    ///   <item><description><c>documents</c> — source files with content and hash</description></item>
    ///   <item><description><c>chunks</c> — text chunks with section metadata</description></item>
    ///   <item><description><c>chunks_fts</c> — FTS5 virtual table backed by chunks</description></item>
    ///   <item><description><c>chunks_vec</c> — embedding blobs for cosine similarity search</description></item>
    /// </list>
    ///
    /// Mirrors 02_02_hybrid_rag/src/db/index.js (i-am-alice/4th-devs).
    /// Note: sqlite-vec is not available on .NET 4.8; vector search is performed
    /// in-memory over blobs stored in <c>chunks_vec</c>.
    /// </summary>
    internal static class Database
    {
        // ----------------------------------------------------------------
        // Public API
        // ----------------------------------------------------------------

        /// <summary>
        /// Opens (or creates) the hybrid RAG database, applies WAL mode, and
        /// creates all required tables and triggers.
        /// </summary>
        internal static SQLiteConnection Open(string dbPath = null)
        {
            if (dbPath == null)
                dbPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "data", "hybrid.db");

            string dir = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var connection = new SQLiteConnection(
                string.Format("Data Source={0};Version=3;", dbPath));
            connection.Open();

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "PRAGMA journal_mode=WAL;";       cmd.ExecuteNonQuery();
                cmd.CommandText = "PRAGMA synchronous=NORMAL;";     cmd.ExecuteNonQuery();
                cmd.CommandText = "PRAGMA foreign_keys=ON;";        cmd.ExecuteNonQuery();
                cmd.CommandText = "PRAGMA busy_timeout=5000;";      cmd.ExecuteNonQuery();
            }

            ApplySchema(connection);
            return connection;
        }

        // ----------------------------------------------------------------
        // Schema
        // ----------------------------------------------------------------

        private static void ApplySchema(SQLiteConnection con)
        {
            Execute(con, @"
                CREATE TABLE IF NOT EXISTS documents (
                    id         INTEGER PRIMARY KEY AUTOINCREMENT,
                    source     TEXT NOT NULL UNIQUE,
                    content    TEXT NOT NULL,
                    hash       TEXT NOT NULL,
                    indexed_at TEXT DEFAULT (datetime('now'))
                );");

            Execute(con, @"
                CREATE TABLE IF NOT EXISTS chunks (
                    id          INTEGER PRIMARY KEY AUTOINCREMENT,
                    document_id INTEGER NOT NULL REFERENCES documents(id) ON DELETE CASCADE,
                    content     TEXT NOT NULL,
                    chunk_index INTEGER NOT NULL,
                    section     TEXT,
                    chars       INTEGER NOT NULL
                );");

            Execute(con, @"
                CREATE VIRTUAL TABLE IF NOT EXISTS chunks_fts USING fts5(
                    content,
                    content='chunks',
                    content_rowid='id'
                );");

            Execute(con, @"
                CREATE TRIGGER IF NOT EXISTS chunks_ai AFTER INSERT ON chunks BEGIN
                    INSERT INTO chunks_fts(rowid, content) VALUES (new.id, new.content);
                END;");

            Execute(con, @"
                CREATE TRIGGER IF NOT EXISTS chunks_ad AFTER DELETE ON chunks BEGIN
                    INSERT INTO chunks_fts(chunks_fts, rowid, content)
                    VALUES ('delete', old.id, old.content);
                END;");

            Execute(con, @"
                CREATE TRIGGER IF NOT EXISTS chunks_au AFTER UPDATE ON chunks BEGIN
                    INSERT INTO chunks_fts(chunks_fts, rowid, content)
                    VALUES ('delete', old.id, old.content);
                    INSERT INTO chunks_fts(rowid, content) VALUES (new.id, new.content);
                END;");

            // In-memory vector search — store embeddings as raw float[] blobs
            Execute(con, @"
                CREATE TABLE IF NOT EXISTS chunks_vec (
                    chunk_id  INTEGER PRIMARY KEY REFERENCES chunks(id) ON DELETE CASCADE,
                    embedding BLOB NOT NULL
                );");
        }

        private static void Execute(SQLiteConnection con, string sql)
        {
            using (var cmd = con.CreateCommand())
            {
                cmd.CommandText = sql;
                cmd.ExecuteNonQuery();
            }
        }
    }
}
