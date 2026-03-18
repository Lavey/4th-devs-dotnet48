using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Neo4j.Driver;
using FourthDevs.Lesson08_GraphAgents.Helpers;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Lesson08_GraphAgents.Graph
{
    /// <summary>
    /// Hybrid search over Neo4j: full-text (BM25) + vector (cosine) + RRF fusion.
    /// Also provides graph traversal helpers for agent tools.
    /// Mirrors 02_03_graph_agents/src/graph/search.js (i-am-alice/4th-devs)
    /// </summary>
    internal static class Search
    {
        private const int RrfK = 60;

        // ── Search result model ────────────────────────────────────────────

        internal sealed class ChunkResult
        {
            internal string  Content;
            internal string  Section;
            internal int     ChunkIndex;
            internal string  Source;
            internal double  Rrf;
            internal int?    FtsRank;
            internal int?    VecRank;
        }

        // ── Full-text search ───────────────────────────────────────────────

        private static async Task<List<(string content, string section, int chunkIndex, string source, double ftsScore)>>
            SearchFullTextAsync(IDriver driver, string query, int limit = 10)
        {
            var list = new List<(string, string, int, string, double)>();
            if (string.IsNullOrWhiteSpace(query)) return list;

            try
            {
                var records = await Neo4jDriver.ReadQueryAsync(
                    driver,
                    "CALL db.index.fulltext.queryNodes(\"chunk_content_ft\", $query, {limit: $limit}) " +
                    "YIELD node, score " +
                    "RETURN node.content AS content, node.section AS section, " +
                    "       node.chunkIndex AS chunkIndex, node.source AS source, score",
                    new { query, limit });

                foreach (var r in records)
                {
                    int idx = r["chunkIndex"].As<int>();
                    list.Add((
                        r["content"].As<string>(),
                        r["section"].As<string>(),
                        idx,
                        r["source"].As<string>(),
                        r["score"].As<double>()
                    ));
                }
            }
            catch { /* index may not exist yet */ }

            return list;
        }

        // ── Vector search ──────────────────────────────────────────────────

        private static async Task<List<(string content, string section, int chunkIndex, string source, double vecScore)>>
            SearchVectorAsync(IDriver driver, float[] queryEmbedding, int limit = 10)
        {
            var list = new List<(string, string, int, string, double)>();

            var records = await Neo4jDriver.ReadQueryAsync(
                driver,
                "CALL db.index.vector.queryNodes(\"chunk_embedding_vec\", $limit, $embedding) " +
                "YIELD node, score " +
                "RETURN node.content AS content, node.section AS section, " +
                "       node.chunkIndex AS chunkIndex, node.source AS source, score",
                new { embedding = queryEmbedding, limit });

            foreach (var r in records)
            {
                int idx = r["chunkIndex"].As<int>();
                list.Add((
                    r["content"].As<string>(),
                    r["section"].As<string>(),
                    idx,
                    r["source"].As<string>(),
                    r["score"].As<double>()
                ));
            }

            return list;
        }

        // ── Hybrid search with RRF ─────────────────────────────────────────

        internal static async Task<List<ChunkResult>> HybridSearchAsync(
            IDriver driver, string keywords, string semantic, int limit = 5)
        {
            int ftsLimit = limit * 3;

            Logger.SearchHeader(keywords, semantic);

            // Full-text search
            var ftsResults = await SearchFullTextAsync(driver, keywords, ftsLimit);
            Logger.SearchFts(ftsResults);

            // Vector search
            var vecResults = new List<(string, string, int, string, double)>();
            try
            {
                using (var embedClient = new EmbeddingClient())
                {
                    float[] embedding = await embedClient.EmbedAsync(semantic);
                    vecResults = await SearchVectorAsync(driver, embedding, ftsLimit);
                }
            }
            catch (Exception ex)
            {
                Logger.Warn("Semantic search unavailable: " + ex.Message);
            }
            Logger.SearchVec(vecResults);

            // RRF fusion
            var scores = new Dictionary<string, ChunkResult>();

            string MakeKey(string src, int chunkIdx) => src + "::" + chunkIdx;

            for (int rank = 0; rank < ftsResults.Count; rank++)
            {
                var r   = ftsResults[rank];
                string key = MakeKey(r.Item4, r.Item3);
                if (!scores.ContainsKey(key))
                    scores[key] = new ChunkResult
                    {
                        Content    = r.Item1,
                        Section    = r.Item2,
                        ChunkIndex = r.Item3,
                        Source     = r.Item4
                    };
                scores[key].Rrf     += 1.0 / (RrfK + rank + 1);
                scores[key].FtsRank  = rank + 1;
            }

            for (int rank = 0; rank < vecResults.Count; rank++)
            {
                var r   = vecResults[rank];
                string key = MakeKey(r.Item4, r.Item3);
                if (!scores.ContainsKey(key))
                    scores[key] = new ChunkResult
                    {
                        Content    = r.Item1,
                        Section    = r.Item2,
                        ChunkIndex = r.Item3,
                        Source     = r.Item4
                    };
                scores[key].Rrf     += 1.0 / (RrfK + rank + 1);
                scores[key].VecRank  = rank + 1;
            }

            var merged = new List<ChunkResult>(scores.Values);
            merged.Sort((a, b) => b.Rrf.CompareTo(a.Rrf));

            if (merged.Count > limit)
                merged = merged.GetRange(0, limit);

            Logger.SearchRrf(merged);

            return merged;
        }

        // ── Entity enrichment ──────────────────────────────────────────────

        internal sealed class EntityRef
        {
            internal string Name;
            internal string Type;
        }

        internal static async Task<(Dictionary<string, List<EntityRef>> chunkEntities,
                                    List<EntityRef> allEntities)>
            GetEntitiesForChunksAsync(IDriver driver, List<ChunkResult> chunks)
        {
            var chunkEntities = new Dictionary<string, List<EntityRef>>();
            var allEntities   = new Dictionary<string, EntityRef>();

            if (chunks.Count == 0)
                return (chunkEntities, new List<EntityRef>(allEntities.Values));

            // Build parameter list
            var chunkParams = new JArray();
            foreach (var c in chunks)
                chunkParams.Add(new JObject
                {
                    ["source"]     = c.Source,
                    ["chunkIndex"] = c.ChunkIndex
                });

            var records = await Neo4jDriver.ReadQueryAsync(
                driver,
                "UNWIND $chunks AS c " +
                "MATCH (ch:Chunk {source: c.source, chunkIndex: c.chunkIndex})-[:MENTIONS]->(e:Entity) " +
                "RETURN c.source AS source, c.chunkIndex AS chunkIndex, " +
                "       collect(DISTINCT {name: e.name, type: e.type}) AS entities",
                new { chunks = chunkParams });

            foreach (var r in records)
            {
                string src = r["source"].As<string>();
                int    idx = r["chunkIndex"].As<int>();
                string key = src + "::" + idx;

                var entities    = r["entities"].As<List<IDictionary<string, object>>>();
                var entityRefs  = new List<EntityRef>();

                foreach (var e in entities)
                {
                    string name = e["name"]?.ToString() ?? string.Empty;
                    string type = e["type"]?.ToString() ?? string.Empty;
                    var    er   = new EntityRef { Name = name, Type = type };
                    entityRefs.Add(er);
                    if (!allEntities.ContainsKey(name))
                        allEntities[name] = er;
                }

                chunkEntities[key] = entityRefs;
            }

            return (chunkEntities, new List<EntityRef>(allEntities.Values));
        }

        // ── Graph traversal helpers ────────────────────────────────────────

        internal sealed class NeighborResult
        {
            internal string Name;
            internal string Type;
            internal string Description;
            internal List<NeighborEntry> Neighbors = new List<NeighborEntry>();
        }

        internal sealed class NeighborEntry
        {
            internal string Entity;
            internal string EntityType;
            internal string RelType;
            internal string RelDescription;
            internal string EvidenceSource;
            internal string Direction;
        }

        internal static async Task<NeighborResult> GetNeighborsAsync(
            IDriver driver, string entityName, int limit = 20)
        {
            var records = await Neo4jDriver.ReadQueryAsync(
                driver,
                "MATCH (e:Entity) WHERE toLower(e.name) = toLower($name) " +
                "OPTIONAL MATCH (e)-[r:RELATED_TO]-(other:Entity) " +
                "RETURN e.name AS name, e.type AS type, e.description AS description, " +
                "       collect(DISTINCT {" +
                "         entity: other.name, entityType: other.type, " +
                "         relType: r.type, relDescription: r.description, " +
                "         evidenceSource: r.evidenceSource, " +
                "         direction: CASE WHEN startNode(r) = e THEN 'outgoing' ELSE 'incoming' END" +
                "       })[0..$limit] AS neighbors",
                new { name = entityName, limit });

            if (records.Count == 0) return null;

            var r0      = records[0];
            var result  = new NeighborResult
            {
                Name        = r0["name"].As<string>(),
                Type        = r0["type"].As<string>(),
                Description = r0["description"].As<string>()
            };

            var neighbors = r0["neighbors"].As<List<IDictionary<string, object>>>();
            foreach (var n in neighbors)
            {
                if (n["entity"] == null) continue;
                result.Neighbors.Add(new NeighborEntry
                {
                    Entity        = n["entity"]?.ToString(),
                    EntityType    = n["entityType"]?.ToString(),
                    RelType       = n["relType"]?.ToString(),
                    RelDescription = n["relDescription"]?.ToString(),
                    EvidenceSource = n["evidenceSource"]?.ToString(),
                    Direction      = n["direction"]?.ToString()
                });
            }

            return result;
        }

        internal sealed class PathResult
        {
            internal List<JObject> Nodes = new List<JObject>();
            internal List<JObject> Edges = new List<JObject>();
        }

        internal static async Task<List<PathResult>> FindPathsAsync(
            IDriver driver, string fromEntity, string toEntity, int maxDepth = 4)
        {
            // Neo4j Cypher shortestPath requires literal depth in the pattern
            // We build the query with the depth embedded as a literal
            string cypher =
                "MATCH (a:Entity), (b:Entity) " +
                "WHERE toLower(a.name) = toLower($from) AND toLower(b.name) = toLower($to) " +
                "MATCH path = shortestPath((a)-[:RELATED_TO*1.." + maxDepth + "]-(b)) " +
                "RETURN [n IN nodes(path) | {name: n.name, type: n.type}] AS nodes, " +
                "       [r IN relationships(path) | {type: r.type, description: r.description, " +
                "                                     evidenceSource: r.evidenceSource}] AS edges " +
                "LIMIT 3";

            var records = await Neo4jDriver.ReadQueryAsync(
                driver, cypher, new { from = fromEntity, to = toEntity });

            var paths = new List<PathResult>();
            foreach (var r in records)
            {
                var pathResult = new PathResult();

                var nodes = r["nodes"].As<List<IDictionary<string, object>>>();
                foreach (var n in nodes)
                    pathResult.Nodes.Add(new JObject
                    {
                        ["name"] = n["name"]?.ToString(),
                        ["type"] = n["type"]?.ToString()
                    });

                var edges = r["edges"].As<List<IDictionary<string, object>>>();
                foreach (var e in edges)
                    pathResult.Edges.Add(new JObject
                    {
                        ["type"]           = e["type"]?.ToString(),
                        ["description"]    = e["description"]?.ToString(),
                        ["evidenceSource"] = e["evidenceSource"]?.ToString()
                    });

                paths.Add(pathResult);
            }

            return paths;
        }

        internal static async Task<JArray> SafeReadCypherAsync(
            IDriver driver, string cypher, JObject parameters = null, int limit = 25)
        {
            string upper = cypher.ToUpperInvariant();
            var writeKeywords = new[] { "CREATE", "MERGE", "DELETE", "SET ", "REMOVE", "DROP", "CALL {" };
            foreach (string kw in writeKeywords)
            {
                if (upper.Contains(kw))
                    throw new InvalidOperationException(
                        "Write operations are not allowed in read-only Cypher tool");
            }

            string safeCypher = !upper.Contains("LIMIT")
                ? cypher + "\nLIMIT " + limit
                : cypher;

            // Build parameter dict from JObject
            var paramDict = new Dictionary<string, object>();
            if (parameters != null)
                foreach (var kv in parameters)
                    paramDict[kv.Key] = kv.Value.ToObject<object>();

            var records = await Neo4jDriver.ReadQueryAsync(driver, safeCypher, paramDict);

            var result = new JArray();
            foreach (var r in records)
            {
                var obj = new JObject();
                foreach (string key in r.Keys)
                {
                    var val = r[key];
                    obj[key] = val == null ? JValue.CreateNull()
                                           : new JValue(val.As<object>());
                }
                result.Add(obj);
            }

            return result;
        }
    }
}
