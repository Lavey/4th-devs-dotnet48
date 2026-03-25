using System;
using System.Collections.Generic;
using System.Linq;
using FourthDevs.Calendar.Models;

namespace FourthDevs.Calendar.Data
{
    public static class CalendarStore
    {
        private static readonly List<CalendarEvent> Events = new List<CalendarEvent>
        {
            new CalendarEvent
            {
                Id = "evt-seed-001",
                Title = "Sprint 14 Planning",
                Start = "2026-02-25T14:00:00+01:00",
                End = "2026-02-25T15:00:00+01:00",
                LocationId = "p-office",
                LocationName = "TechVolt Office",
                Address = "ul. Przemysłowa 12, 30-701 Kraków",
                Guests = new List<CalendarGuest>
                {
                    new CalendarGuest { Name = "Kasia Nowak", Email = "kasia.nowak@techvolt.io", Status = "accepted" },
                },
                Description = "Sprint 14 planning session. Agenda: retro, API v3 migration, Nexon capacity.",
                IsVirtual = false,
            },
            new CalendarEvent
            {
                Id = "evt-seed-002",
                Title = "Dentist",
                Start = "2026-02-27T10:00:00+01:00",
                End = "2026-02-27T11:00:00+01:00",
                Guests = new List<CalendarGuest>(),
                Description = "Regular checkup. Dr. Mazur, ul. Karmelicka 30.",
                IsVirtual = false,
            },
        };

        private static int _nextId = 1;

        public static CalendarEvent AddEvent(CalendarEvent evt)
        {
            evt.Id = string.Format("evt-{0}", _nextId.ToString().PadLeft(3, '0'));
            _nextId++;
            Events.Add(evt);
            return evt;
        }

        public static List<CalendarEvent> GetEvents()
        {
            return new List<CalendarEvent>(Events);
        }

        public static CalendarEvent GetEventById(string id)
        {
            return Events.FirstOrDefault(e => e.Id == id);
        }

        public static List<CalendarEvent> GetEventsInRange(string start, string end)
        {
            long from = DateTimeOffset.Parse(start).ToUnixTimeMilliseconds();
            long to = DateTimeOffset.Parse(end).ToUnixTimeMilliseconds();

            return Events.Where(e =>
            {
                long eStart = DateTimeOffset.Parse(e.Start).ToUnixTimeMilliseconds();
                return eStart >= from && eStart <= to;
            }).ToList();
        }

        public static void ResetNextId()
        {
            _nextId = 1;
        }
    }
}
