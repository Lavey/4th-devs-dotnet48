using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Aype.AI.ApiClient;

namespace Aype.AI.AgentHybridRag.Db
{
    /// <summary>
    /// Reads text files from the workspace directory, chunks them, generates
    /// embeddings, and stores everything in the SQLite database.
    /// </summary>
    internal static class Indexer
    {
        private static readonly HashSet<string> SupportedExts =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".md", ".txt" };

        private const int ChunkSize    = 1000;
        private const int ChunkOverlap = 200;

        // ----------------------------------------------------------------
        // Public API
        // ----------------------------------------------------------------

        internal static async Task IndexWorkspaceAsync(
            SQLiteConnection db, string workspacePath)
        {
            Directory.CreateDirectory(workspacePath);

            string[] files    = Directory.GetFiles(workspacePath);
            var supported     = new List<string>();
            foreach (string f in files)
            {
                string ext = Path.GetExtension(f);
                if (SupportedExts.Contains(ext))
                    supported.Add(f);
            }

            if (supported.Count == 0)
            {
                Console.WriteLine("[indexer] No .md/.txt files found in " + workspacePath);
                return;
            }

            Console.WriteLine(string.Format("[indexer] Found {0} file(s)", supported.Count));

            using (var embedder = new EmbeddingClient())
            {
                foreach (string filePath in supported)
                    await IndexFileAsync(db, embedder, filePath, Path.GetFileName(filePath));
            }

            // Prune stale entries
            var onDisk = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string f in supported)
                onDisk.Add(Path.GetFileName(f));

            var indexed = SelectAll(db,
                "SELECT id, source FROM documents",
                r => (r.GetInt64(0), r.GetString(1)));

            foreach (var (id, source) in indexed)
            {
                if (!onDisk.Contains(source))
                {
                    Console.WriteLine("[indexer] Removing stale: " + source);
                    RemoveDocument(db, id);
                }
            }
        }

        // ----------------------------------------------------------------
        // Single-file indexing
        // ----------------------------------------------------------------

        private static async Task IndexFileAsync(
            SQLiteConnection db, EmbeddingClient embedder,
            string filePath, string fileName)
        {
            string content = File.ReadAllText(filePath, Encoding.UTF8).Trim();
            if (string.IsNullOrEmpty(content))
            {
                Console.WriteLine("[indexer] Skipping empty: " + fileName);
                return;
            }

            string hash = Sha256(content);

            long?  existingId   = null;
            string existingHash = null;

            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText = "SELECT id, hash FROM documents WHERE source = @s";
                cmd.Parameters.AddWithValue("@s", fileName);
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        existingId   = reader.GetInt64(0);
                        existingHash = reader.GetString(1);
                    }
                }
            }

            if (existingHash == hash)
            {
                Console.WriteLine("[indexer] Skipping (unchanged): " + fileName);
                return;
            }

            if (existingId.HasValue)
            {
                Console.WriteLine("[indexer] Re-indexing (changed): " + fileName);
                RemoveDocument(db, existingId.Value);
            }

            var chunks = ChunkBySeparators(content, fileName);
            Console.WriteLine(string.Format(
                "[indexer] {0}: {1} chunk(s)", fileName, chunks.Count));

            long docId;
            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText =
                    "INSERT INTO documents (source, content, hash) VALUES (@s, @c, @h)";
                cmd.Parameters.AddWithValue("@s", fileName);
                cmd.Parameters.AddWithValue("@c", content);
                cmd.Parameters.AddWithValue("@h", hash);
                cmd.ExecuteNonQuery();
                docId = db.LastInsertRowId;
            }

            var chunkIds = new List<long>();
            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText =
                    "INSERT INTO chunks (document_id, content, chunk_index, section, chars) " +
                    "VALUES (@d, @c, @i, @s, @ch)";

                foreach (var chunk in chunks)
                {
                    cmd.Parameters.Clear();
                    cmd.Parameters.AddWithValue("@d",  docId);
                    cmd.Parameters.AddWithValue("@c",  chunk.Content);
                    cmd.Parameters.AddWithValue("@i",  chunk.Index);
                    cmd.Parameters.AddWithValue("@s",  (object)chunk.Section ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ch", chunk.Content.Length);
                    cmd.ExecuteNonQuery();
                    chunkIds.Add(db.LastInsertRowId);
                }
            }

            var contents = new List<string>();
            foreach (var chunk in chunks)
                contents.Add(chunk.Content);

            Console.Write("  embeddings: 0/" + chunks.Count + "\r");
            List<float[]> embeddings = await embedder.EmbedBatchAsync(contents);
            Console.WriteLine(string.Format(
                "  embeddings: {0}/{0}                    ", chunks.Count));

            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText =
                    "INSERT INTO chunks_vec (chunk_id, embedding) VALUES (@id, @emb)";

                for (int i = 0; i < chunkIds.Count; i++)
                {
                    cmd.Parameters.Clear();
                    cmd.Parameters.AddWithValue("@id",  chunkIds[i]);
                    cmd.Parameters.AddWithValue("@emb", ToBytes(embeddings[i]));
                    cmd.ExecuteNonQuery();
                }
            }

            Console.WriteLine(string.Format(
                "[indexer] Indexed {0}: {1} chunks", fileName, chunks.Count));
        }

        // ----------------------------------------------------------------
        // Chunking
        // ----------------------------------------------------------------

        private sealed class TextChunk
        {
            public string Content { get; set; }
            public int    Index   { get; set; }
            public string Section { get; set; }
        }

        private static readonly string[] Separators =
        {
            "\n## ", "\n### ", "\n\n", "\n", ". ", " "
        };

        private static List<TextChunk> ChunkBySeparators(string text, string source)
        {
            var rawChunks = SplitRecursive(text, ChunkSize, ChunkOverlap, Separators);
            var headings  = BuildHeadingIndex(text);
            var result    = new List<TextChunk>();

            for (int i = 0; i < rawChunks.Count; i++)
            {
                result.Add(new TextChunk
                {
                    Content = rawChunks[i],
                    Index   = i,
                    Section = FindSection(text, rawChunks[i], headings)
                });
            }

            return result;
        }

        private static List<string> SplitRecursive(
            string text, int size, int overlap, string[] seps)
        {
            if (text.Length <= size) return new List<string> { text };

            string sep    = null;
            int    sepIdx = -1;
            for (int i = 0; i < seps.Length; i++)
            {
                if (text.Contains(seps[i]))
                {
                    sep    = seps[i];
                    sepIdx = i;
                    break;
                }
            }

            if (sep == null) return new List<string> { text };

            var    parts  = text.Split(new[] { sep }, StringSplitOptions.None);
            var    chunks = new List<string>();
            string curr   = string.Empty;

            foreach (string part in parts)
            {
                string cand = string.IsNullOrEmpty(curr) ? part : curr + sep + part;
                if (cand.Length > size && !string.IsNullOrEmpty(curr))
                {
                    chunks.Add(curr);
                    int    start = Math.Max(0, curr.Length - overlap);
                    string tail  = curr.Substring(start);
                    int    nlIdx = tail.IndexOf('\n');
                    if (nlIdx == -1)
                        for (int k = 0; k < tail.Length; k++)
                            if (char.IsWhiteSpace(tail[k])) { nlIdx = k; break; }

                    string ov = nlIdx == -1
                        ? string.Empty
                        : curr.Substring(start + nlIdx + 1);

                    if (!string.IsNullOrEmpty(sep) &&
                        ov.StartsWith(sep, StringComparison.Ordinal))
                        ov = ov.Substring(sep.Length);

                    curr = string.IsNullOrEmpty(ov) ? part : ov + sep + part;
                }
                else
                {
                    curr = cand;
                }
            }

            if (!string.IsNullOrEmpty(curr))
                chunks.Add(curr);

            int remStart = sepIdx + 1;
            int remLen   = seps.Length - remStart;
            var rem      = new string[remLen];
            for (int i = 0; i < remLen; i++)
                rem[i] = seps[remStart + i];

            var result = new List<string>();
            foreach (string c in chunks)
            {
                if (c.Length > size && rem.Length > 0)
                    result.AddRange(SplitRecursive(c, size, overlap, rem));
                else
                    result.Add(c);
            }

            return result;
        }

        // ----------------------------------------------------------------
        // Heading index
        // ----------------------------------------------------------------

        private struct Heading
        {
            public int    Position { get; set; }
            public int    Level    { get; set; }
            public string Title    { get; set; }
        }

        private static List<Heading> BuildHeadingIndex(string text)
        {
            var headings = new List<Heading>();
            var mdRegex  = new Regex(@"^(#{1,6})\s+(.+)$", RegexOptions.Multiline);
            foreach (Match m in mdRegex.Matches(text))
            {
                headings.Add(new Heading
                {
                    Position = m.Index,
                    Level    = m.Groups[1].Length,
                    Title    = m.Groups[2].Value.Trim()
                });
            }
            headings.Sort((a, b) => a.Position.CompareTo(b.Position));
            return headings;
        }

        private static string FindSection(
            string text, string chunk, List<Heading> headings)
        {
            if (headings.Count == 0) return null;
            int mid = (int)(chunk.Length * 0.4);
            int len = Math.Min(100, chunk.Length - mid);
            if (len <= 0) return null;

            string sample = chunk.Substring(mid, len);
            int    pos    = text.IndexOf(sample, StringComparison.Ordinal);
            if (pos == -1) return null;

            Heading? current = null;
            foreach (var h in headings)
            {
                if (h.Position <= pos) current = h;
                else break;
            }

            if (current == null) return null;
            return new string('#', current.Value.Level) + " " + current.Value.Title;
        }

        // ----------------------------------------------------------------
        // Helpers
        // ----------------------------------------------------------------

        private static void RemoveDocument(SQLiteConnection db, long docId)
        {
            Execute(db,
                "DELETE FROM chunks_vec WHERE chunk_id IN " +
                "(SELECT id FROM chunks WHERE document_id = @id)",
                ("@id", docId));
            Execute(db, "DELETE FROM chunks WHERE document_id = @id", ("@id", docId));
            Execute(db, "DELETE FROM documents WHERE id = @id",       ("@id", docId));
        }

        private static void Execute(
            SQLiteConnection db, string sql,
            params (string name, object value)[] parms)
        {
            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText = sql;
                foreach (var (name, value) in parms)
                    cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
                cmd.ExecuteNonQuery();
            }
        }

        private static List<T> SelectAll<T>(
            SQLiteConnection db, string sql, Func<SQLiteDataReader, T> map)
        {
            var result = new List<T>();
            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText = sql;
                using (var r = cmd.ExecuteReader())
                    while (r.Read())
                        result.Add(map(r));
            }
            return result;
        }

        private static string Sha256(string text)
        {
            using (var sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
                var sb = new StringBuilder();
                foreach (byte b in bytes)
                    sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        private static byte[] ToBytes(float[] arr)
        {
            var bytes = new byte[arr.Length * sizeof(float)];
            Buffer.BlockCopy(arr, 0, bytes, 0, bytes.Length);
            return bytes;
        }
    }
}
