using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace FourthDevs.Evals.Core
{
    /// <summary>
    /// Structured JSON logger that writes one JSON line per log entry to stdout.
    /// </summary>
    internal sealed class Logger
    {
        private readonly Dictionary<string, object> _bindings;

        public Logger(Dictionary<string, object> bindings = null)
        {
            _bindings = bindings ?? new Dictionary<string, object>();
        }

        public void Debug(string message, Dictionary<string, object> data = null)
        {
            Write("debug", message, data);
        }

        public void Info(string message, Dictionary<string, object> data = null)
        {
            Write("info", message, data);
        }

        public void Warn(string message, Dictionary<string, object> data = null)
        {
            Write("warn", message, data);
        }

        public void Error(string message, Dictionary<string, object> data = null)
        {
            Write("error", message, data);
        }

        /// <summary>
        /// Creates a child logger that inherits current bindings and adds extra ones.
        /// </summary>
        public Logger Child(Dictionary<string, object> extra)
        {
            var merged = new Dictionary<string, object>(_bindings);
            if (extra != null)
            {
                foreach (var kv in extra)
                {
                    merged[kv.Key] = kv.Value;
                }
            }
            return new Logger(merged);
        }

        private void Write(string level, string message, Dictionary<string, object> data)
        {
            var payload = new Dictionary<string, object>
            {
                { "level", level },
                { "time", DateTime.UtcNow.ToString("o") },
                { "message", message }
            };

            foreach (var kv in _bindings)
            {
                payload[kv.Key] = kv.Value;
            }

            if (data != null)
            {
                foreach (var kv in data)
                {
                    payload[kv.Key] = kv.Value;
                }
            }

            Console.WriteLine(JsonConvert.SerializeObject(payload));
        }
    }
}
