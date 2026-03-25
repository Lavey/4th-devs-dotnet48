using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FourthDevs.Calendar.Data;
using FourthDevs.Calendar.Models;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Calendar.Tools
{
    public static class CalendarTools
    {
        private static DateTime? ParseIso(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null) return null;
            string s = token.Value<string>();
            if (string.IsNullOrEmpty(s)) return null;
            DateTimeOffset dt;
            if (!DateTimeOffset.TryParse(s, out dt)) return null;
            return dt.UtcDateTime;
        }

        private static string NormalizeText(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            string lower = value.ToLowerInvariant();
            var sb = new System.Text.StringBuilder();
            foreach (char c in lower.Normalize(System.Text.NormalizationForm.FormD))
            {
                var cat = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
                if (cat == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
                if (char.IsLetterOrDigit(c) || c == ' ')
                    sb.Append(c);
                else
                    sb.Append(' ');
            }
            return System.Text.RegularExpressions.Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
        }

        private static string BuildEventSearchText(CalendarEvent evt)
        {
            var parts = new List<string> { evt.Title };
            if (!string.IsNullOrEmpty(evt.LocationName)) parts.Add(evt.LocationName);
            if (!string.IsNullOrEmpty(evt.Description)) parts.Add(evt.Description);
            if (evt.Guests != null)
                foreach (var g in evt.Guests)
                    parts.Add(g.Name + " " + g.Email);
            return string.Join(" ", parts);
        }

        private static int ScoreTitleMatch(CalendarEvent evt, string normalizedQuery, string[] queryTokens)
        {
            string haystack = NormalizeText(BuildEventSearchText(evt));
            if (string.IsNullOrEmpty(haystack) || string.IsNullOrEmpty(normalizedQuery)) return 0;

            int score = 0;
            if (haystack.Contains(normalizedQuery)) score += 100;

            string normalizedTitle = NormalizeText(evt.Title);
            if (normalizedQuery.Contains(normalizedTitle) || normalizedTitle.Contains(normalizedQuery))
                score += 40;

            foreach (string token in queryTokens)
                if (haystack.Contains(token)) score += 10;

            return score;
        }

        private static int ScoreTimeProximity(CalendarEvent evt, DateTime? expected)
        {
            if (!expected.HasValue) return 0;
            DateTimeOffset evtStart;
            if (!DateTimeOffset.TryParse(evt.Start, out evtStart)) return 0;
            double diffMinutes = Math.Abs((evtStart.UtcDateTime - expected.Value).TotalMinutes);
            if (diffMinutes <= 15) return 40;
            if (diffMinutes <= 60) return 30;
            if (diffMinutes <= 180) return 20;
            if (diffMinutes <= 720) return 10;
            return 0;
        }

        private static List<CalendarGuest> ToGuestList(JToken token)
        {
            var result = new List<CalendarGuest>();
            if (token == null || token.Type != JTokenType.Array) return result;

            foreach (JToken item in token)
            {
                string email = item.Value<string>();
                if (string.IsNullOrEmpty(email)) continue;

                var matched = ContactStore.Contacts.FirstOrDefault(c =>
                    string.Equals(c.Email, email, StringComparison.OrdinalIgnoreCase));

                result.Add(new CalendarGuest
                {
                    Name = matched != null ? matched.Name : email.Split('@')[0],
                    Email = email,
                    Status = "pending",
                });
            }
            return result;
        }

        private static List<CalendarEvent> SortByStart(List<CalendarEvent> items)
        {
            return items.OrderBy(e =>
            {
                DateTimeOffset dt;
                return DateTimeOffset.TryParse(e.Start, out dt) ? dt.ToUnixTimeMilliseconds() : 0;
            }).ToList();
        }

        public static List<LocalToolDefinition> GetTools()
        {
            return new List<LocalToolDefinition>
            {
                new LocalToolDefinition
                {
                    Name = "create_event",
                    Description = "Create a calendar event with guests, location, and description.",
                    Parameters = JObject.FromObject(new
                    {
                        type = "object",
                        properties = new
                        {
                            title = new { type = "string", description = "Event title" },
                            start = new { type = "string", description = "Start datetime in ISO format with timezone" },
                            end = new { type = "string", description = "End datetime in ISO format with timezone" },
                            location_id = new { type = "string", description = "Optional place ID for in-person event" },
                            guest_emails = new { type = "array", items = new { type = "string" }, description = "Optional guest email list" },
                            description = new { type = "string", description = "Optional event description" },
                            is_virtual = new { type = "boolean", description = "Whether this is a virtual meeting (default false)" },
                            meeting_link = new { type = "string", description = "Optional meeting link for virtual events" },
                        },
                        required = new[] { "title", "start", "end" },
                        additionalProperties = false,
                    }),
                    Handler = async (args) =>
                    {
                        string title = args["title"]?.Value<string>();
                        if (string.IsNullOrWhiteSpace(title))
                            return new { error = "title is required and must be a non-empty string" };

                        DateTime? start = ParseIso(args["start"]);
                        DateTime? end = ParseIso(args["end"]);
                        if (!start.HasValue || !end.HasValue)
                            return new { error = "start and end must be valid ISO datetimes" };
                        if (start.Value >= end.Value)
                            return new { error = "end must be later than start" };

                        bool isVirtual = args["is_virtual"] != null && args["is_virtual"].Type == JTokenType.Boolean
                            ? args["is_virtual"].Value<bool>() : false;

                        string locationId = args["location_id"]?.Value<string>();
                        Place place = null;
                        if (!string.IsNullOrEmpty(locationId))
                        {
                            place = PlaceStore.Places.FirstOrDefault(p => p.Id == locationId);
                            if (!isVirtual && place == null)
                                return new { error = "Unknown location_id: " + locationId };
                        }

                        var startStr = args["start"]?.Value<string>() ?? start.Value.ToString("o");
                        var endStr = args["end"]?.Value<string>() ?? end.Value.ToString("o");

                        var evt = new CalendarEvent
                        {
                            Title = title.Trim(),
                            Start = startStr,
                            End = endStr,
                            Guests = ToGuestList(args["guest_emails"]),
                            LocationId = place?.Id,
                            LocationName = place?.Name,
                            Address = place?.Address,
                            Description = args["description"]?.Value<string>(),
                            IsVirtual = isVirtual,
                            MeetingLink = args["meeting_link"]?.Value<string>(),
                        };

                        var created = CalendarStore.AddEvent(evt);
                        return new { created = true, @event = created };
                    },
                },

                new LocalToolDefinition
                {
                    Name = "list_events",
                    Description = "List calendar events, optionally filtered by date range or text query.",
                    Parameters = JObject.FromObject(new
                    {
                        type = "object",
                        properties = new
                        {
                            from = new { type = "string", description = "Optional start datetime ISO filter (inclusive)" },
                            to = new { type = "string", description = "Optional end datetime ISO filter (inclusive)" },
                            query = new { type = "string", description = "Optional title query" },
                            limit = new { type = "number", description = "Optional max number of events (default 20)" },
                        },
                        required = new string[0],
                        additionalProperties = false,
                    }),
                    Handler = async (args) =>
                    {
                        DateTime? from = ParseIso(args["from"]);
                        DateTime? to = ParseIso(args["to"]);

                        List<CalendarEvent> items = (from.HasValue && to.HasValue)
                            ? CalendarStore.GetEventsInRange(from.Value.ToString("o"), to.Value.ToString("o"))
                            : CalendarStore.GetEvents();

                        string query = args["query"]?.Value<string>();
                        if (!string.IsNullOrWhiteSpace(query))
                        {
                            string q = query.ToLowerInvariant();
                            items = items.Where(e => e.Title.ToLowerInvariant().Contains(q)).ToList();
                        }

                        int limit = args["limit"] != null && args["limit"].Type == JTokenType.Integer
                            ? Math.Max(1, args["limit"].Value<int>()) : 20;

                        var sorted = SortByStart(items).Take(limit).ToList();
                        return new { total = sorted.Count, events = sorted };
                    },
                },

                new LocalToolDefinition
                {
                    Name = "get_event",
                    Description = "Get one event by exact event ID.",
                    Parameters = JObject.FromObject(new
                    {
                        type = "object",
                        properties = new
                        {
                            event_id = new { type = "string", description = "Event ID, e.g. evt-001" },
                        },
                        required = new[] { "event_id" },
                        additionalProperties = false,
                    }),
                    Handler = async (args) =>
                    {
                        string eventId = args["event_id"]?.Value<string>();
                        if (string.IsNullOrEmpty(eventId))
                            return new { error = "event_id is required and must be a string" };

                        var evt = CalendarStore.GetEventById(eventId);
                        if (evt == null) return new { error = "Event not found: " + eventId };
                        return evt;
                    },
                },

                new LocalToolDefinition
                {
                    Name = "find_event",
                    Description = "Find an event by fuzzy title match (title + guest names + description), " +
                                  "optionally weighted by expected start timestamp.",
                    Parameters = JObject.FromObject(new
                    {
                        type = "object",
                        properties = new
                        {
                            title = new { type = "string", description = "Event title or partial title" },
                            starts_at = new { type = "string", description = "Optional expected start datetime (ISO)" },
                        },
                        required = new[] { "title" },
                        additionalProperties = false,
                    }),
                    Handler = async (args) =>
                    {
                        string title = args["title"]?.Value<string>();
                        if (string.IsNullOrWhiteSpace(title))
                            return new { error = "title is required and must be a non-empty string" };

                        string normalizedTitle = NormalizeText(title);
                        string[] tokens = normalizedTitle.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                            .Where(t => t.Length >= 2).ToArray();
                        DateTime? startsAt = ParseIso(args["starts_at"]);

                        var ranked = CalendarStore.GetEvents()
                            .Select(evt => new
                            {
                                Event = evt,
                                TitleScore = ScoreTitleMatch(evt, normalizedTitle, tokens),
                                TimeScore = ScoreTimeProximity(evt, startsAt),
                            })
                            .Select(x => new { x.Event, x.TitleScore, x.TimeScore, Total = x.TitleScore + x.TimeScore })
                            .Where(x => x.TitleScore > 0 || x.TimeScore >= 30)
                            .OrderByDescending(x => x.Total)
                            .ToList();

                        if (ranked.Count == 0)
                            return new { error = "No event found for title query: " + title };

                        return new
                        {
                            @event = ranked[0].Event,
                            candidates = ranked.Count,
                            confidence = ranked[0].Total,
                        };
                    },
                },
            };
        }
    }
}
