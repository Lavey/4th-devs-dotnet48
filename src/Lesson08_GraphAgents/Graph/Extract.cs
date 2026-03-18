using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using FourthDevs.Common;
using FourthDevs.Lesson08_GraphAgents.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Lesson08_GraphAgents.Graph
{
    /// <summary>
    /// Entity and relationship extraction from text chunks using an LLM.
    /// Mirrors 02_03_graph_agents/src/graph/extract.js (i-am-alice/4th-devs)
    /// </summary>
    internal static class Extract
    {
        private const string ExtractionModel = "gpt-5-mini";

        private static readonly HashSet<string> EntityTypes = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase)
        {
            "concept", "person", "technology", "organization", "technique", "other"
        };

        private static readonly HashSet<string> RelationshipTypes = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase)
        {
            "relates_to", "uses", "part_of", "created_by",
            "influences", "contrasts_with", "example_of", "depends_on"
        };

        private static readonly HashSet<string> Acronyms = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase)
        {
            "LLM", "LLMs", "GPT", "API", "JSON", "XML", "YML", "CoT", "HTML", "URL", "ID"
        };

        private const string ExtractionInstructions =
            "You are an entity and relationship extractor. Given a text chunk, extract " +
            "structured knowledge.\n\n" +
            "## OUTPUT FORMAT\n" +
            "Return ONLY valid JSON — no markdown fences, no explanation:\n\n" +
            "{\"entities\":[{\"name\":\"Exact Name\",\"type\":\"concept|person|technology|" +
            "organization|technique|other\",\"description\":\"One-sentence description\"}]," +
            "\"relationships\":[{\"source\":\"Entity A name\",\"target\":\"Entity B name\"," +
            "\"type\":\"relates_to|uses|part_of|created_by|influences|contrasts_with|" +
            "example_of|depends_on\",\"description\":\"Brief description\"}]}\n\n" +
            "## RULES\n" +
            "- Extract concrete, meaningful entities\n" +
            "- Normalize entity names: canonical/full form (e.g. \"GPT-4\" not \"gpt4\")\n" +
            "- Use SINGULAR form (e.g. \"Token\" not \"Tokens\")\n" +
            "- Each relationship MUST reference entities in the entities array\n" +
            "- Source and target MUST be DIFFERENT entities\n" +
            "- ONLY use relationship types from the allowed list\n" +
            "- Extract 3-15 entities per chunk\n" +
            "- If no meaningful entities, return {\"entities\":[],\"relationships\":[]}";

        // ── Entity / Relationship models ───────────────────────────────────

        internal sealed class EntityInfo
        {
            internal string Name;
            internal string Type;
            internal string Description;
        }

        internal sealed class RelationshipInfo
        {
            internal string Source;
            internal string Target;
            internal string Type;
            internal string Description;
        }

        internal sealed class ExtractionResult
        {
            internal List<EntityInfo>       Entities      = new List<EntityInfo>();
            internal List<RelationshipInfo> Relationships = new List<RelationshipInfo>();
        }

        // ── Deduplication result ───────────────────────────────────────────

        internal sealed class DedupResult
        {
            internal List<EntityInfo>                  Entities      = new List<EntityInfo>();
            internal List<RelationshipInfo>             Relationships = new List<RelationshipInfo>();
            internal Dictionary<int, List<string>>     ChunkEntities = new Dictionary<int, List<string>>();
        }

        // ── Name normalization ─────────────────────────────────────────────

        private static string TitleCase(string str)
        {
            if (string.IsNullOrEmpty(str)) return str;
            var words = str.Split(' ');
            var result = new System.Text.StringBuilder();
            foreach (string word in words)
            {
                if (result.Length > 0) result.Append(' ');
                if (Acronyms.Contains(word))
                    result.Append(word.ToUpperInvariant());
                else if (word.Length > 0)
                    result.Append(char.ToUpperInvariant(word[0])).Append(word.Substring(1).ToLowerInvariant());
            }
            return result.ToString();
        }

        private static string Singularize(string str)
        {
            if (str.EndsWith("ies", StringComparison.OrdinalIgnoreCase))
                return str.Substring(0, str.Length - 3) + "y";
            if (str.EndsWith("s", StringComparison.OrdinalIgnoreCase) &&
                !str.EndsWith("ss", StringComparison.OrdinalIgnoreCase))
                return str.Substring(0, str.Length - 1);
            return str;
        }

        private static string DedupeKey(string name) =>
            Singularize(name.Trim().ToLowerInvariant());

        // ── Normalization ──────────────────────────────────────────────────

        private static ExtractionResult Normalize(JObject raw)
        {
            var result  = new ExtractionResult();
            var nameMap = new Dictionary<string, string>();

            var entitiesArr = raw["entities"] as JArray ?? new JArray();
            foreach (var e in entitiesArr)
            {
                string rawName = e["name"]?.Value<string>()?.Trim() ?? string.Empty;
                string rawType = e["type"]?.Value<string>() ?? string.Empty;
                if (rawName.Length <= 1) continue;

                string normalized = TitleCase(rawName);
                nameMap[rawName] = normalized;

                result.Entities.Add(new EntityInfo
                {
                    Name        = normalized,
                    Type        = EntityTypes.Contains(rawType) ? rawType.ToLowerInvariant() : "other",
                    Description = e["description"]?.Value<string>() ?? string.Empty
                });
            }

            var entityNames = new HashSet<string>();
            foreach (var e in result.Entities) entityNames.Add(e.Name);

            var relsArr = raw["relationships"] as JArray ?? new JArray();
            foreach (var r in relsArr)
            {
                string rawSrc    = r["source"]?.Value<string>() ?? string.Empty;
                string rawTgt    = r["target"]?.Value<string>() ?? string.Empty;
                string rawType   = r["type"]?.Value<string>() ?? string.Empty;

                string src = nameMap.ContainsKey(rawSrc) ? nameMap[rawSrc] : TitleCase(rawSrc.Trim());
                string tgt = nameMap.ContainsKey(rawTgt) ? nameMap[rawTgt] : TitleCase(rawTgt.Trim());
                string typ = RelationshipTypes.Contains(rawType) ? rawType.ToLowerInvariant() : "relates_to";

                if (src == tgt || !entityNames.Contains(src) || !entityNames.Contains(tgt))
                    continue;

                result.Relationships.Add(new RelationshipInfo
                {
                    Source      = src,
                    Target      = tgt,
                    Type        = typ,
                    Description = r["description"]?.Value<string>() ?? string.Empty
                });
            }

            return result;
        }

        // ── LLM extraction ─────────────────────────────────────────────────

        internal static async Task<ExtractionResult> ExtractFromChunkAsync(
            string text, string source = null, string section = null)
        {
            var contextParts = new List<string>();
            if (!string.IsNullOrEmpty(source))  contextParts.Add("Source file: " + source);
            if (!string.IsNullOrEmpty(section)) contextParts.Add("Section: " + section);

            string prompt = contextParts.Count > 0
                ? string.Join("\n", contextParts) + "\n\n---\n\n" + text
                : text;

            try
            {
                string responseText = await ChatAsync(ExtractionModel, prompt);
                if (string.IsNullOrEmpty(responseText))
                    return new ExtractionResult();

                string cleaned = responseText
                    .TrimStart()
                    .TrimEnd();
                if (cleaned.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
                    cleaned = cleaned.Substring(7);
                else if (cleaned.StartsWith("```"))
                    cleaned = cleaned.Substring(3);
                if (cleaned.EndsWith("```"))
                    cleaned = cleaned.Substring(0, cleaned.Length - 3);
                cleaned = cleaned.Trim();

                var parsed = JObject.Parse(cleaned);
                return Normalize(parsed);
            }
            catch (Exception ex)
            {
                Logger.Warn("Extraction failed: " + ex.Message);
                return new ExtractionResult();
            }
        }

        // ── Global deduplication ───────────────────────────────────────────

        private static DedupResult DeduplicateGlobal(
            List<EntityInfo> allEntities,
            List<RelationshipInfo> allRelationships,
            Dictionary<int, List<string>> chunkEntities)
        {
            var canonMap = new Dictionary<string, EntityInfo>();

            foreach (var e in allEntities)
            {
                string key = DedupeKey(e.Name);
                if (!canonMap.ContainsKey(key))
                {
                    canonMap[key] = new EntityInfo
                    {
                        Name        = e.Name,
                        Type        = e.Type,
                        Description = e.Description
                    };
                }
                else
                {
                    var existing = canonMap[key];
                    if ((e.Description?.Length ?? 0) > (existing.Description?.Length ?? 0))
                        existing.Description = e.Description;
                    if (e.Name.Length > existing.Name.Length)
                        existing.Name = e.Name;
                }
            }

            // Build rename map
            var renameMap = new Dictionary<string, string>();
            foreach (var e in allEntities)
            {
                string key = DedupeKey(e.Name);
                if (canonMap.ContainsKey(key))
                    renameMap[e.Name] = canonMap[key].Name;
            }

            var entities     = new List<EntityInfo>(canonMap.Values);
            var entityNames  = new HashSet<string>();
            foreach (var e in entities) entityNames.Add(e.Name);

            // Remap relationships
            var seenRels     = new HashSet<string>();
            var relationships = new List<RelationshipInfo>();
            foreach (var r in allRelationships)
            {
                string src = renameMap.ContainsKey(r.Source) ? renameMap[r.Source] : r.Source;
                string tgt = renameMap.ContainsKey(r.Target) ? renameMap[r.Target] : r.Target;
                if (src == tgt) continue;
                if (!entityNames.Contains(src) || !entityNames.Contains(tgt)) continue;

                string edgeKey = src + "→" + r.Type + "→" + tgt;
                if (seenRels.Contains(edgeKey)) continue;
                seenRels.Add(edgeKey);

                relationships.Add(new RelationshipInfo
                {
                    Source      = src,
                    Target      = tgt,
                    Type        = r.Type,
                    Description = r.Description
                });
            }

            // Remap chunkEntities
            var remappedChunkEntities = new Dictionary<int, List<string>>();
            foreach (var kv in chunkEntities)
            {
                var remapped = new List<string>();
                var seen     = new HashSet<string>();
                foreach (string n in kv.Value)
                {
                    string canonical = renameMap.ContainsKey(n) ? renameMap[n] : n;
                    if (entityNames.Contains(canonical) && seen.Add(canonical))
                        remapped.Add(canonical);
                }
                remappedChunkEntities[kv.Key] = remapped;
            }

            Logger.Info(string.Format(
                "Extracted {0} raw → {1} unique entities, {2} raw → {3} unique relationships",
                allEntities.Count, entities.Count,
                allRelationships.Count, relationships.Count));

            return new DedupResult
            {
                Entities      = entities,
                Relationships = relationships,
                ChunkEntities = remappedChunkEntities
            };
        }

        /// <summary>
        /// Batch extraction for multiple chunks, then global deduplication.
        /// </summary>
        internal static async Task<DedupResult> ExtractFromChunksAsync(
            List<Chunking.ChunkResult> chunks)
        {
            var allEntities      = new List<EntityInfo>();
            var allRelationships = new List<RelationshipInfo>();
            var chunkEntities    = new Dictionary<int, List<string>>();

            for (int i = 0; i < chunks.Count; i++)
            {
                Console.Write(string.Format("  extracting: {0}/{1}\r", i + 1, chunks.Count));

                var chunk = chunks[i];
                var result = await ExtractFromChunkAsync(
                    chunk.Content, chunk.Source, chunk.Section);

                var names = new List<string>();
                foreach (var e in result.Entities) names.Add(e.Name);

                chunkEntities[i]  = names;
                allEntities.AddRange(result.Entities);
                allRelationships.AddRange(result.Relationships);
            }

            if (chunks.Count > 1) Console.WriteLine();

            return DeduplicateGlobal(allEntities, allRelationships, chunkEntities);
        }

        // ── HTTP helper for LLM calls ──────────────────────────────────────

        private static async Task<string> ChatAsync(string model, string userContent)
        {
            using (var http = new HttpClient())
            {
                http.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", AiConfig.ApiKey);

                if (AiConfig.Provider == "openrouter")
                {
                    if (!string.IsNullOrWhiteSpace(AiConfig.HttpReferer))
                        http.DefaultRequestHeaders.TryAddWithoutValidation(
                            "HTTP-Referer", AiConfig.HttpReferer);
                    if (!string.IsNullOrWhiteSpace(AiConfig.AppName))
                        http.DefaultRequestHeaders.TryAddWithoutValidation(
                            "X-Title", AiConfig.AppName);
                }

                var body = new JObject
                {
                    ["model"]             = AiConfig.ResolveModel(model),
                    ["instructions"]      = ExtractionInstructions,
                    ["max_output_tokens"] = 4096,
                    ["input"]             = new JArray
                    {
                        new JObject
                        {
                            ["type"]    = "message",
                            ["role"]    = "user",
                            ["content"] = userContent
                        }
                    }
                };

                string json = body.ToString(Formatting.None);

                using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
                using (var response = await http.PostAsync(AiConfig.ApiEndpoint, content))
                {
                    string responseBody = await response.Content.ReadAsStringAsync();
                    var parsed = JObject.Parse(responseBody);

                    if (parsed["error"] != null)
                        throw new InvalidOperationException(
                            parsed["error"]["message"]?.Value<string>() ?? responseBody);

                    Stats.RecordUsage(parsed["usage"]);

                    var output = parsed["output"] as JArray;
                    if (output == null) return null;

                    foreach (var item in output)
                    {
                        if (item["type"]?.Value<string>() == "message")
                        {
                            var contentArr = item["content"] as JArray;
                            if (contentArr != null)
                                foreach (var part in contentArr)
                                    if (part["type"]?.Value<string>() == "output_text")
                                        return part["text"]?.Value<string>();
                        }
                    }

                    return null;
                }
            }
        }
    }
}
