using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace FourthDevs.AgentGraph.Events
{
    public sealed class AgentEvent
    {
        public int Seq { get; set; }
        public string Type { get; set; }
        public string Time { get; set; }
        public JObject Data { get; set; }
    }

    public static class EventBus
    {
        private static int _seq;
        private static readonly List<AgentEvent> Buffer = new List<AgentEvent>();
        private static readonly List<Action<AgentEvent>> Listeners = new List<Action<AgentEvent>>();
        private const int MaxBuffer = 500;

        public static void Emit(string type, JObject data = null)
        {
            var evt = new AgentEvent
            {
                Seq = ++_seq,
                Type = type,
                Time = DateTime.UtcNow.ToString("o"),
                Data = data ?? new JObject()
            };
            Buffer.Add(evt);
            if (Buffer.Count > MaxBuffer) Buffer.RemoveAt(0);

            foreach (var listener in Listeners.ToArray())
            {
                try { listener(evt); }
                catch (Exception ex) { Console.Error.WriteLine("[events] listener error: " + ex.Message); }
            }
        }

        public static Action Subscribe(Action<AgentEvent> listener)
        {
            Listeners.Add(listener);
            return () => Listeners.Remove(listener);
        }

        public static IReadOnlyList<AgentEvent> Replay() => Buffer.AsReadOnly();
    }
}
