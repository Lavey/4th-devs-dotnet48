using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FourthDevs.Calendar.Data;
using FourthDevs.Calendar.Models;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Calendar.Tools
{
    public static class MapTools
    {
        public static List<LocalToolDefinition> GetTools()
        {
            return new List<LocalToolDefinition>
            {
                new LocalToolDefinition
                {
                    Name = "get_route",
                    Description = "Get travel options between two place IDs (walking, driving, and optional transit).",
                    Parameters = JObject.FromObject(new
                    {
                        type = "object",
                        properties = new
                        {
                            from_place_id = new { type = "string", description = "Route origin place ID" },
                            to_place_id = new { type = "string", description = "Route destination place ID" },
                        },
                        required = new[] { "from_place_id", "to_place_id" },
                        additionalProperties = false,
                    }),
                    Handler = async (args) =>
                    {
                        string fromId = args["from_place_id"]?.Value<string>();
                        string toId = args["to_place_id"]?.Value<string>();

                        if (string.IsNullOrEmpty(fromId) || string.IsNullOrEmpty(toId))
                            return new { error = "from_place_id and to_place_id must be strings" };

                        Place fromPlace = PlaceStore.Places.FirstOrDefault(p => p.Id == fromId);
                        Place toPlace = PlaceStore.Places.FirstOrDefault(p => p.Id == toId);

                        if (fromPlace == null) return new { error = "Unknown from_place_id: " + fromId };
                        if (toPlace == null) return new { error = "Unknown to_place_id: " + toId };

                        Route route = RouteStore.FindRoute(fromId, toId);
                        if (route == null)
                            return new { error = string.Format("Route not found between {0} and {1}", fromId, toId) };

                        // Find fastest mode
                        var travel = new List<(string Mode, int Duration)>
                        {
                            ("walking", route.Walking.DurationMin),
                            ("driving", route.Driving.DurationMin),
                        };
                        if (route.Transit != null)
                            travel.Add(("transit", route.Transit.DurationMin));

                        var fastest = travel.OrderBy(t => t.Duration).First();

                        return new
                        {
                            from = new { id = fromPlace.Id, name = fromPlace.Name },
                            to = new { id = toPlace.Id, name = toPlace.Name },
                            options = new
                            {
                                walking = route.Walking,
                                driving = route.Driving,
                                transit = route.Transit,
                            },
                            fastest_mode = fastest.Mode,
                            fastest_duration_min = fastest.Duration,
                        };
                    },
                },
            };
        }
    }
}
