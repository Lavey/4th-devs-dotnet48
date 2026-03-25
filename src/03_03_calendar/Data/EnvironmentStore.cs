using System;
using System.Text;
using FourthDevs.Calendar.Models;

namespace FourthDevs.Calendar.Data
{
    public static class EnvironmentStore
    {
        private static readonly EnvironmentState State = new EnvironmentState
        {
            CurrentTime = "2026-02-25T09:00:00+01:00",
            UserLocation = new UserLocation
            {
                PlaceId = "p-home",
                Name = "Adam's Apartment",
                Coordinates = new Coordinates { Lat = 50.0637, Lng = 19.9390 },
            },
        };

        public static EnvironmentState GetEnvironment()
        {
            return new EnvironmentState
            {
                CurrentTime = State.CurrentTime,
                UserLocation = new UserLocation
                {
                    PlaceId = State.UserLocation.PlaceId,
                    Name = State.UserLocation.Name,
                    Coordinates = new Coordinates
                    {
                        Lat = State.UserLocation.Coordinates.Lat,
                        Lng = State.UserLocation.Coordinates.Lng,
                    },
                },
            };
        }

        public static void SetTime(string iso)
        {
            State.CurrentTime = iso;
        }

        public static void SetUserLocation(string placeId)
        {
            Place place = PlaceStore.Places.Find(p => p.Id == placeId);
            if (place == null) throw new ArgumentException("Unknown place: " + placeId);

            State.UserLocation = new UserLocation
            {
                PlaceId = place.Id,
                Name = place.Name,
                Coordinates = new Coordinates { Lat = place.Coordinates.Lat, Lng = place.Coordinates.Lng },
            };
        }

        public static string BuildMetadata()
        {
            DateTimeOffset dt = DateTimeOffset.Parse(State.CurrentTime);
            string date = State.CurrentTime.Substring(0, 10);
            int hour = dt.Hour;

            WeatherSlot weather = WeatherStore.GetWeatherAt(date, hour);

            string[] dayNames = { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };
            string dayName = dayNames[(int)dt.DayOfWeek];

            string timeStr = dt.ToString("HH:mm");

            var sb = new StringBuilder();
            sb.AppendLine("<metadata>");
            sb.AppendLine(string.Format("Current time: {0}, {1} {2} CET", dayName, date, timeStr));
            sb.AppendLine(string.Format("Location ID: {0}", State.UserLocation.PlaceId));

            Place currentPlace = PlaceStore.Places.Find(p => p.Id == State.UserLocation.PlaceId);
            string address = currentPlace != null ? currentPlace.Address : "";
            sb.AppendLine(string.Format("Location: {0} ({1})", State.UserLocation.Name, address));

            if (weather != null)
            {
                string precipPart = weather.PrecipMm > 0
                    ? string.Format(", precipitation {0} mm", weather.PrecipMm)
                    : "";
                sb.AppendLine(string.Format("Weather: {0} — {1}°C, wind {2} km/h{3}",
                    weather.Description, weather.TempC, weather.WindKmh, precipPart));
            }

            sb.Append("</metadata>");
            return sb.ToString();
        }
    }
}
