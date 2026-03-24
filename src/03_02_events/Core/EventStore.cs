using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using FourthDevs.Events.Models;
using FourthDevs.Events.Config;

namespace FourthDevs.Events.Core
{
    /// <summary>
    /// Stores heartbeat events to memory and disk (events.jsonl + round files).
    /// </summary>
    internal class EventStore
    {
        private readonly string _eventsDir;
        private readonly List<HeartbeatEvent> _roundEvents = new List<HeartbeatEvent>();
        private readonly object _lock = new object();

        public EventStore(string eventsDir)
        {
            _eventsDir = eventsDir;
            Directory.CreateDirectory(_eventsDir);
        }

        public Task EmitAsync(HeartbeatEvent evt)
        {
            if (string.IsNullOrEmpty(evt.At))
                evt.At = DateTime.UtcNow.ToString("o");

            lock (_lock)
            {
                _roundEvents.Add(evt);
            }

            Logger.Event(evt);

            string jsonLine = JsonConvert.SerializeObject(evt, Formatting.None,
                new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            string jsonlPath = Path.Combine(_eventsDir, "events.jsonl");
            File.AppendAllText(jsonlPath, jsonLine + Environment.NewLine, Encoding.UTF8);

            return Task.FromResult(0);
        }

        public void FlushRound(int round)
        {
            List<HeartbeatEvent> snapshot;
            lock (_lock)
            {
                snapshot = new List<HeartbeatEvent>(_roundEvents);
                _roundEvents.Clear();
            }

            if (snapshot.Count == 0) return;

            var sb = new StringBuilder();
            sb.AppendLine("---");
            sb.AppendLine("round: " + round);
            sb.AppendLine("events: " + snapshot.Count);
            sb.AppendLine("flushed_at: " + DateTime.UtcNow.ToString("o"));
            sb.AppendLine("---");
            sb.AppendLine();

            foreach (var evt in snapshot)
            {
                sb.AppendLine("## " + evt.Type);
                sb.AppendLine();
                sb.AppendLine("- **at**: " + evt.At);
                sb.AppendLine("- **message**: " + evt.Message);
                if (!string.IsNullOrEmpty(evt.Agent))
                    sb.AppendLine("- **agent**: " + evt.Agent);
                if (!string.IsNullOrEmpty(evt.TaskId))
                    sb.AppendLine("- **taskId**: " + evt.TaskId);
                sb.AppendLine();
            }

            string fileName = string.Format("round-{0:D3}.md", round);
            File.WriteAllText(Path.Combine(_eventsDir, fileName), sb.ToString(), Encoding.UTF8);
        }
    }
}
