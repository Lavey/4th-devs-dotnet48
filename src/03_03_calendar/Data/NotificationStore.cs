using System.Collections.Generic;
using FourthDevs.Calendar.Models;

namespace FourthDevs.Calendar.Data
{
    public static class NotificationStore
    {
        private static readonly List<NotificationRecord> Records = new List<NotificationRecord>();
        private static int _nextId = 1;

        public static NotificationRecord PushNotification(string channel, string title, string message, string eventId = null)
        {
            var record = new NotificationRecord
            {
                Id = string.Format("notif-{0}", _nextId.ToString().PadLeft(3, '0')),
                CreatedAt = EnvironmentStore.GetEnvironment().CurrentTime,
                Channel = channel,
                Title = title,
                Message = message,
                EventId = eventId,
            };
            _nextId++;
            Records.Add(record);
            return record;
        }

        public static List<NotificationRecord> ListNotifications()
        {
            return new List<NotificationRecord>(Records);
        }
    }
}
