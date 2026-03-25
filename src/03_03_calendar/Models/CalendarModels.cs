using System.Collections.Generic;

namespace FourthDevs.Calendar.Models
{
    public class Contact
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Company { get; set; }
        public string Role { get; set; }
        public string Relationship { get; set; }
        public List<string> Preferences { get; set; }
        public string Notes { get; set; }
    }

    public class Coordinates
    {
        public double Lat { get; set; }
        public double Lng { get; set; }
    }

    public class Place
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public string Address { get; set; }
        public Coordinates Coordinates { get; set; }
        public Dictionary<string, string> OpeningHours { get; set; }
        public List<string> Tags { get; set; }
        public string Phone { get; set; }
        public string Website { get; set; }
        public string Description { get; set; }
    }

    public class TravelOption
    {
        public int DurationMin { get; set; }
        public double DistanceKm { get; set; }
        public string Description { get; set; }
    }

    public class TransitOption : TravelOption
    {
        public string Line { get; set; }
        public int Stops { get; set; }
    }

    public class Route
    {
        public string From { get; set; }
        public string To { get; set; }
        public TravelOption Walking { get; set; }
        public TravelOption Driving { get; set; }
        public TransitOption Transit { get; set; }
    }

    public class CalendarGuest
    {
        public string Name { get; set; }
        public string Email { get; set; }
        public string Status { get; set; }
    }

    public class CalendarEvent
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Start { get; set; }
        public string End { get; set; }
        public string LocationId { get; set; }
        public string LocationName { get; set; }
        public string Address { get; set; }
        public List<CalendarGuest> Guests { get; set; }
        public string Description { get; set; }
        public bool IsVirtual { get; set; }
        public string MeetingLink { get; set; }
    }

    public class NotificationRecord
    {
        public string Id { get; set; }
        public string CreatedAt { get; set; }
        public string Channel { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public string EventId { get; set; }
    }

    public class WeatherSlot
    {
        public string Date { get; set; }
        public int Hour { get; set; }
        public double TempC { get; set; }
        public string Condition { get; set; }
        public double WindKmh { get; set; }
        public double PrecipMm { get; set; }
        public string Description { get; set; }
    }

    public class WebSearchResult
    {
        public string Title { get; set; }
        public string Url { get; set; }
        public string Snippet { get; set; }
    }

    public class WebSearchEntry
    {
        public List<string> Keywords { get; set; }
        public List<WebSearchResult> Results { get; set; }
    }

    public class AddScenarioStep
    {
        public string Id { get; set; }
        public string At { get; set; }
        public string LocationId { get; set; }
        public string Message { get; set; }
    }

    public class NotificationWebhook
    {
        public string Id { get; set; }
        public string At { get; set; }
        public string LocationId { get; set; }
        public NotificationWebhookPayload Payload { get; set; }
    }

    public class NotificationWebhookPayload
    {
        public string Type { get; set; }
        public string EventTitle { get; set; }
        public string StartsAt { get; set; }
        public int MinutesUntilStart { get; set; }
    }

    public class EnvironmentState
    {
        public string CurrentTime { get; set; }
        public UserLocation UserLocation { get; set; }
    }

    public class UserLocation
    {
        public string PlaceId { get; set; }
        public string Name { get; set; }
        public Coordinates Coordinates { get; set; }
    }
}
