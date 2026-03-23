using System;
using System.Collections.Generic;
using System.Linq;
using FourthDevs.Observability.Models;

namespace FourthDevs.Observability
{
    /// <summary>
    /// Thread-safe in-memory session storage.
    /// </summary>
    internal static class SessionStore
    {
        private static readonly Dictionary<string, Session> _sessions =
            new Dictionary<string, Session>(StringComparer.OrdinalIgnoreCase);

        private static readonly object _lock = new object();

        /// <summary>
        /// Returns the session for the given id, creating a new one if it does not exist.
        /// </summary>
        public static Session GetSession(string sessionId)
        {
            lock (_lock)
            {
                Session session;
                if (!_sessions.TryGetValue(sessionId, out session))
                {
                    session = new Session
                    {
                        Id = sessionId,
                        Messages = new List<ChatMessage>()
                    };
                    _sessions[sessionId] = session;
                }
                return session;
            }
        }

        /// <summary>
        /// Returns a lightweight summary of all active sessions.
        /// </summary>
        public static List<object> ListSessions()
        {
            lock (_lock)
            {
                return _sessions.Values
                    .Select(s => (object)new
                    {
                        id = s.Id,
                        messageCount = s.Messages.Count
                    })
                    .ToList();
            }
        }
    }
}
