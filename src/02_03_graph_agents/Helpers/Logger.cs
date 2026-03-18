using System;
using System.Collections.Generic;
using System.Text;

namespace FourthDevs.Lesson08_GraphAgents.Helpers
{
    /// <summary>
    /// Colored console logger.
    /// Mirrors 02_03_graph_agents/src/helpers/logger.js (i-am-alice/4th-devs)
    /// </summary>
    internal static class Logger
    {
        private static string Timestamp() =>
            DateTime.Now.ToString("HH:mm:ss");

        private static void WriteDim(string prefix, string msg, ConsoleColor prefixColor)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("[" + Timestamp() + "] ");
            Console.ForegroundColor = prefixColor;
            Console.Write(prefix + " ");
            Console.ResetColor();
            Console.WriteLine(msg);
        }

        internal static void Info(string msg)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("[" + Timestamp() + "] ");
            Console.ResetColor();
            Console.WriteLine(msg);
        }

        internal static void Success(string msg) =>
            WriteDim("✓", msg, ConsoleColor.Green);

        internal static void Error(string title, string msg = null)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("[" + Timestamp() + "] ");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write("✗ " + title + " ");
            Console.ResetColor();
            Console.WriteLine(msg ?? string.Empty);
        }

        internal static void Warn(string msg) =>
            WriteDim("⚠", msg, ConsoleColor.Yellow);

        internal static void Start(string msg) =>
            WriteDim("→", msg, ConsoleColor.Cyan);

        internal static void Box(string text)
        {
            string[] lines = text.Split(new[] { "\n" }, StringSplitOptions.None);
            int width = 0;
            foreach (string l in lines)
                if (l.Length + 4 > width) width = l.Length + 4;

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(new string('─', width));
            foreach (string line in lines)
            {
                Console.Write("│ ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write(line.PadRight(width - 3));
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("│");
            }
            Console.WriteLine(new string('─', width));
            Console.ResetColor();
            Console.WriteLine();
        }

        internal static void Query(string q)
        {
            Console.WriteLine();
            Console.BackgroundColor = ConsoleColor.DarkBlue;
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write(" QUERY ");
            Console.ResetColor();
            Console.WriteLine(" " + q);
            Console.WriteLine();
        }

        internal static void Response(string r)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("Response: ");
            Console.ResetColor();
            string preview = r.Length > 500 ? r.Substring(0, 500) + "..." : r;
            Console.WriteLine(preview);
            Console.WriteLine();
        }

        internal static void ApiStep(int step, int msgCount)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("[" + Timestamp() + "] ");
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.Write("◆ ");
            Console.ResetColor();
            Console.WriteLine(string.Format("Step {0} ({1} messages)", step, msgCount));
        }

        internal static void ApiDone(Newtonsoft.Json.Linq.JToken usage)
        {
            if (usage == null) return;
            int input     = usage["input_tokens"]?.ToObject<int>() ?? 0;
            int output    = usage["output_tokens"]?.ToObject<int>() ?? 0;
            int reasoning = usage["output_tokens_details"]?["reasoning_tokens"]?.ToObject<int>() ?? 0;
            int cached    = usage["input_tokens_details"]?["cached_tokens"]?.ToObject<int>() ?? 0;

            var parts = new List<string> { input + " in" };
            if (cached > 0) parts.Add(cached + " cached");
            parts.Add(output + " out");
            if (reasoning > 0)
                parts.Add(reasoning + " reasoning + " + (output - reasoning) + " visible");

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("         tokens: " + string.Join(" / ", parts));
            Console.ResetColor();
        }

        internal static void Tool(string name, string argsJson)
        {
            string truncated = argsJson.Length > 300
                ? argsJson.Substring(0, 300) + "..." : argsJson;
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("[" + Timestamp() + "] ");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("⚡ " + name + " ");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine(truncated);
            Console.ResetColor();
        }

        internal static void ToolResult(string name, bool success, string output)
        {
            string icon = success ? "✓" : "✗";
            ConsoleColor color = success ? ConsoleColor.Green : ConsoleColor.Red;
            string truncated = output.Length > 500
                ? output.Substring(0, 500) + "..." : output;

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("         ");
            Console.ForegroundColor = color;
            Console.Write(icon + " ");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine(truncated);
            Console.ResetColor();
        }

        // ── Search-specific logs ───────────────────────────────────────────

        internal static void SearchHeader(string keywords, string semantic)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("         ");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("fts:      ");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("\"" + keywords + "\"");

            Console.Write("         ");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("semantic: ");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("\"" + semantic + "\"");
            Console.ResetColor();
        }

        internal static void SearchFts<T>(List<T> results)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("         ");
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.Write("FTS ");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine(results.Count == 0 ? "(no matches)" : results.Count + " hit(s)");
            Console.ResetColor();
        }

        internal static void SearchVec<T>(List<T> results)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("         ");
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.Write("VEC ");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine(results.Count == 0 ? "(no matches)" : results.Count + " hit(s)");
            Console.ResetColor();
        }

        internal static void SearchRrf(List<Graph.Search.ChunkResult> results)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("         ");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("RRF ");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine(results.Count + " merged result(s)");
            foreach (var r in results)
            {
                string section = !string.IsNullOrEmpty(r.Section) ? " → " + r.Section : string.Empty;
                string fts     = r.FtsRank.HasValue ? "fts:#" + r.FtsRank.Value : "—";
                string vec     = r.VecRank.HasValue ? "vec:#" + r.VecRank.Value : "—";
                Console.WriteLine(string.Format("           {0}{1} [{2} {3}] rrf={4:F4}",
                    r.Source, section, fts, vec, r.Rrf));
            }
            Console.ResetColor();
        }
    }
}
