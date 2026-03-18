using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Lesson08_GraphAgents.Agent
{
    /// <summary>
    /// Tool JSON schema definitions sent to the LLM.
    /// Mirrors the TOOLS array in 02_03_graph_agents/src/agent/tools.js (i-am-alice/4th-devs)
    /// </summary>
    internal static class ToolDefinitions
    {
        internal static readonly List<JObject> All = new List<JObject>
        {
            // ── Retrieval ─────────────────────────────────────────────────

            new JObject
            {
                ["type"]        = "function",
                ["name"]        = "search",
                ["description"] =
                    "Search the knowledge base using hybrid retrieval (full-text BM25 + semantic vector). " +
                    "Returns matching document chunks AND the graph entities mentioned in those chunks. " +
                    "Use this as your first tool for any question.",
                ["parameters"]  = new JObject
                {
                    ["type"]       = "object",
                    ["properties"] = new JObject
                    {
                        ["keywords"] = new JObject
                        {
                            ["type"]        = "string",
                            ["description"] = "Keywords for full-text matching — names, terms, and phrases."
                        },
                        ["semantic"] = new JObject
                        {
                            ["type"]        = "string",
                            ["description"] = "Natural language query for semantic matching."
                        },
                        ["limit"] = new JObject
                        {
                            ["type"]        = "number",
                            ["description"] = "Maximum chunks to return (default: 5, max: 20)"
                        }
                    },
                    ["required"] = new JArray { "keywords", "semantic" }
                },
                ["strict"]      = false
            },

            new JObject
            {
                ["type"]        = "function",
                ["name"]        = "explore",
                ["description"] =
                    "Explore the knowledge graph around a specific entity. Returns the entity's metadata " +
                    "and all directly connected entities with relationship types and descriptions. " +
                    "Use AFTER search to follow connections.",
                ["parameters"]  = new JObject
                {
                    ["type"]       = "object",
                    ["properties"] = new JObject
                    {
                        ["entity"] = new JObject
                        {
                            ["type"]        = "string",
                            ["description"] = "Entity name to explore. Example: 'Prompt Engineering'"
                        },
                        ["limit"] = new JObject
                        {
                            ["type"]        = "number",
                            ["description"] = "Maximum neighbors to return (default: 20, max: 50)"
                        }
                    },
                    ["required"] = new JArray { "entity" }
                },
                ["strict"]      = false
            },

            new JObject
            {
                ["type"]        = "function",
                ["name"]        = "connect",
                ["description"] =
                    "Find how two entities are connected through the knowledge graph. Returns the shortest " +
                    "path(s): the chain of entities and relationships linking them. " +
                    "Use when the user asks how two concepts relate.",
                ["parameters"]  = new JObject
                {
                    ["type"]       = "object",
                    ["properties"] = new JObject
                    {
                        ["from"] = new JObject
                        {
                            ["type"]        = "string",
                            ["description"] = "Starting entity name."
                        },
                        ["to"] = new JObject
                        {
                            ["type"]        = "string",
                            ["description"] = "Target entity name."
                        },
                        ["maxDepth"] = new JObject
                        {
                            ["type"]        = "number",
                            ["description"] = "Maximum relationship hops (default: 4, max: 6)."
                        }
                    },
                    ["required"] = new JArray { "from", "to" }
                },
                ["strict"]      = false
            },

            new JObject
            {
                ["type"]        = "function",
                ["name"]        = "cypher",
                ["description"] =
                    "Execute a read-only Cypher query against the knowledge graph. " +
                    "Schema: (:Document {source, hash})-[:HAS_CHUNK]->(:Chunk {content, section, source, chunkIndex})" +
                    "-[:MENTIONS]->(:Entity {name, type, description}), " +
                    "(:Entity)-[:RELATED_TO {type, description, evidenceSource}]->(:Entity). " +
                    "ONLY read queries — no CREATE, MERGE, DELETE, SET, DROP.",
                ["parameters"]  = new JObject
                {
                    ["type"]       = "object",
                    ["properties"] = new JObject
                    {
                        ["query"] = new JObject
                        {
                            ["type"]        = "string",
                            ["description"] = "Cypher query string."
                        },
                        ["params"] = new JObject
                        {
                            ["type"]        = "object",
                            ["description"] = "Parameters to substitute into the query."
                        }
                    },
                    ["required"] = new JArray { "query" }
                },
                ["strict"]      = false
            },

            // ── Curation ──────────────────────────────────────────────────

            new JObject
            {
                ["type"]        = "function",
                ["name"]        = "learn",
                ["description"] =
                    "Index content into the knowledge graph. Two modes: pass 'filename' to index a file " +
                    "from workspace/, or pass 'text' + 'source' to index raw text.",
                ["parameters"]  = new JObject
                {
                    ["type"]       = "object",
                    ["properties"] = new JObject
                    {
                        ["filename"] = new JObject
                        {
                            ["type"]        = "string",
                            ["description"] = "Filename inside workspace/ directory."
                        },
                        ["text"] = new JObject
                        {
                            ["type"]        = "string",
                            ["description"] = "Raw text content to index directly."
                        },
                        ["source"] = new JObject
                        {
                            ["type"]        = "string",
                            ["description"] = "Label for raw text content (required when using 'text')."
                        }
                    }
                },
                ["strict"]      = false
            },

            new JObject
            {
                ["type"]        = "function",
                ["name"]        = "forget",
                ["description"] =
                    "Remove content and all its chunks, entity mentions, and orphaned entities from the graph.",
                ["parameters"]  = new JObject
                {
                    ["type"]       = "object",
                    ["properties"] = new JObject
                    {
                        ["source"] = new JObject
                        {
                            ["type"]        = "string",
                            ["description"] = "Source identifier to remove — a filename or source label."
                        }
                    },
                    ["required"] = new JArray { "source" }
                },
                ["strict"]      = false
            },

            new JObject
            {
                ["type"]        = "function",
                ["name"]        = "merge_entities",
                ["description"] =
                    "Merge a duplicate entity into a canonical one. Moves all relationships and chunk mentions " +
                    "from source entity to target entity, then deletes the source.",
                ["parameters"]  = new JObject
                {
                    ["type"]       = "object",
                    ["properties"] = new JObject
                    {
                        ["source"] = new JObject
                        {
                            ["type"]        = "string",
                            ["description"] = "Entity name to merge away (will be deleted)."
                        },
                        ["target"] = new JObject
                        {
                            ["type"]        = "string",
                            ["description"] = "Canonical entity name to keep."
                        }
                    },
                    ["required"] = new JArray { "source", "target" }
                },
                ["strict"]      = false
            },

            new JObject
            {
                ["type"]        = "function",
                ["name"]        = "audit",
                ["description"] =
                    "Diagnose knowledge graph quality. Returns node counts, orphan entities, " +
                    "potential duplicate entities, relationship type distribution, and entity type distribution.",
                ["parameters"]  = new JObject
                {
                    ["type"]       = "object",
                    ["properties"] = new JObject()
                },
                ["strict"]      = false
            }
        };
    }
}
