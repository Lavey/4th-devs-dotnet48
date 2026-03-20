using System;
using System.Collections.Generic;

namespace FourthDevs.Lesson05_Agent.Events
{
    /// <summary>
    /// Simple event emitter — thin wrapper that supports typed + wildcard handlers.
    /// Mirrors 01_05_agent/src/events/emitter.ts (i-am-alice/4th-devs).
    /// </summary>
    internal class AgentEventEmitter
    {
        private readonly Dictionary<string, List<Action<AgentEvent>>> _handlers =
            new Dictionary<string, List<Action<AgentEvent>>>(StringComparer.OrdinalIgnoreCase);

        private readonly List<Action<AgentEvent>> _wildcardHandlers = new List<Action<AgentEvent>>();

        private readonly object _lock = new object();

        /// <summary>
        /// Emit an event to all matching type-specific handlers and all wildcard handlers.
        /// </summary>
        internal void Emit(AgentEvent evt)
        {
            List<Action<AgentEvent>> typed;
            List<Action<AgentEvent>> wildcard;

            lock (_lock)
            {
                _handlers.TryGetValue(evt.Type, out typed);
                wildcard = new List<Action<AgentEvent>>(_wildcardHandlers);
            }

            if (typed != null)
            {
                foreach (var handler in typed)
                    SafeCall(handler, evt);
            }

            foreach (var handler in wildcard)
                SafeCall(handler, evt);
        }

        /// <summary>
        /// Subscribe to events of a specific type.
        /// Returns an unsubscribe action.
        /// </summary>
        internal Action On(string type, Action<AgentEvent> handler)
        {
            lock (_lock)
            {
                List<Action<AgentEvent>> list;
                if (!_handlers.TryGetValue(type, out list))
                {
                    list = new List<Action<AgentEvent>>();
                    _handlers[type] = list;
                }
                list.Add(handler);
            }

            return () =>
            {
                lock (_lock)
                {
                    List<Action<AgentEvent>> list;
                    if (_handlers.TryGetValue(type, out list))
                        list.Remove(handler);
                }
            };
        }

        /// <summary>
        /// Subscribe to all events (wildcard).
        /// Returns an unsubscribe action.
        /// </summary>
        internal Action OnAny(Action<AgentEvent> handler)
        {
            lock (_lock) _wildcardHandlers.Add(handler);
            return () => { lock (_lock) _wildcardHandlers.Remove(handler); };
        }

        private static void SafeCall(Action<AgentEvent> handler, AgentEvent evt)
        {
            try
            {
                handler(evt);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[events] handler error on " + evt.Type + ": " + ex.Message);
            }
        }
    }
}
