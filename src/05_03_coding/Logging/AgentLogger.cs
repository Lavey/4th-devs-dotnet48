using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.CodingAgent.Logging
{
    /// <summary>
    /// Console and JSONL file logger for the coding agent.
    /// Mirrors logger.ts from the TypeScript original.
    /// </summary>
    internal sealed class AgentLogger
    {
        private readonly string _sessionId;
        private readonly string _logPath;
        private readonly object _lock = new object();

        public AgentLogger(string sessionId)
        {
            _sessionId = sessionId;

            string logDir = Config.AgentConfig.GetLogDir();
            Directory.CreateDirectory(logDir);
            _logPath = Path.Combine(logDir, sessionId + ".jsonl");
        }

        public void Info(string scope, string message)
        {
            // DIM [scope] RESET message
            Console.WriteLine("  \x1b[2m[{0}]\x1b[0m {1}", scope, message);
        }

        public void Error(string scope, string message)
        {
            Console.WriteLine("  \x1b[31m[{0}] {1}\x1b[0m", scope, message);
        }

        public void Error(string scope, Exception ex, string context = "Unexpected error")
        {
            Error(scope, string.Format("{0}: {1}", context, ex.Message));
        }

        public void Event(string type, JObject data = null)
        {
            try
            {
                var entry = data != null ? new JObject(data) : new JObject();
                entry["at"] = DateTime.UtcNow.ToString("o");
                entry["type"] = type;
                entry["sessionId"] = _sessionId;

                string line = entry.ToString(Formatting.None) + "\n";

                lock (_lock)
                {
                    File.AppendAllText(_logPath, line);
                }
            }
            catch
            {
                // Logging should never break the agent.
            }
        }

        public string LogFilePath { get { return _logPath; } }
    }
}
