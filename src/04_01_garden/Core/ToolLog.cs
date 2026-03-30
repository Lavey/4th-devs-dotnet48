using System;

namespace FourthDevs.Garden.Core
{
    /// <summary>
    /// Coloured console logging for tool calls and agent turns.
    /// Port of 04_01_garden/src/agent/log.ts.
    /// </summary>
    internal static class ToolLog
    {
        public static void LogToolCall(string name, string argsPreview)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("-> " + name);
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine(" " + Truncate(argsPreview, 200));
            Console.ResetColor();
        }

        public static void LogToolResult(string name, string output, bool ok)
        {
            Console.ForegroundColor = ok ? ConsoleColor.Green : ConsoleColor.Red;
            string icon = ok ? "ok" : "ERR";
            Console.Write("  " + icon + " " + name);
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine(" " + Truncate(output, 200));
            Console.ResetColor();
        }

        public static void LogTurn(int turn)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine();
            Console.WriteLine("-- turn " + turn + " --");
            Console.ResetColor();
        }

        public static void LogBuiltinTool(string type, string detail)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("-> " + type);
            if (!string.IsNullOrEmpty(detail))
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write(" " + detail);
            }
            Console.WriteLine();
            Console.ResetColor();
        }

        private static string Truncate(string text, int max)
        {
            if (text == null) return string.Empty;
            if (text.Length <= max) return text;
            return text.Substring(0, max) + "... (" + text.Length + " chars)";
        }
    }
}
