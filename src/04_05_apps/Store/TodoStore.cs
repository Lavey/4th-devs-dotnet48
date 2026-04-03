using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using FourthDevs.McpApps.Models;

namespace FourthDevs.McpApps.Store
{
    internal static class TodoStore
    {
        private static readonly Regex LinePattern = new Regex(@"^- \[( |x)\] ([^|]+)\| (.+)$");

        private static string FilePath
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "workspace", "todos.md"); }
        }

        public static void EnsureWorkspace()
        {
            string dir = Path.GetDirectoryName(FilePath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            if (!File.Exists(FilePath))
            {
                var defaults = new List<TodoItem>
                {
                    new TodoItem { Id = "t1", Text = "Review the MCP Apps phase-one scaffold", Done = false },
                    new TodoItem { Id = "t2", Text = "Draft the welcome sequence outline", Done = false },
                    new TodoItem { Id = "t3", Text = "Archive old checkout experiments", Done = true },
                };
                WriteItems(defaults);
            }
        }

        public static TodosState ReadState()
        {
            EnsureWorkspace();
            var items = ParseFile();
            return new TodosState
            {
                Items = items,
                UpdatedAt = File.GetLastWriteTimeUtc(FilePath).ToString("o")
            };
        }

        public static string Summarize(TodosState state)
        {
            int pending = state.Items.Count(i => !i.Done);
            int done = state.Items.Count - pending;
            return string.Format("{0} total, {1} open, {2} done", state.Items.Count, pending, done);
        }

        public static TodoItem AddTodo(string text)
        {
            var state = ReadState();
            string id = GetNextId(state.Items);
            var item = new TodoItem { Id = id, Text = text.Trim(), Done = false };
            state.Items.Add(item);
            WriteItems(state.Items);
            return item;
        }

        public static TodoItem CompleteTodo(string target)
        {
            return SetDone(target, true);
        }

        public static TodoItem ReopenTodo(string target)
        {
            return SetDone(target, false);
        }

        public static TodoItem RemoveTodo(string target)
        {
            var state = ReadState();
            var match = Resolve(state.Items, target);
            state.Items.RemoveAll(i => i.Id == match.Id);
            WriteItems(state.Items);
            return match;
        }

        // ────────────────────────────────────────────

        private static TodoItem SetDone(string target, bool done)
        {
            var state = ReadState();
            var match = Resolve(state.Items, target);
            match.Done = done;
            WriteItems(state.Items);
            return match;
        }

        private static TodoItem Resolve(List<TodoItem> items, string target)
        {
            string needle = target.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(needle)) throw new Exception("Todo target must be a non-empty string.");

            var exact = items.FirstOrDefault(i => i.Id.ToLowerInvariant() == needle);
            if (exact != null) return exact;

            var textMatch = items.FirstOrDefault(i => i.Text.ToLowerInvariant() == needle);
            if (textMatch != null) return textMatch;

            var partial = items.Where(i => i.Text.ToLowerInvariant().Contains(needle)).ToList();
            if (partial.Count == 1) return partial[0];
            if (partial.Count > 1) throw new Exception("Todo target is ambiguous: \"" + target + "\".");

            throw new Exception("Todo not found: \"" + target + "\".");
        }

        private static string GetNextId(List<TodoItem> items)
        {
            int max = 0;
            foreach (var item in items)
            {
                var m = Regex.Match(item.Id, @"^t(\d+)$");
                if (m.Success)
                {
                    int n = int.Parse(m.Groups[1].Value);
                    if (n > max) max = n;
                }
            }
            return "t" + (max + 1);
        }

        private static List<TodoItem> ParseFile()
        {
            var items = new List<TodoItem>();
            if (!File.Exists(FilePath)) return items;
            foreach (string line in File.ReadAllLines(FilePath, Encoding.UTF8))
            {
                var m = LinePattern.Match(line);
                if (!m.Success) continue;
                items.Add(new TodoItem
                {
                    Id = m.Groups[2].Value.Trim(),
                    Text = m.Groups[3].Value.Trim(),
                    Done = m.Groups[1].Value == "x"
                });
            }
            return items;
        }

        private static void WriteItems(List<TodoItem> items)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Todos");
            sb.AppendLine();
            foreach (var item in items)
                sb.AppendLine(string.Format("- [{0}] {1} | {2}", item.Done ? "x" : " ", item.Id, item.Text));
            File.WriteAllText(FilePath, sb.ToString(), Encoding.UTF8);
        }
    }
}
