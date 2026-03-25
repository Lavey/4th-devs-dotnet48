using System.Collections.Generic;
using System.Threading.Tasks;
using FourthDevs.Calendar.Data;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Calendar.Tools
{
    public static class NotificationTools
    {
        public static List<LocalToolDefinition> GetTools()
        {
            return new List<LocalToolDefinition>
            {
                new LocalToolDefinition
                {
                    Name = "send_notification",
                    Description = "Send a user notification. Used by the webhook phase.",
                    Parameters = JObject.FromObject(new
                    {
                        type = "object",
                        properties = new
                        {
                            title = new { type = "string", description = "Notification title" },
                            message = new { type = "string", description = "Notification body text" },
                            channel = new
                            {
                                type = "string",
                                @enum = new[] { "push", "sms", "email" },
                                description = "Delivery channel (default push)",
                            },
                            event_id = new { type = "string", description = "Optional related calendar event ID" },
                        },
                        required = new[] { "title", "message" },
                        additionalProperties = false,
                    }),
                    Handler = async (args) =>
                    {
                        string title = args["title"]?.Value<string>();
                        if (string.IsNullOrWhiteSpace(title))
                            return new { error = "title is required and must be a non-empty string" };

                        string message = args["message"]?.Value<string>();
                        if (string.IsNullOrWhiteSpace(message))
                            return new { error = "message is required and must be a non-empty string" };

                        string channelRaw = args["channel"]?.Value<string>();
                        string channel = (channelRaw == "sms" || channelRaw == "email" || channelRaw == "push")
                            ? channelRaw : "push";

                        string eventId = args["event_id"]?.Value<string>();

                        var created = NotificationStore.PushNotification(channel, title, message, eventId);
                        return new { sent = true, notification = created };
                    },
                },

                new LocalToolDefinition
                {
                    Name = "list_notifications",
                    Description = "List all notifications sent so far.",
                    Parameters = JObject.FromObject(new
                    {
                        type = "object",
                        properties = new { },
                        required = new string[0],
                        additionalProperties = false,
                    }),
                    Handler = async (args) =>
                    {
                        var notifications = NotificationStore.ListNotifications();
                        return new { total = notifications.Count, notifications = notifications };
                    },
                },
            };
        }
    }
}
