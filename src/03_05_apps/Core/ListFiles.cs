using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FourthDevs.Apps.Models;

namespace FourthDevs.Apps.Core
{
    internal static class ListFiles
    {
        public static void EnsureListFiles(string todoPath, string shoppingPath)
        {
            if (!File.Exists(todoPath))
                File.WriteAllText(todoPath, "# Todo\n\n", Encoding.UTF8);
            if (!File.Exists(shoppingPath))
                File.WriteAllText(shoppingPath, "# Shopping\n\n", Encoding.UTF8);
        }

        public static ListsState ReadListsState(string todoPath, string shoppingPath)
        {
            var state = new ListsState
            {
                Todo     = ParseMarkdownList(todoPath),
                Shopping = ParseMarkdownList(shoppingPath),
                UpdatedAt = DateTime.UtcNow.ToString("o")
            };
            return state;
        }

        public static void WriteListsState(string todoPath, string shoppingPath, ListsState state)
        {
            File.WriteAllText(todoPath,     SerializeList("Todo",     state.Todo),     Encoding.UTF8);
            File.WriteAllText(shoppingPath, SerializeList("Shopping", state.Shopping), Encoding.UTF8);
        }

        public static string SummarizeLists(ListsState state)
        {
            int todoPending     = CountPending(state.Todo);
            int shoppingPending = CountPending(state.Shopping);
            return string.Format(
                "Todo items: {0} total ({1} pending). Shopping items: {2} total ({3} pending).",
                state.Todo.Count, todoPending,
                state.Shopping.Count, shoppingPending);
        }

        // -----------------------------------------------------------------------

        private static List<ListItem> ParseMarkdownList(string path)
        {
            var items = new List<ListItem>();
            if (!File.Exists(path)) return items;

            string[] lines = File.ReadAllLines(path, Encoding.UTF8);
            int idCounter = 1;

            foreach (string raw in lines)
            {
                string line = raw.TrimEnd();
                if (!line.StartsWith("- ")) continue;

                string rest = line.Substring(2); // after "- "

                bool done = false;
                string text;

                if (rest.StartsWith("[x] ", StringComparison.OrdinalIgnoreCase))
                {
                    done = true;
                    text = rest.Substring(4);
                }
                else if (rest.StartsWith("[ ] ", StringComparison.OrdinalIgnoreCase))
                {
                    done = false;
                    text = rest.Substring(4);
                }
                else
                {
                    // Plain dash bullet with no checkbox
                    done = false;
                    text = rest;
                }

                if (string.IsNullOrWhiteSpace(text)) continue;

                items.Add(new ListItem
                {
                    Id   = (idCounter++).ToString(),
                    Text = text.Trim(),
                    Done = done
                });
            }

            return items;
        }

        private static string SerializeList(string heading, List<ListItem> items)
        {
            var sb = new StringBuilder();
            sb.Append("# ").AppendLine(heading);
            sb.AppendLine();

            if (items != null)
            {
                foreach (var item in items)
                {
                    string checkbox = item.Done ? "[x]" : "[ ]";
                    sb.AppendLine(string.Format("- {0} {1}", checkbox, item.Text));
                }
            }

            return sb.ToString();
        }

        private static int CountPending(List<ListItem> items)
        {
            int count = 0;
            if (items == null) return count;
            foreach (var item in items)
                if (!item.Done) count++;
            return count;
        }
    }
}
