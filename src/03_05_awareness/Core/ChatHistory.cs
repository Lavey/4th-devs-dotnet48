using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FourthDevs.Awareness.Models;
using Newtonsoft.Json;

namespace FourthDevs.Awareness.Core
{
    internal static class ChatHistory
    {
        private static readonly string HistoryPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "workspace", "system", "chat", "history.jsonl");

        public static async Task<List<ChatLogEntry>> LoadRecentHistoryAsync(int limit)
        {
            if (!File.Exists(HistoryPath))
                return new List<ChatLogEntry>();

            var lines = new List<string>();
            using (var reader = new StreamReader(HistoryPath))
            {
                string line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    line = line.Trim();
                    if (!string.IsNullOrEmpty(line))
                        lines.Add(line);
                }
            }

            int start = Math.Max(0, lines.Count - limit);
            var entries = new List<ChatLogEntry>();
            for (int i = start; i < lines.Count; i++)
            {
                try
                {
                    var entry = JsonConvert.DeserializeObject<ChatLogEntry>(lines[i]);
                    if (entry != null) entries.Add(entry);
                }
                catch { /* skip malformed lines */ }
            }
            return entries;
        }

        public static List<Message> HistoryToMessages(List<ChatLogEntry> entries)
        {
            var messages = new List<Message>();
            foreach (var entry in entries)
                messages.Add(new Message { Role = entry.Role, Content = entry.Content });
            return messages;
        }

        public static async Task AppendConversationLogsAsync(string sessionId, string userMsg, string assistantMsg)
        {
            string dir = Path.GetDirectoryName(HistoryPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            using (var writer = new StreamWriter(HistoryPath, append: true))
            {
                var userEntry = new ChatLogEntry
                {
                    At = DateTime.UtcNow.ToString("o"),
                    SessionId = sessionId,
                    Role = "user",
                    Content = userMsg
                };
                await writer.WriteLineAsync(JsonConvert.SerializeObject(userEntry, Formatting.None));

                var assistantEntry = new ChatLogEntry
                {
                    At = DateTime.UtcNow.ToString("o"),
                    SessionId = sessionId,
                    Role = "assistant",
                    Content = assistantMsg
                };
                await writer.WriteLineAsync(JsonConvert.SerializeObject(assistantEntry, Formatting.None));
            }
        }
    }
}
