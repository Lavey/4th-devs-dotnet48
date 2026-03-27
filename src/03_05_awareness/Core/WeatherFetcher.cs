using System;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FourthDevs.Awareness.Models;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Awareness.Core
{
    internal static class WeatherFetcher
    {
        public static string ExtractLocation(string identityMarkdown)
        {
            if (string.IsNullOrEmpty(identityMarkdown)) return null;
            var match = Regex.Match(identityMarkdown, @"^Location:\s*(.+)$", RegexOptions.Multiline);
            return match.Success ? match.Groups[1].Value.Trim() : null;
        }

        public static async Task<WeatherSnapshot> FetchWeatherAsync(string location)
        {
            if (string.IsNullOrWhiteSpace(location)) return null;
            try
            {
                using (var http = new HttpClient())
                {
                    string geoUrl = $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(location)}&count=1&language=en&format=json";
                    string geoJson = await http.GetStringAsync(geoUrl);
                    JObject geoObj = JObject.Parse(geoJson);
                    JArray results = geoObj["results"] as JArray;
                    if (results == null || results.Count == 0) return null;

                    double lat = results[0]["latitude"].Value<double>();
                    double lon = results[0]["longitude"].Value<double>();
                    string name = results[0]["name"]?.ToString() ?? location;

                    string forecastUrl = $"https://api.open-meteo.com/v1/forecast?latitude={lat}&longitude={lon}&current=temperature_2m,weather_code&timezone=auto";
                    string forecastJson = await http.GetStringAsync(forecastUrl);
                    JObject forecastObj = JObject.Parse(forecastJson);
                    JToken current = forecastObj["current"];

                    double? temp = current?["temperature_2m"]?.Value<double>();
                    int? weatherCode = current?["weather_code"]?.Value<int>();

                    return new WeatherSnapshot
                    {
                        Location = name,
                        Summary = WeatherCodeToSummary(weatherCode),
                        TemperatureC = temp,
                        ObservedAt = DateTime.UtcNow.ToString("o"),
                        Source = "open-meteo"
                    };
                }
            }
            catch
            {
                return null;
            }
        }

        private static string WeatherCodeToSummary(int? code)
        {
            if (code == null) return "unknown";
            int c = code.Value;
            if (c == 0) return "clear sky";
            if (c <= 2) return "partly cloudy";
            if (c == 3) return "overcast";
            if (c <= 49) return "foggy";
            if (c <= 57) return "drizzle";
            if (c <= 67) return "rain";
            if (c <= 77) return "snow";
            if (c <= 82) return "rain showers";
            if (c <= 86) return "snow showers";
            if (c <= 99) return "thunderstorm";
            return "unknown";
        }
    }
}
