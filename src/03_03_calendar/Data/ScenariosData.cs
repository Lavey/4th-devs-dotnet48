using System.Collections.Generic;
using FourthDevs.Calendar.Models;

namespace FourthDevs.Calendar.Data
{
    public static class ScenariosData
    {
        public static readonly List<AddScenarioStep> AddScenario = new List<AddScenarioStep>
        {
            new AddScenarioStep
            {
                Id = "add-001",
                At = "2026-02-25T09:00:00+01:00",
                LocationId = "p-home",
                Message = "Book a creative review with Marta and Luiza at that cafe on Planty tomorrow at 10:00. " +
                          "Please include both as guests and write a short agenda in the description.",
            },
            new AddScenarioStep
            {
                Id = "add-002",
                At = "2026-02-25T09:10:00+01:00",
                LocationId = "p-office",
                Message = "Schedule lunch with Kasia tomorrow around noon, somewhere near the office, maybe that Italian place. " +
                          "Please invite her and keep it at 60 minutes.",
            },
            new AddScenarioStep
            {
                Id = "add-003",
                At = "2026-02-25T09:20:00+01:00",
                LocationId = "p-office",
                Message = "Set up a meeting with Tomek Brandt on Thursday at 14:00 at the coworking space in Kazimierz. " +
                          "Keep a formal tone in the event description.",
            },
            new AddScenarioStep
            {
                Id = "add-004",
                At = "2026-02-25T09:30:00+01:00",
                LocationId = "p-office",
                Message = "I want to take Anna for dinner on Friday evening. She loves sushi, so find a good place and add her as a guest.",
            },
            new AddScenarioStep
            {
                Id = "add-005",
                At = "2026-02-25T09:40:00+01:00",
                LocationId = "p-office",
                Message = "Quick call with Piotr on Monday morning, 30 minutes. Make it a virtual event with a meeting link.",
            },
        };

        public static readonly List<NotificationWebhook> NotificationWebhooks = new List<NotificationWebhook>
        {
            new NotificationWebhook
            {
                Id = "wh-001",
                At = "2026-02-26T09:15:00+01:00",
                LocationId = "p-home",
                Payload = new NotificationWebhookPayload
                {
                    Type = "event.upcoming",
                    EventTitle = "Creative review with Marta and Luiza",
                    StartsAt = "2026-02-26T10:00:00+01:00",
                    MinutesUntilStart = 45,
                },
            },
            new NotificationWebhook
            {
                Id = "wh-002",
                At = "2026-02-26T11:15:00+01:00",
                LocationId = "p-office",
                Payload = new NotificationWebhookPayload
                {
                    Type = "event.upcoming",
                    EventTitle = "Lunch with Kasia Nowak",
                    StartsAt = "2026-02-26T12:00:00+01:00",
                    MinutesUntilStart = 45,
                },
            },
            new NotificationWebhook
            {
                Id = "wh-003",
                At = "2026-02-26T13:15:00+01:00",
                LocationId = "p-trattoria",
                Payload = new NotificationWebhookPayload
                {
                    Type = "event.upcoming",
                    EventTitle = "Meeting with Tomek Brandt",
                    StartsAt = "2026-02-26T14:00:00+01:00",
                    MinutesUntilStart = 45,
                },
            },
            new NotificationWebhook
            {
                Id = "wh-004",
                At = "2026-02-27T18:15:00+01:00",
                LocationId = "p-home",
                Payload = new NotificationWebhookPayload
                {
                    Type = "event.upcoming",
                    EventTitle = "Dinner with Anna Wisniewska",
                    StartsAt = "2026-02-27T19:00:00+01:00",
                    MinutesUntilStart = 45,
                },
            },
            new NotificationWebhook
            {
                Id = "wh-005",
                At = "2026-03-02T08:30:00+01:00",
                LocationId = "p-home",
                Payload = new NotificationWebhookPayload
                {
                    Type = "event.upcoming",
                    EventTitle = "Call with Piotr Zielinski",
                    StartsAt = "2026-03-02T09:00:00+01:00",
                    MinutesUntilStart = 30,
                },
            },
        };
    }
}
