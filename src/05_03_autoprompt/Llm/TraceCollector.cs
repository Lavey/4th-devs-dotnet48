using System.Collections.Generic;
using FourthDevs.AutoPrompt.Models;

namespace FourthDevs.AutoPrompt.Llm
{
    public static class TraceCollector
    {
        private static readonly List<TraceEntry> _traces = new List<TraceEntry>();
        private static readonly object _lock = new object();

        public static void Record(TraceEntry entry)
        {
            lock (_lock)
            {
                _traces.Add(entry);
            }
        }

        public static List<TraceEntry> Collect()
        {
            lock (_lock)
            {
                var collected = new List<TraceEntry>(_traces);
                _traces.Clear();
                return collected;
            }
        }
    }
}
