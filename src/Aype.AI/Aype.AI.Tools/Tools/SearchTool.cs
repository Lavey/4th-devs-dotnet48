using Aype.AI.Common.Models;

namespace Aype.AI.Tools.Tools
{
    /// <summary>
    /// Definition for the hybrid search tool used by the AgentHybridRag.
    /// Combines full-text BM25 search with semantic vector similarity.
    /// </summary>
    public static class SearchTool
    {
        public static ToolDefinition Build()
        {
            return new ToolDefinition
            {
                Type        = "function",
                Name        = "search",
                Description =
                    "Search the indexed knowledge base using hybrid search " +
                    "(full-text BM25 + semantic vector similarity). " +
                    "Returns the most relevant document chunks with content, source file, " +
                    "and section heading. " +
                    "Provide BOTH a keyword query for full-text search AND a natural language " +
                    "query for semantic search.",
                Parameters  = new
                {
                    type       = "object",
                    properties = new
                    {
                        keywords = new
                        {
                            type        = "string",
                            description = "Keywords for full-text search (BM25) — " +
                                          "specific terms, names, and phrases"
                        },
                        semantic = new
                        {
                            type        = "string",
                            description = "Natural language query for semantic/vector search"
                        },
                        limit = new
                        {
                            type        = "number",
                            description = "Maximum number of results to return (default 5, max 20)"
                        }
                    },
                    required = new[] { "keywords", "semantic" }
                },
                Strict = false
            };
        }
    }
}
