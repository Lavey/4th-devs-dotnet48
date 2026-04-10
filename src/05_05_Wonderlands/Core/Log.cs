using System;
using System.Collections.Generic;

namespace FourthDevs.Wonderlands.Core
{
    public sealed class ScopedLog
    {
        private readonly string _actorName;
        private readonly string _tag;

        internal ScopedLog(string actorName)
        {
            _actorName = actorName;
            _tag = Log.ActorTag(actorName);
        }

        public void Llm(int step)
        {
            Console.WriteLine(Log.Pre() + " " + _tag + " " + Log.Dim + "LLM step " + step + Log.Reset);
        }

        public void Decision(string text)
        {
            Console.WriteLine(Log.Pre() + " " + _tag + " " + Log.Blue + "\u0001f4ad" + Log.Reset + " " + Log.Dim + Log.Truncate(text, 90) + Log.Reset);
        }

        public void Tool(string name, object args)
        {
            var argStr = args != null ? args.ToString() : "{}";
            Console.WriteLine(Log.Pre() + " " + _tag + " " + Log.Yellow + "\u26a1" + Log.Reset + " " + Log.Bold + name + Log.Reset + " " + Log.Dim + Log.Truncate(argStr, 70) + Log.Reset);
        }

        public void ToolResult(string name, bool ok, string output)
        {
            var icon = ok ? Log.Green + "\u2713" + Log.Reset : Log.Red + "\u2717" + Log.Reset;
            Console.WriteLine(Log.Pre() + " " + _tag + "   " + icon + " " + Log.Dim + Log.Truncate(output, 90) + Log.Reset);
        }

        public void Usage(int inputTokens, int outputTokens, int cachedTokens)
        {
            var cacheRate = inputTokens > 0 ? (int)Math.Round(100.0 * cachedTokens / inputTokens) : 0;
            var cacheInfo = cachedTokens > 0
                ? " " + Log.Green + "(" + cachedTokens + " cached, " + cacheRate + "% hit)" + Log.Reset
                : "";
            Console.WriteLine(Log.Pre() + " " + _tag + " " + Log.Dim + "tokens: " + inputTokens + " in / " + outputTokens + " out" + cacheInfo + Log.Reset);
        }
    }

    public static class Log
    {
        internal const string Reset = "\x1b[0m";
        internal const string Bold = "\x1b[1m";
        internal const string Dim = "\x1b[2m";
        internal const string Red = "\x1b[31m";
        internal const string Green = "\x1b[32m";
        internal const string Yellow = "\x1b[33m";
        internal const string Blue = "\x1b[34m";
        internal const string Magenta = "\x1b[35m";
        internal const string Cyan = "\x1b[36m";

        private static readonly Dictionary<string, string> ActorColors = new Dictionary<string, string>
        {
            ["orchestrator"] = Cyan,
            ["researcher"] = Magenta,
            ["writer"] = Green,
            ["reviewer"] = Yellow,
        };

        internal static string Pre() => Dim + "[" + DateTime.Now.ToString("HH:mm:ss") + "]" + Reset;
        internal static string Truncate(string s, int max = 120) =>
            s != null && s.Length > max ? s.Substring(0, max) + "\u2026" : s ?? "";

        internal static string ActorTag(string name)
        {
            string color;
            if (!ActorColors.TryGetValue(name, out color)) color = Blue;
            return color + "[" + name + "]" + Reset;
        }

        public static ScopedLog Scoped(string actorName) => new ScopedLog(actorName);

        public static void Header(string text)
        {
            int width = Math.Max(text.Length + 4, 50);
            Console.WriteLine("\n" + Cyan + new string('\u2500', width) + Reset);
            Console.WriteLine(Cyan + "\u2502" + Reset + " " + Bold + text.PadRight(width - 3) + Reset + Cyan + "\u2502" + Reset);
            Console.WriteLine(Cyan + new string('\u2500', width) + Reset);
        }

        public static void Info(string msg) => Console.WriteLine(Pre() + " " + msg);
        public static void Success(string msg) => Console.WriteLine(Pre() + " " + Green + "\u2713" + Reset + " " + msg);
        public static void Error(string msg) => Console.WriteLine(Pre() + " " + Red + "\u2717" + Reset + " " + msg);
        public static void Warn(string msg) => Console.WriteLine(Pre() + " " + Yellow + "\u26a0" + Reset + " " + msg);

        public static void Round(int n, int jobCount)
        {
            Console.WriteLine("\n" + Pre() + " \x1b[44m\x1b[37m\x1b[1m ROUND " + n + " " + Reset + " " + Dim + jobCount + " job(s) ready" + Reset);
        }

        public static void Actor(string name, string jobTitle)
        {
            Console.WriteLine(Pre() + " " + ActorTag(name) + " " + Dim + "working on" + Reset + " " + Truncate(jobTitle, 60));
        }

        public static void Delegate(string from, string to, string title)
        {
            Console.WriteLine(Pre() + " " + ActorTag(from) + " " + Magenta + "\u2192" + Reset + " " + ActorTag(to) + " " + Dim + Truncate(title, 50) + Reset);
        }

        public static void JobDone(string agentName, string summary)
        {
            Console.WriteLine(Pre() + " " + ActorTag(agentName) + " " + Green + "\u2713 completed" + Reset + " " + Dim + Truncate(summary, 70) + Reset);
        }

        public static void JobWaiting(string agentName, string reason)
        {
            Console.WriteLine(Pre() + " " + ActorTag(agentName) + " " + Blue + "\u23f8 waiting" + Reset + " " + Dim + Truncate(reason, 70) + Reset);
        }

        public static void JobBlocked(string agentName, string reason)
        {
            Console.WriteLine(Pre() + " " + ActorTag(agentName) + " " + Yellow + "\u25c6 blocked" + Reset + " " + Dim + Truncate(reason, 70) + Reset);
        }

        public static void JobError(string agentName, string error)
        {
            Console.WriteLine(Pre() + " " + ActorTag(agentName) + " " + Red + "\u2717 error" + Reset + " " + Dim + Truncate(error, 70) + Reset);
        }

        public static void MemoryStatus(int itemCount, int pendingTokens, int observationTokens, int generation)
        {
            Console.WriteLine(Pre() + " " + Magenta + "\U0001f9e0" + Reset + " " + Dim + "pending: " + pendingTokens + " tokens (" + itemCount + " items) | observations: " + observationTokens + " tokens (gen " + generation + ")" + Reset);
        }

        public static void MemoryObserved(int itemCount, int observationLines, int tokens, int sealedSeq)
        {
            Console.WriteLine(Pre() + " " + Magenta + "\U0001f9e0 observed" + Reset + " " + itemCount + " items \u2192 " + observationLines + " lines (" + tokens + " tokens, sealed through #" + sealedSeq + ")");
        }

        public static void MemoryReflected(int tokensBefore, int tokensAfter, int level, int generation)
        {
            Console.WriteLine(Pre() + " " + Magenta + "\U0001f9e0 reflected" + Reset + " " + tokensBefore + " \u2192 " + tokensAfter + " tokens (level " + level + ", gen " + generation + ")");
        }

        public static void MemorySkipped(int pendingTokens, int threshold)
        {
            Console.WriteLine(Pre() + " " + Magenta + "\U0001f9e0" + Reset + " " + Dim + "below threshold (" + pendingTokens + " < " + threshold + "), skipped" + Reset);
        }
    }
}
