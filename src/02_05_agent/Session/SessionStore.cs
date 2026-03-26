using System.Collections.Generic;
using System.Linq;
using FourthDevs.ContextAgent.Memory;
using MemSession = FourthDevs.ContextAgent.Memory.Session;

namespace FourthDevs.ContextAgent.Session
{
    internal static class SessionStore
    {
        private static readonly Dictionary<string, MemSession> _sessions =
            new Dictionary<string, MemSession>(System.StringComparer.OrdinalIgnoreCase);

        private static readonly object _lock = new object();

        public static MemSession GetOrCreate(string sessionId)
        {
            lock (_lock)
            {
                MemSession session;
                if (!_sessions.TryGetValue(sessionId, out session))
                {
                    session = new MemSession { Id = sessionId };
                    _sessions[sessionId] = session;
                }
                return session;
            }
        }

        public static MemSession Get(string sessionId)
        {
            lock (_lock)
            {
                MemSession session;
                _sessions.TryGetValue(sessionId, out session);
                return session;
            }
        }

        public static List<object> List()
        {
            lock (_lock)
            {
                return _sessions.Values.Select(s => (object)new
                {
                    id = s.Id,
                    messageCount = s.Messages.Count,
                    observationTokens = s.Memory.ObservationTokenCount,
                    generation = s.Memory.GenerationCount
                }).ToList();
            }
        }
    }
}
