using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Aype.AI.ApiClient;

namespace Aype.AI.AgentHybridRag.Db
{
    /// <summary>
    /// Hybrid search: FTS5 (BM25) keyword search combined with in-memory cosine
    /// vector similarity, merged via Reciprocal Rank Fusion (RRF).
    /// </summary>
    internal static class Search
    {
        private const int    RrfK = 60;

        // ----------------------------------------------------------------
        // HybridSearch
        // ----------------------------------------------------------------

        internal static async Task<List<SearchResult>> HybridSearchAsync(
            SQLiteConnection db, string keywords, string semantic,
            int limit = 5)
        {
            int ftsLimit = limit * 3;

            Color(string.Format(
                "  [search] keywords: \"{0}\"  semantic: \"{1}\"",
                keywords, semantic), ConsoleColor.DarkGray);

            var ftsResults = SearchFts(db, keywords, ftsLimit);
            Color(string.Format("  [fts] {0} results", ftsResults.Count), ConsoleColor.DarkGray);

            var vecResults = new List<SearchResult>();
            try
            {
                using (var embedder = new EmbeddingClient())
                {
                    float[] queryVec = await embedder.EmbedAsync(semantic);
                    vecResults = SearchVector(db, queryVec, ftsLimit);
                }
            }
            catch (Exception ex)
            {
                Color("  [search] Semantic search unavailable: " + ex.Message,
                    ConsoleColor.Yellow);
            }

            Color(string.Format("  [vec] {0} results", vecResults.Count), ConsoleColor.DarkGray);

            // RRF merge
            var scores = new Dictionary<long, RrfEntry>();

            for (int rank = 0; rank < ftsResults.Count; rank++)
            {
                var r = ftsResults[rank];
                if (!scores.ContainsKey(r.Id))
                    scores[r.Id] = new RrfEntry(r);
                scores[r.Id].Rrf += 1.0 / (RrfK + rank + 1);
            }

            for (int rank = 0; rank < vecResults.Count; rank++)
            {
                var r = vecResults[rank];
                if (!scores.ContainsKey(r.Id))
                    scores[r.Id] = new RrfEntry(r);
                scores[r.Id].Rrf += 1.0 / (RrfK + rank + 1);
            }

            var merged = new List<RrfEntry>(scores.Values);
            merged.Sort((a, b) => b.Rrf.CompareTo(a.Rrf));

            Color(string.Format(
                "  [rrf] {0} merged, returning top {1}",
                merged.Count, Math.Min(limit, merged.Count)),
                ConsoleColor.DarkGray);

            var result = new List<SearchResult>();
            for (int i = 0; i < Math.Min(limit, merged.Count); i++)
                result.Add(merged[i].Data);

            return result;
        }

        // ----------------------------------------------------------------
        // FTS5 search
        // ----------------------------------------------------------------

        private static List<SearchResult> SearchFts(
            SQLiteConnection db, string keywords, int limit)
        {
            string ftsQuery = ToFtsQuery(keywords);
            if (ftsQuery == null) return new List<SearchResult>();

            var results = new List<SearchResult>();
            try
            {
                using (var cmd = db.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT c.id, c.content, c.section, c.chunk_index, d.source,
                               rank AS fts_score
                        FROM   chunks_fts
                        JOIN   chunks c    ON c.id = chunks_fts.rowid
                        JOIN   documents d ON d.id = c.document_id
                        WHERE  chunks_fts MATCH @q
                        ORDER  BY rank
                        LIMIT  @lim";
                    cmd.Parameters.AddWithValue("@q",   ftsQuery);
                    cmd.Parameters.AddWithValue("@lim", limit);

                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            results.Add(new SearchResult
                            {
                                Id         = r.GetInt64(0),
                                Content    = r.GetString(1),
                                Section    = r.IsDBNull(2) ? null : r.GetString(2),
                                ChunkIndex = r.GetInt32(3),
                                Source     = r.GetString(4)
                            });
                        }
                    }
                }
            }
            catch
            {
                // FTS query syntax errors — return empty
            }

            return results;
        }

        // ----------------------------------------------------------------
        // Vector (cosine) search — in-memory
        // ----------------------------------------------------------------

        private static List<SearchResult> SearchVector(
            SQLiteConnection db, float[] queryVec, int limit)
        {
            var rows = new List<(long chunkId, float[] vec)>();
            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText = "SELECT chunk_id, embedding FROM chunks_vec";
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        long    id   = r.GetInt64(0);
                        byte[]  blob = (byte[])r["embedding"];
                        float[] vec  = FromBytes(blob);
                        rows.Add((id, vec));
                    }
                }
            }

            if (rows.Count == 0) return new List<SearchResult>();

            var scored = new List<(long id, double score)>();
            foreach (var (id, vec) in rows)
                scored.Add((id, CosineSimilarity(queryVec, vec)));

            scored.Sort((a, b) => b.score.CompareTo(a.score));

            var topIds = new List<long>();
            for (int i = 0; i < Math.Min(limit, scored.Count); i++)
                topIds.Add(scored[i].id);

            if (topIds.Count == 0) return new List<SearchResult>();

            var ph = new StringBuilder();
            for (int i = 0; i < topIds.Count; i++)
            {
                if (i > 0) ph.Append(',');
                ph.Append("@id").Append(i);
            }

            var chunkMap = new Dictionary<long, SearchResult>();
            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText = string.Format(
                    @"SELECT c.id, c.content, c.section, c.chunk_index, d.source
                      FROM   chunks c
                      JOIN   documents d ON d.id = c.document_id
                      WHERE  c.id IN ({0})", ph);

                for (int i = 0; i < topIds.Count; i++)
                    cmd.Parameters.AddWithValue("@id" + i, topIds[i]);

                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        long id = r.GetInt64(0);
                        chunkMap[id] = new SearchResult
                        {
                            Id         = id,
                            Content    = r.GetString(1),
                            Section    = r.IsDBNull(2) ? null : r.GetString(2),
                            ChunkIndex = r.GetInt32(3),
                            Source     = r.GetString(4)
                        };
                    }
                }
            }

            var results = new List<SearchResult>();
            foreach (long id in topIds)
            {
                if (chunkMap.ContainsKey(id))
                    results.Add(chunkMap[id]);
            }

            return results;
        }

        // ----------------------------------------------------------------
        // Helpers
        // ----------------------------------------------------------------

        private static string ToFtsQuery(string query)
        {
            var terms = Regex.Replace(query, @"[^\p{L}\p{N}\s]", " ")
                .Trim()
                .Split(new[] { ' ', '\t', '\n' },
                    StringSplitOptions.RemoveEmptyEntries);

            var valid = new List<string>();
            foreach (var t in terms)
                if (t.Length > 1) valid.Add(t);

            if (valid.Count == 0) return null;

            var sb = new StringBuilder();
            for (int i = 0; i < valid.Count; i++)
            {
                if (i > 0) sb.Append(" OR ");
                sb.Append('"');
                sb.Append(valid[i].Replace("\"", string.Empty));
                sb.Append('"');
            }

            return sb.ToString();
        }

        private static double CosineSimilarity(float[] a, float[] b)
        {
            double dot = 0, normA = 0, normB = 0;
            int len = Math.Min(a.Length, b.Length);
            for (int i = 0; i < len; i++)
            {
                dot   += (double)a[i] * b[i];
                normA += (double)a[i] * a[i];
                normB += (double)b[i] * b[i];
            }
            double denom = Math.Sqrt(normA) * Math.Sqrt(normB);
            return denom == 0 ? 0 : dot / denom;
        }

        private static float[] FromBytes(byte[] bytes)
        {
            var arr = new float[bytes.Length / sizeof(float)];
            Buffer.BlockCopy(bytes, 0, arr, 0, bytes.Length);
            return arr;
        }

        private static void Color(string text, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(text);
            Console.ResetColor();
        }

        // ----------------------------------------------------------------
        // Types
        // ----------------------------------------------------------------

        private sealed class RrfEntry
        {
            public SearchResult Data { get; }
            public double       Rrf  { get; set; }

            public RrfEntry(SearchResult data) { Data = data; }
        }
    }

    // ----------------------------------------------------------------
    // SearchResult — shared with agent
    // ----------------------------------------------------------------

    internal sealed class SearchResult
    {
        public long   Id         { get; set; }
        public string Content    { get; set; }
        public string Section    { get; set; }
        public int    ChunkIndex { get; set; }
        public string Source     { get; set; }
    }
}
