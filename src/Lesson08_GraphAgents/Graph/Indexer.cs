using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Neo4j.Driver;
using FourthDevs.Lesson08_GraphAgents.Helpers;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Lesson08_GraphAgents.Graph
{
    /// <summary>
    /// Workspace indexer: reads files, chunks, embeds, extracts entities,
    /// then writes everything to Neo4j in a single transaction per document.
    /// Mirrors 02_03_graph_agents/src/graph/indexer.js (i-am-alice/4th-devs)
    /// </summary>
    internal static class Indexer
    {
        private static readonly HashSet<string> SupportedExtensions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".md", ".txt" };

        // ── Hash helper ────────────────────────────────────────────────────

        private static string HashContent(string content)
        {
            using (var sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(content));
                var sb = new StringBuilder();
                foreach (byte b in bytes) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        // ── Remove document ────────────────────────────────────────────────

        internal static async Task RemoveDocumentAsync(IDriver driver, string source)
        {
            await Neo4jDriver.WriteQueryAsync(
                driver,
                "MATCH (d:Document {source: $source}) " +
                "OPTIONAL MATCH (d)-[:HAS_CHUNK]->(c:Chunk) " +
                "OPTIONAL MATCH (c)-[:MENTIONS]->(e:Entity) " +
                "DETACH DELETE c, d " +
                "WITH e WHERE e IS NOT NULL " +
                "AND NOT EXISTS { (e)<-[:MENTIONS]-(:Chunk) } " +
                "DETACH DELETE e",
                new { source });
        }

        // ── Core indexing pipeline ─────────────────────────────────────────

        internal struct IndexStats
        {
            internal int  Chunks;
            internal int  Entities;
            internal int  Relationships;
            internal bool Skipped;
        }

        private static async Task<IndexStats> IndexContentAsync(
            IDriver driver, string content, string source)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                Logger.Warn("Skipping empty content: " + source);
                return new IndexStats();
            }

            string hash = HashContent(content);

            // Check if already indexed with same hash
            var existing = await Neo4jDriver.ReadQueryAsync(
                driver,
                "MATCH (d:Document {source: $source}) RETURN d.hash AS hash",
                new { source });

            if (existing.Count > 0 && existing[0]["hash"].As<string>() == hash)
            {
                Logger.Info("Skipping " + source + " (unchanged)");
                return new IndexStats { Skipped = true };
            }

            if (existing.Count > 0)
            {
                Logger.Info("Re-indexing " + source + " (changed)");
                await RemoveDocumentAsync(driver, source);
            }

            // 1. Chunk
            var chunks = Chunking.ChunkBySeparators(content, source);
            Logger.Info(source + ": " + chunks.Count + " chunks");

            // 2. Embed chunks
            List<float[]> chunkEmbeddings;
            using (var embedClient = new EmbeddingClient())
            {
                var chunkTexts = new List<string>();
                foreach (var c in chunks) chunkTexts.Add(c.Content);
                chunkEmbeddings = await embedClient.EmbedBatchAsync(chunkTexts);
            }

            // 3. Extract entities & relationships
            Logger.Start("Extracting entities...");
            var dedup = await Extract.ExtractFromChunksAsync(chunks);

            // 4. Embed unique entities
            var uniqueEntities     = DeduplicateEntities(dedup.Entities);
            List<float[]> entityEmbeddings = new List<float[]>();
            if (uniqueEntities.Count > 0)
            {
                using (var embedClient = new EmbeddingClient())
                {
                    var entityTexts = new List<string>();
                    foreach (var e in uniqueEntities)
                        entityTexts.Add(e.Name + ": " + (e.Description ?? e.Type));
                    entityEmbeddings = await embedClient.EmbedBatchAsync(entityTexts);
                }
            }

            // 5. Write to Neo4j in a single transaction
            await Neo4jDriver.WriteTransactionAsync(driver, async tx =>
            {
                // Document node
                await tx.RunAsync(
                    "CREATE (d:Document {source: $source, hash: $hash, indexedAt: datetime()})",
                    new { source, hash });

                // Chunk nodes + HAS_CHUNK edges
                for (int i = 0; i < chunks.Count; i++)
                {
                    var c = chunks[i];
                    await tx.RunAsync(
                        "MATCH (d:Document {source: $source}) " +
                        "CREATE (d)-[:HAS_CHUNK]->(c:Chunk {" +
                        "content: $content, chunkIndex: $index, section: $section, " +
                        "chars: $chars, source: $source, embedding: $embedding})",
                        new
                        {
                            source,
                            content   = c.Content,
                            index     = c.Index,
                            section   = c.Section ?? string.Empty,
                            chars     = c.Chars,
                            embedding = chunkEmbeddings[i]
                        });
                }

                // Entity nodes (MERGE to deduplicate across sources)
                for (int i = 0; i < uniqueEntities.Count; i++)
                {
                    var e   = uniqueEntities[i];
                    var emb = i < entityEmbeddings.Count ? entityEmbeddings[i] : new float[0];
                    await tx.RunAsync(
                        "MERGE (e:Entity {name: $name, type: $type}) " +
                        "ON CREATE SET e.description = $description, " +
                        "              e.aliases_text = $name, " +
                        "              e.embedding = $embedding " +
                        "ON MATCH SET  e.description = CASE WHEN size(e.description) < size($description) " +
                        "                                   THEN $description ELSE e.description END",
                        new
                        {
                            name        = e.Name,
                            type        = e.Type,
                            description = e.Description ?? string.Empty,
                            embedding   = emb
                        });
                }

                // MENTIONS edges: Chunk → Entity
                foreach (var kv in dedup.ChunkEntities)
                {
                    int chunkIdx = kv.Key;
                    foreach (string eName in kv.Value)
                    {
                        await tx.RunAsync(
                            "MATCH (c:Chunk {source: $source, chunkIndex: $chunkIdx}) " +
                            "MATCH (e:Entity {name: $eName}) " +
                            "MERGE (c)-[:MENTIONS]->(e)",
                            new { source, chunkIdx, eName });
                    }
                }

                // RELATED_TO edges between entities
                foreach (var rel in dedup.Relationships)
                {
                    await tx.RunAsync(
                        "MATCH (a:Entity {name: $src}) " +
                        "MATCH (b:Entity {name: $tgt}) " +
                        "MERGE (a)-[r:RELATED_TO {type: $type}]->(b) " +
                        "ON CREATE SET r.description = $description, r.evidenceSource = $evidenceSource",
                        new
                        {
                            src           = rel.Source,
                            tgt           = rel.Target,
                            type          = rel.Type,
                            description   = rel.Description ?? string.Empty,
                            evidenceSource = source
                        });
                }
            });

            var stats = new IndexStats
            {
                Chunks        = chunks.Count,
                Entities      = uniqueEntities.Count,
                Relationships = dedup.Relationships.Count
            };

            Logger.Success(string.Format(
                "Indexed {0}: {1} chunks, {2} entities, {3} relationships",
                source, stats.Chunks, stats.Entities, stats.Relationships));

            return stats;
        }

        // ── Public indexing methods ────────────────────────────────────────

        internal static async Task<IndexStats> IndexFileAsync(
            IDriver driver, string filePath, string fileName)
        {
            string content = File.ReadAllText(filePath, Encoding.UTF8);
            return await IndexContentAsync(driver, content, fileName);
        }

        internal static async Task<IndexStats> IndexTextAsync(
            IDriver driver, string text, string source)
        {
            return await IndexContentAsync(driver, text, source);
        }

        // ── Entity deduplication ───────────────────────────────────────────

        private static List<Extract.EntityInfo> DeduplicateEntities(
            List<Extract.EntityInfo> entities)
        {
            var map = new Dictionary<string, Extract.EntityInfo>();
            foreach (var e in entities)
            {
                string key = e.Name + "::" + e.Type;
                if (!map.ContainsKey(key) ||
                    (e.Description?.Length ?? 0) > (map[key].Description?.Length ?? 0))
                {
                    map[key] = e;
                }
            }
            return new List<Extract.EntityInfo>(map.Values);
        }

        // ── Workspace indexing ─────────────────────────────────────────────

        internal static async Task IndexWorkspaceAsync(IDriver driver, string workspacePath)
        {
            Directory.CreateDirectory(workspacePath);

            var allFiles = Directory.GetFiles(workspacePath);
            var supported = new List<string>();
            foreach (string f in allFiles)
            {
                string ext = Path.GetExtension(f);
                if (SupportedExtensions.Contains(ext))
                    supported.Add(f);
            }

            if (supported.Count == 0)
            {
                Logger.Warn("No .md/.txt files found in " + workspacePath);
                return;
            }

            Logger.Info(string.Format(
                "Found {0} file(s) in {1}", supported.Count, workspacePath));

            foreach (string filePath in supported)
            {
                string fileName = Path.GetFileName(filePath);
                await IndexFileAsync(driver, filePath, fileName);
            }

            // Prune documents no longer on disk
            var indexed = await Neo4jDriver.ReadQueryAsync(
                driver, "MATCH (d:Document) RETURN d.source AS source");

            var onDisk = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string fp in supported)
                onDisk.Add(Path.GetFileName(fp));

            foreach (var record in indexed)
            {
                string src = record["source"].As<string>();
                if (!onDisk.Contains(src))
                {
                    Logger.Info("Removing stale index: " + src);
                    await RemoveDocumentAsync(driver, src);
                }
            }
        }

        // ── Graph management ───────────────────────────────────────────────

        internal static async Task ClearGraphAsync(IDriver driver)
        {
            var records = await Neo4jDriver.WriteQueryAsync(
                driver,
                "MATCH (n) DETACH DELETE n RETURN count(n) AS deleted");
            long deleted = records.Count > 0
                ? records[0]["deleted"].As<long>()
                : 0;
            Logger.Info("Cleared " + deleted + " nodes");
        }

        internal static async Task<JObject> AuditGraphAsync(IDriver driver)
        {
            var tasks = new Task<IList<IRecord>>[]
            {
                Neo4jDriver.ReadQueryAsync(driver,
                    "MATCH (n) WITH labels(n)[0] AS label, count(n) AS count " +
                    "RETURN label, count ORDER BY count DESC"),

                Neo4jDriver.ReadQueryAsync(driver,
                    "MATCH (e:Entity) WHERE NOT (e)-[:RELATED_TO]-() " +
                    "RETURN e.name AS name, e.type AS type"),

                Neo4jDriver.ReadQueryAsync(driver,
                    "MATCH (a:Entity), (b:Entity) " +
                    "WHERE id(a) < id(b) AND a.type = b.type " +
                    "AND (a.name CONTAINS b.name OR b.name CONTAINS a.name) " +
                    "AND a.name <> b.name " +
                    "RETURN a.name AS a, b.name AS b, a.type AS type " +
                    "LIMIT 20"),

                Neo4jDriver.ReadQueryAsync(driver,
                    "MATCH ()-[r:RELATED_TO]->() " +
                    "RETURN r.type AS type, count(r) AS count ORDER BY count DESC"),

                Neo4jDriver.ReadQueryAsync(driver,
                    "MATCH (e:Entity) " +
                    "RETURN e.type AS type, count(e) AS count ORDER BY count DESC")
            };

            await Task.WhenAll(tasks);

            IList<IRecord> counts       = tasks[0].Result;
            IList<IRecord> orphans      = tasks[1].Result;
            IList<IRecord> duplicates   = tasks[2].Result;
            IList<IRecord> relTypes     = tasks[3].Result;
            IList<IRecord> entityTypes  = tasks[4].Result;

            var nodeCounts = new JArray();
            foreach (var r in counts)
                nodeCounts.Add(new JObject
                {
                    ["label"] = r["label"].As<string>(),
                    ["count"] = r["count"].As<long>()
                });

            var orphanEntities = new JArray();
            foreach (var r in orphans)
                orphanEntities.Add(new JObject
                {
                    ["name"] = r["name"].As<string>(),
                    ["type"] = r["type"].As<string>()
                });

            var potentialDuplicates = new JArray();
            foreach (var r in duplicates)
                potentialDuplicates.Add(new JObject
                {
                    ["a"]    = r["a"].As<string>(),
                    ["b"]    = r["b"].As<string>(),
                    ["type"] = r["type"].As<string>()
                });

            var relationshipTypes = new JArray();
            foreach (var r in relTypes)
                relationshipTypes.Add(new JObject
                {
                    ["type"]  = r["type"].As<string>(),
                    ["count"] = r["count"].As<long>()
                });

            var entityTypeCounts = new JArray();
            foreach (var r in entityTypes)
                entityTypeCounts.Add(new JObject
                {
                    ["type"]  = r["type"].As<string>(),
                    ["count"] = r["count"].As<long>()
                });

            return new JObject
            {
                ["nodeCounts"]          = nodeCounts,
                ["orphanEntities"]      = orphanEntities,
                ["potentialDuplicates"] = potentialDuplicates,
                ["relationshipTypes"]   = relationshipTypes,
                ["entityTypes"]         = entityTypeCounts
            };
        }

        /// <summary>
        /// Merge sourceName entity into targetName, deleting the source.
        /// Returns (merged, into) on success, or null if not found.
        /// </summary>
        internal static async Task<(string merged, string into)?> MergeEntitiesAsync(
            IDriver driver, string sourceName, string targetName)
        {
            (string merged, string into)? result = null;

            var session = driver.AsyncSession();
            try
            {
                result = await session.ExecuteWriteAsync<(string, string)?>(async tx =>
                {
                    // Verify both exist
                    var check = await tx.RunAsync(
                        "MATCH (s:Entity) WHERE toLower(s.name) = toLower($source) " +
                        "MATCH (t:Entity) WHERE toLower(t.name) = toLower($target) " +
                        "RETURN s.name AS sName, t.name AS tName",
                        new { source = sourceName, target = targetName });

                    var checkList = await check.ToListAsync();
                    if (checkList.Count == 0) return null;

                    // Rewire MENTIONS
                    await (await tx.RunAsync(
                        "MATCH (c:Chunk)-[old:MENTIONS]->(s:Entity) " +
                        "WHERE toLower(s.name) = toLower($source) " +
                        "MATCH (t:Entity) WHERE toLower(t.name) = toLower($target) " +
                        "MERGE (c)-[:MENTIONS]->(t) DELETE old",
                        new { source = sourceName, target = targetName })).ConsumeAsync();

                    // Rewire outgoing RELATED_TO
                    await (await tx.RunAsync(
                        "MATCH (s:Entity)-[old:RELATED_TO]->(other:Entity) " +
                        "WHERE toLower(s.name) = toLower($source) " +
                        "MATCH (t:Entity) WHERE toLower(t.name) = toLower($target) " +
                        "MERGE (t)-[r:RELATED_TO {type: old.type}]->(other) " +
                        "ON CREATE SET r.description = old.description, r.evidenceSource = old.evidenceSource " +
                        "DELETE old",
                        new { source = sourceName, target = targetName })).ConsumeAsync();

                    // Rewire incoming RELATED_TO
                    await (await tx.RunAsync(
                        "MATCH (other:Entity)-[old:RELATED_TO]->(s:Entity) " +
                        "WHERE toLower(s.name) = toLower($source) " +
                        "MATCH (t:Entity) WHERE toLower(t.name) = toLower($target) " +
                        "MERGE (other)-[r:RELATED_TO {type: old.type}]->(t) " +
                        "ON CREATE SET r.description = old.description, r.evidenceSource = old.evidenceSource " +
                        "DELETE old",
                        new { source = sourceName, target = targetName })).ConsumeAsync();

                    // Delete source entity
                    await (await tx.RunAsync(
                        "MATCH (s:Entity) WHERE toLower(s.name) = toLower($source) DETACH DELETE s",
                        new { source = sourceName })).ConsumeAsync();

                    return (checkList[0]["sName"].As<string>(),
                            checkList[0]["tName"].As<string>());
                });
            }
            finally
            {
                await session.CloseAsync();
            }

            return result;
        }
    }
}
