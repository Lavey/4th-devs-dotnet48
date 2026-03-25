using System.Collections.Generic;
using System.Linq;
using FourthDevs.Calendar.Models;

namespace FourthDevs.Calendar.Data
{
    public static class WeatherStore
    {
        public static readonly List<WeatherSlot> Forecast = new List<WeatherSlot>
        {
            // Wednesday Feb 25 — sunny, cool
            new WeatherSlot { Date = "2026-02-25", Hour = 6,  TempC = 2,  Condition = "clear",         WindKmh = 5,  PrecipMm = 0,   Description = "Clear and cold" },
            new WeatherSlot { Date = "2026-02-25", Hour = 9,  TempC = 5,  Condition = "sunny",         WindKmh = 8,  PrecipMm = 0,   Description = "Sunny, light breeze" },
            new WeatherSlot { Date = "2026-02-25", Hour = 12, TempC = 8,  Condition = "sunny",         WindKmh = 10, PrecipMm = 0,   Description = "Sunny and pleasant" },
            new WeatherSlot { Date = "2026-02-25", Hour = 15, TempC = 7,  Condition = "sunny",         WindKmh = 8,  PrecipMm = 0,   Description = "Sunny, cooling down" },
            new WeatherSlot { Date = "2026-02-25", Hour = 18, TempC = 4,  Condition = "clear",         WindKmh = 5,  PrecipMm = 0,   Description = "Clear evening" },
            new WeatherSlot { Date = "2026-02-25", Hour = 21, TempC = 2,  Condition = "clear",         WindKmh = 3,  PrecipMm = 0,   Description = "Cold, clear night" },

            // Thursday Feb 26 — rain from late morning
            new WeatherSlot { Date = "2026-02-26", Hour = 6,  TempC = 1,  Condition = "cloudy",        WindKmh = 12, PrecipMm = 0,   Description = "Overcast and cold" },
            new WeatherSlot { Date = "2026-02-26", Hour = 9,  TempC = 3,  Condition = "cloudy",        WindKmh = 15, PrecipMm = 0,   Description = "Grey skies, windy" },
            new WeatherSlot { Date = "2026-02-26", Hour = 12, TempC = 4,  Condition = "rainy",         WindKmh = 20, PrecipMm = 2.5, Description = "Steady rain, gusty wind" },
            new WeatherSlot { Date = "2026-02-26", Hour = 15, TempC = 5,  Condition = "rainy",         WindKmh = 18, PrecipMm = 1.8, Description = "Light rain continues" },
            new WeatherSlot { Date = "2026-02-26", Hour = 18, TempC = 3,  Condition = "rainy",         WindKmh = 15, PrecipMm = 1.2, Description = "Rain tapering off, cold" },
            new WeatherSlot { Date = "2026-02-26", Hour = 21, TempC = 2,  Condition = "cloudy",        WindKmh = 10, PrecipMm = 0,   Description = "Dry but overcast" },

            // Friday Feb 27 — clear, warmer
            new WeatherSlot { Date = "2026-02-27", Hour = 6,  TempC = 5,  Condition = "clear",         WindKmh = 5,  PrecipMm = 0,   Description = "Fresh and clear" },
            new WeatherSlot { Date = "2026-02-27", Hour = 9,  TempC = 8,  Condition = "sunny",         WindKmh = 8,  PrecipMm = 0,   Description = "Bright morning sun" },
            new WeatherSlot { Date = "2026-02-27", Hour = 12, TempC = 11, Condition = "sunny",         WindKmh = 10, PrecipMm = 0,   Description = "Warm for February" },
            new WeatherSlot { Date = "2026-02-27", Hour = 15, TempC = 12, Condition = "sunny",         WindKmh = 8,  PrecipMm = 0,   Description = "Pleasant afternoon" },
            new WeatherSlot { Date = "2026-02-27", Hour = 18, TempC = 10, Condition = "clear",         WindKmh = 5,  PrecipMm = 0,   Description = "Clear, mild evening" },
            new WeatherSlot { Date = "2026-02-27", Hour = 21, TempC = 7,  Condition = "clear",         WindKmh = 3,  PrecipMm = 0,   Description = "Cool, starry night" },

            // Saturday Feb 28 — cloudy
            new WeatherSlot { Date = "2026-02-28", Hour = 9,  TempC = 4,  Condition = "cloudy",        WindKmh = 10, PrecipMm = 0,   Description = "Overcast morning" },
            new WeatherSlot { Date = "2026-02-28", Hour = 12, TempC = 6,  Condition = "cloudy",        WindKmh = 12, PrecipMm = 0,   Description = "Grey but dry" },
            new WeatherSlot { Date = "2026-02-28", Hour = 18, TempC = 5,  Condition = "partly_cloudy", WindKmh = 8,  PrecipMm = 0,   Description = "Some breaks in clouds" },

            // Sunday Mar 1 — partly cloudy
            new WeatherSlot { Date = "2026-03-01", Hour = 9,  TempC = 5,  Condition = "partly_cloudy", WindKmh = 8,  PrecipMm = 0,   Description = "Mixed skies" },
            new WeatherSlot { Date = "2026-03-01", Hour = 12, TempC = 8,  Condition = "partly_cloudy", WindKmh = 10, PrecipMm = 0,   Description = "Sun and clouds" },
            new WeatherSlot { Date = "2026-03-01", Hour = 18, TempC = 6,  Condition = "cloudy",        WindKmh = 8,  PrecipMm = 0,   Description = "Clouding over" },

            // Monday Mar 2 — cloudy, then clearing
            new WeatherSlot { Date = "2026-03-02", Hour = 6,  TempC = 4,  Condition = "cloudy",        WindKmh = 12, PrecipMm = 0,   Description = "Grey start" },
            new WeatherSlot { Date = "2026-03-02", Hour = 9,  TempC = 6,  Condition = "cloudy",        WindKmh = 15, PrecipMm = 0,   Description = "Overcast and breezy" },
            new WeatherSlot { Date = "2026-03-02", Hour = 12, TempC = 8,  Condition = "partly_cloudy", WindKmh = 12, PrecipMm = 0,   Description = "Clouds breaking up" },
            new WeatherSlot { Date = "2026-03-02", Hour = 15, TempC = 9,  Condition = "sunny",         WindKmh = 10, PrecipMm = 0,   Description = "Afternoon sun" },
            new WeatherSlot { Date = "2026-03-02", Hour = 18, TempC = 6,  Condition = "clear",         WindKmh = 8,  PrecipMm = 0,   Description = "Clear evening" },
        };

        public static WeatherSlot GetWeatherAt(string date, int hour)
        {
            var exact = Forecast.FirstOrDefault(s => s.Date == date && s.Hour == hour);
            if (exact != null) return exact;

            var sameDay = Forecast.Where(s => s.Date == date).ToList();
            if (sameDay.Count == 0) return null;

            WeatherSlot closest = sameDay[0];
            foreach (var slot in sameDay)
            {
                if (System.Math.Abs(slot.Hour - hour) < System.Math.Abs(closest.Hour - hour))
                    closest = slot;
            }
            return closest;
        }
    }
}
