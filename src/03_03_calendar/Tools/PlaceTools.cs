using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FourthDevs.Calendar.Data;
using FourthDevs.Calendar.Models;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Calendar.Tools
{
    public static class PlaceTools
    {
        private static int ScorePlace(Place place, string query)
        {
            string q = query.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(q)) return 0;

            var parts = new List<string> { place.Name, place.Address, place.Description, place.Type };
            if (place.Tags != null) parts.AddRange(place.Tags);

            string haystack = string.Join(" ", parts).ToLowerInvariant();

            if (haystack.Contains(q)) return 100;

            string[] tokens = q.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0) return 0;

            return tokens.Sum(token => haystack.Contains(token) ? 10 : 0);
        }

        public static List<LocalToolDefinition> GetTools()
        {
            return new List<LocalToolDefinition>
            {
                new LocalToolDefinition
                {
                    Name = "search_places",
                    Description = "Search places by name, tags, area, and description.",
                    Parameters = JObject.FromObject(new
                    {
                        type = "object",
                        properties = new
                        {
                            query = new { type = "string", description = "Free-text query, e.g. \"Italian near office\"" },
                            type = new
                            {
                                type = "string",
                                @enum = new[] { "office", "restaurant", "cafe", "coworking", "home", "mall" },
                                description = "Optional place type filter",
                            },
                            limit = new { type = "number", description = "Maximum number of places to return (default 5)" },
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
                        string typeFilter = args["type"]?.Value<string>();

                        var source = string.IsNullOrEmpty(typeFilter)
                            ? PlaceStore.Places
                            : PlaceStore.Places.Where(p => p.Type == typeFilter).ToList();

                        var ranked = source
                            .Select(p => new { Place = p, Score = ScorePlace(p, query) })
                            .Where(x => x.Score > 0)
                            .OrderByDescending(x => x.Score)
                            .Take(limit)
                            .Select(x => x.Place)
                            .ToList();

                        return new { total = ranked.Count, places = ranked };
                    },
                },

                new LocalToolDefinition
                {
                    Name = "get_place",
                    Description = "Get full place details by place ID.",
                    Parameters = JObject.FromObject(new
                    {
                        type = "object",
                        properties = new
                        {
                            place_id = new { type = "string", description = "Place ID, e.g. p-trattoria" },
                        },
                        required = new[] { "place_id" },
                        additionalProperties = false,
                    }),
                    Handler = async (args) =>
                    {
                        string placeId = args["place_id"]?.Value<string>();
                        if (string.IsNullOrEmpty(placeId))
                            return new { error = "place_id is required and must be a string" };

                        var place = PlaceStore.Places.FirstOrDefault(p => p.Id == placeId);
                        if (place == null) return new { error = "Place not found: " + placeId };
                        return place;
                    },
                },
            };
        }
    }
}
