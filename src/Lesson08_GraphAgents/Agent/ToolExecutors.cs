using System;
using System.IO;
using System.Threading.Tasks;
using Neo4j.Driver;
using FourthDevs.Lesson08_GraphAgents.Graph;
using FourthDevs.Lesson08_GraphAgents.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Lesson08_GraphAgents.Agent
{
    /// <summary>
    /// Tool execution handlers for all graph RAG tools.
    /// Mirrors the handlers in 02_03_graph_agents/src/agent/tools.js (i-am-alice/4th-devs)
    /// </summary>
    internal static class ToolExecutors
    {
        private static readonly string WorkspaceDir = "workspace";

        internal static async Task<string> ExecuteAsync(
            IDriver driver, string name, JObject args)
        {
            Logger.Tool(name, args.ToString(Formatting.None));

            try
            {
                string result = await RunAsync(driver, name, args);
                Logger.ToolResult(name, true, result);
                return result;
            }
            catch (Exception ex)
            {
                string error = JsonConvert.SerializeObject(new { error = ex.Message });
                Logger.ToolResult(name, false, ex.Message);
                return error;
            }
        }

        private static async Task<string> RunAsync(IDriver driver, string name, JObject args)
        {
            switch (name)
            {
                case "search":    return await SearchAsync(driver, args);
                case "explore":   return await ExploreAsync(driver, args);
                case "connect":   return await ConnectAsync(driver, args);
                case "cypher":    return await CypherAsync(driver, args);
                case "learn":     return await LearnAsync(driver, args);
                case "forget":    return await ForgetAsync(driver, args);
                case "merge_entities": return await MergeEntitiesAsync(driver, args);
                case "audit":     return await AuditAsync(driver);
                default:
                    return JsonConvert.SerializeObject(new { error = "Unknown tool: " + name });
            }
        }

        // ── Retrieval tools ────────────────────────────────────────────────

        private static async Task<string> SearchAsync(IDriver driver, JObject args)
        {
            string keywords = args["keywords"]?.Value<string>() ?? string.Empty;
            string semantic = args["semantic"]?.Value<string>() ?? string.Empty;
            int    limit    = Math.Min(args["limit"]?.Value<int>() ?? 5, 20);

            var chunks = await Search.HybridSearchAsync(driver, keywords, semantic, limit);
            var (chunkEntities, allEntities) = await Search.GetEntitiesForChunksAsync(driver, chunks);

            var resultChunks = new JArray();
            foreach (var c in chunks)
            {
                string key = c.Source + "::" + c.ChunkIndex;
                var entities = new JArray();
                if (chunkEntities.ContainsKey(key))
                    foreach (var e in chunkEntities[key])
                        entities.Add(e.Name);

                resultChunks.Add(new JObject
                {
                    ["source"]   = c.Source,
                    ["section"]  = c.Section ?? string.Empty,
                    ["content"]  = c.Content,
                    ["entities"] = entities
                });
            }

            var entitiesArr = new JArray();
            foreach (var e in allEntities)
                entitiesArr.Add(new JObject
                {
                    ["name"] = e.Name,
                    ["type"] = e.Type
                });

            return JsonConvert.SerializeObject(new JObject
            {
                ["chunks"]   = resultChunks,
                ["entities"] = entitiesArr
            });
        }

        private static async Task<string> ExploreAsync(IDriver driver, JObject args)
        {
            string entity = args["entity"]?.Value<string>() ?? string.Empty;
            int    limit  = Math.Min(args["limit"]?.Value<int>() ?? 20, 50);

            var result = await Search.GetNeighborsAsync(driver, entity, limit);
            if (result == null)
                return JsonConvert.SerializeObject(new
                {
                    error = string.Format(
                        "Entity \"{0}\" not found in graph. Check spelling or use search first.", entity)
                });

            var neighbors = new JArray();
            foreach (var n in result.Neighbors)
                neighbors.Add(new JObject
                {
                    ["entity"]        = n.Entity,
                    ["entityType"]    = n.EntityType,
                    ["relType"]       = n.RelType,
                    ["relDescription"] = n.RelDescription,
                    ["evidenceSource"] = n.EvidenceSource,
                    ["direction"]      = n.Direction
                });

            return JsonConvert.SerializeObject(new JObject
            {
                ["name"]        = result.Name,
                ["type"]        = result.Type,
                ["description"] = result.Description,
                ["neighbors"]   = neighbors
            });
        }

        private static async Task<string> ConnectAsync(IDriver driver, JObject args)
        {
            string from     = args["from"]?.Value<string>() ?? string.Empty;
            string to       = args["to"]?.Value<string>() ?? string.Empty;
            int    maxDepth = Math.Min(args["maxDepth"]?.Value<int>() ?? 4, 6);

            var paths = await Search.FindPathsAsync(driver, from, to, maxDepth);
            if (paths.Count == 0)
                return JsonConvert.SerializeObject(new
                {
                    error = string.Format(
                        "No path found between \"{0}\" and \"{1}\" within {2} hops.", from, to, maxDepth)
                });

            var pathsArr = new JArray();
            foreach (var p in paths)
                pathsArr.Add(new JObject
                {
                    ["nodes"] = new JArray(p.Nodes.ToArray()),
                    ["edges"] = new JArray(p.Edges.ToArray())
                });

            return JsonConvert.SerializeObject(new JObject { ["paths"] = pathsArr });
        }

        private static async Task<string> CypherAsync(IDriver driver, JObject args)
        {
            string query  = args["query"]?.Value<string>() ?? string.Empty;
            var    @params = args["params"] as JObject;

            var result = await Search.SafeReadCypherAsync(driver, query, @params);
            return result.ToString(Formatting.None);
        }

        // ── Curation tools ─────────────────────────────────────────────────

        private static async Task<string> LearnAsync(IDriver driver, JObject args)
        {
            string filename = args["filename"]?.Value<string>();
            string text     = args["text"]?.Value<string>();
            string source   = args["source"]?.Value<string>();

            if (!string.IsNullOrEmpty(filename))
            {
                // Index a file from workspace
                string filePath = Path.Combine(WorkspaceDir, filename);
                if (!File.Exists(filePath))
                {
                    string available = string.Join(", ", Directory.GetFiles(WorkspaceDir,
                        "*.*", SearchOption.TopDirectoryOnly));
                    return JsonConvert.SerializeObject(new
                    {
                        error = string.Format("File \"{0}\" not found in workspace/. Available: {1}",
                            filename, available)
                    });
                }

                var stats = await Indexer.IndexFileAsync(driver, filePath, filename);
                if (stats.Skipped)
                    return JsonConvert.SerializeObject(new
                    {
                        success = true,
                        message = string.Format("\"{0}\" already indexed (unchanged)", filename)
                    });

                return JsonConvert.SerializeObject(new
                {
                    success = true,
                    message = string.Format("Indexed \"{0}\": {1} chunks, {2} entities, {3} relationships",
                        filename, stats.Chunks, stats.Entities, stats.Relationships)
                });
            }

            if (!string.IsNullOrEmpty(text))
            {
                if (string.IsNullOrWhiteSpace(source))
                    return JsonConvert.SerializeObject(new
                    {
                        error = "Source label is required when indexing raw text"
                    });

                if (string.IsNullOrWhiteSpace(text))
                    return JsonConvert.SerializeObject(new { error = "Text content is empty" });

                var stats = await Indexer.IndexTextAsync(driver, text, source);
                if (stats.Skipped)
                    return JsonConvert.SerializeObject(new
                    {
                        success = true,
                        message = string.Format("\"{0}\" already indexed (unchanged)", source)
                    });

                return JsonConvert.SerializeObject(new
                {
                    success = true,
                    message = string.Format("Indexed \"{0}\": {1} chunks, {2} entities, {3} relationships",
                        source, stats.Chunks, stats.Entities, stats.Relationships)
                });
            }

            return JsonConvert.SerializeObject(new
            {
                error = "Provide either 'filename' to index a file, or 'text' + 'source' to index raw text"
            });
        }

        private static async Task<string> ForgetAsync(IDriver driver, JObject args)
        {
            string source = args["source"]?.Value<string>() ?? string.Empty;
            await Indexer.RemoveDocumentAsync(driver, source);
            return JsonConvert.SerializeObject(new
            {
                success = true,
                message = string.Format("Removed \"{0}\" and its data from the graph", source)
            });
        }

        private static async Task<string> MergeEntitiesAsync(IDriver driver, JObject args)
        {
            string source = args["source"]?.Value<string>() ?? string.Empty;
            string target = args["target"]?.Value<string>() ?? string.Empty;

            var result = await Indexer.MergeEntitiesAsync(driver, source, target);
            if (!result.HasValue)
                return JsonConvert.SerializeObject(new
                {
                    error = string.Format("One or both entities not found: \"{0}\", \"{1}\"", source, target)
                });

            return JsonConvert.SerializeObject(new
            {
                success = true,
                message = string.Format("Merged \"{0}\" into \"{1}\"", result.Value.merged, result.Value.into)
            });
        }

        private static async Task<string> AuditAsync(IDriver driver)
        {
            var report = await Indexer.AuditGraphAsync(driver);
            return report.ToString(Formatting.None);
        }
    }
}
