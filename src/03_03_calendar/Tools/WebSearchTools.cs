using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FourthDevs.Calendar.Data;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Calendar.Tools
{
    public static class WebSearchTools
    {
        public static List<LocalToolDefinition> GetTools()
        {
            return new List<LocalToolDefinition>
            {
                new LocalToolDefinition
                {
                    Name = "web_search",
                    Description = "Run a fake web search over curated local results (places, reviews, profiles).",
                    Parameters = JObject.FromObject(new
                    {
                        type = "object",
                        properties = new
                        {
                            query = new { type = "string", description = "Search query" },
                            limit = new { type = "number", description = "Maximum number of results (default 5)" },
                        },
                        required = new[] { "query" },
                        additionalProperties = false,
                    }),
                    Handler = async (args) =>
                    {
                        string query = args["query"]?.Value<string>();
                        if (string.IsNullOrWhiteSpace(query))
                            return new { error = "query is required and must be a non-empty string" };

                        int limit = args["limit"] != null && args["limit"].Type == JTokenType.Integer
                            ? Math.Max(1, args["limit"].Value<int>()) : 5;

                        var results = WebSearchStore.Search(query);
                        if (results.Count > limit) results = results.GetRange(0, limit);

                        return new { total = results.Count, results = results };
                    },
                },
            };
        }
    }
}
