using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Lesson03_McpNative
{
    /// <summary>
    /// "MCP" tools — in production these would be dispatched to a real MCP server.
    /// Here they run in-process to keep the demo self-contained.
    /// Mirrors the tools defined in 01_03_mcp_native/src/mcp/server.js.
    /// </summary>
    internal static class McpTools
    {
        public static readonly string[] Names = { "get_weather", "get_time" };

        public static bool Handles(string toolName)
        {
            return Array.IndexOf(Names, toolName) >= 0;
        }

        public static object Execute(string name, JObject args)
        {
            switch (name)
            {
                case "get_weather": return GetWeather(args);
                case "get_time":    return GetTime(args);
                default:
                    throw new InvalidOperationException(
                        string.Format("Unknown MCP tool: {0}", name));
            }
        }

        static object GetWeather(JObject args)
        {
            string city = args["city"]?.ToString() ?? string.Empty;
            var data = new Dictionary<string, object>
            {
                { "Tokyo",  new { temp = 22, conditions = "sunny",  humidity = 60 } },
                { "London", new { temp =  8, conditions = "cloudy", humidity = 80 } },
                { "Paris",  new { temp = 14, conditions = "partly cloudy", humidity = 70 } },
                { "Kraków", new { temp = -2, conditions = "snow",   humidity = 90 } },
                { "Warsaw", new { temp =  0, conditions = "overcast", humidity = 85 } }
            };

            object weather;
            return data.TryGetValue(city, out weather)
                ? (object)new { city, weather }
                : new { city, weather = new { temp = (int?)null, conditions = "unknown" } };
        }

        static object GetTime(JObject args)
        {
            string timezone = args["timezone"]?.ToString() ?? "UTC";
            try
            {
                // Map IANA → Windows timezone ID for .NET 4.8
                string windowsId = IanaToWindows(timezone);
                var tz           = TimeZoneInfo.FindSystemTimeZoneById(windowsId);
                var now          = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
                return new { timezone, time = now.ToString("HH:mm:ss"), date = now.ToString("yyyy-MM-dd") };
            }
            catch
            {
                return new { timezone, time = DateTime.UtcNow.ToString("HH:mm:ss") + " UTC", note = "timezone not found" };
            }
        }

        static string IanaToWindows(string iana)
        {
            // Minimal IANA → Windows mapping for common timezones
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Europe/London",    "GMT Standard Time" },
                { "Europe/Paris",     "Romance Standard Time" },
                { "Europe/Warsaw",    "Central European Standard Time" },
                { "Europe/Krakow",    "Central European Standard Time" },
                { "America/New_York", "Eastern Standard Time" },
                { "America/Chicago",  "Central Standard Time" },
                { "America/Denver",   "Mountain Standard Time" },
                { "America/Los_Angeles", "Pacific Standard Time" },
                { "Asia/Tokyo",       "Tokyo Standard Time" },
                { "Asia/Shanghai",    "China Standard Time" },
                { "UTC",              "UTC" }
            };

            string win;
            return map.TryGetValue(iana, out win) ? win : iana;
        }
    }
}
