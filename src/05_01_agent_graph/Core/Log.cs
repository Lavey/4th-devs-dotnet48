using System;
using System.Collections.Generic;
using FourthDevs.AgentGraph.Events;
using Newtonsoft.Json.Linq;

namespace FourthDevs.AgentGraph.Core
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
            Console.WriteLine($"{Log.Pre()} {_tag} {Log.Dim}LLM step {step}{Log.Reset}");
            EventBus.Emit("llm", new JObject { ["step"] = step, ["actorName"] = _actorName });
        }

        public void Decision(string text)
        {
            Console.WriteLine($"{Log.Pre()} {_tag} {Log.Blue}💭{Log.Reset} {Log.Dim}{Log.Truncate(text, 90)}{Log.Reset}");
            EventBus.Emit("decision", new JObject { ["text"] = text, ["actorName"] = _actorName });
        }

        public void Tool(string name, JObject args)
        {
            var argStr = args?.ToString(Newtonsoft.Json.Formatting.None) ?? "{}";
            Console.WriteLine($"{Log.Pre()} {_tag} {Log.Yellow}⚡{Log.Reset} {Log.Bold}{name}{Log.Reset} {Log.Dim}{Log.Truncate(argStr, 70)}{Log.Reset}");
            EventBus.Emit("tool", new JObject { ["name"] = name, ["args"] = args, ["actorName"] = _actorName });
        }

        public void ToolResult(string name, bool ok, string output)
        {
            var icon = ok ? $"{Log.Green}✓{Log.Reset}" : $"{Log.Red}✗{Log.Reset}";
            Console.WriteLine($"{Log.Pre()} {_tag}   {icon} {Log.Dim}{Log.Truncate(output, 90)}{Log.Reset}");
            EventBus.Emit("toolResult", new JObject { ["name"] = name, ["ok"] = ok, ["output"] = Log.Truncate(output, 200), ["actorName"] = _actorName });
        }

        public void Artifact(string action, string path, int? chars = null)
        {
            var detail = chars.HasValue ? $" {Log.Dim}({chars.Value:N0} chars){Log.Reset}" : "";
            Console.WriteLine($"{Log.Pre()} {_tag} {Log.Cyan}📄{Log.Reset} {action} {Log.Bold}{path}{Log.Reset}{detail}");
            EventBus.Emit("artifact", new JObject { ["action"] = action, ["path"] = path, ["chars"] = chars, ["actorName"] = _actorName });
        }

        public void Usage(int inputTokens, int outputTokens, int cachedTokens)
        {
            var cacheRate = inputTokens > 0 ? (int)Math.Round(100.0 * cachedTokens / inputTokens) : 0;
            var cacheInfo = cachedTokens > 0
                ? $" {Log.Green}({cachedTokens} cached, {cacheRate}% hit){Log.Reset}"
                : "";
            Console.WriteLine($"{Log.Pre()} {_tag} {Log.Dim}tokens: {inputTokens} in / {outputTokens} out{cacheInfo}{Log.Reset}");
            EventBus.Emit("usage", new JObject { ["actorName"] = _actorName, ["inputTokens"] = inputTokens, ["outputTokens"] = outputTokens, ["cachedTokens"] = cachedTokens, ["cacheRate"] = cacheRate });
        }
    }

    public static class Log
    {
        // ANSI codes
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

        internal static string Pre() => $"{Dim}[{DateTime.Now:HH:mm:ss}]{Reset}";
        internal static string Truncate(string s, int max = 120) =>
            s != null && s.Length > max ? s.Substring(0, max) + "…" : s ?? "";

        internal static string ActorTag(string name)
        {
            string color;
            if (!ActorColors.TryGetValue(name, out color)) color = Blue;
            return $"{color}[{name}]{Reset}";
        }

        public static void Header(string text)
        {
            int width = Math.Max(text.Length + 4, 50);
            Console.WriteLine($"\n{Cyan}{new string('─', width)}{Reset}");
            Console.WriteLine($"{Cyan}│{Reset} {Bold}{text.PadRight(width - 3)}{Reset}{Cyan}│{Reset}");
            Console.WriteLine($"{Cyan}{new string('─', width)}{Reset}");
            EventBus.Emit("header", new JObject { ["text"] = text });
        }

        public static void Info(string msg)
        {
            Console.WriteLine($"{Pre()} {msg}");
            EventBus.Emit("info", new JObject { ["message"] = msg });
        }

        public static void Success(string msg)
        {
            Console.WriteLine($"{Pre()} {Green}✓{Reset} {msg}");
            EventBus.Emit("success", new JObject { ["message"] = msg });
        }

        public static void Error(string msg)
        {
            Console.WriteLine($"{Pre()} {Red}✗{Reset} {msg}");
            EventBus.Emit("error", new JObject { ["message"] = msg });
        }

        public static void Warn(string msg)
        {
            Console.WriteLine($"{Pre()} {Yellow}⚠{Reset} {msg}");
            EventBus.Emit("warn", new JObject { ["message"] = msg });
        }

        public static void Round(int n, int taskCount)
        {
            Console.WriteLine($"\n{Pre()} \x1b[44m\x1b[37m\x1b[1m ROUND {n} {Reset} {Dim}{taskCount} task(s) ready{Reset}");
            EventBus.Emit("round", new JObject { ["round"] = n, ["taskCount"] = taskCount });
        }

        public static void Actor(string name, string taskTitle)
        {
            Console.WriteLine($"{Pre()} {ActorTag(name)} {Dim}working on{Reset} {Truncate(taskTitle, 60)}");
            EventBus.Emit("actor", new JObject { ["name"] = name, ["taskTitle"] = taskTitle });
        }

        public static void Delegate(string from, string to, string title)
        {
            Console.WriteLine($"{Pre()} {ActorTag(from)} {Magenta}→{Reset} {ActorTag(to)} {Dim}{Truncate(title, 50)}{Reset}");
            EventBus.Emit("delegate", new JObject { ["from"] = from, ["to"] = to, ["title"] = title });
        }

        public static void TaskDone(string actorName, string summary)
        {
            Console.WriteLine($"{Pre()} {ActorTag(actorName)} {Green}✓ completed{Reset} {Dim}{Truncate(summary, 70)}{Reset}");
            EventBus.Emit("taskDone", new JObject { ["actorName"] = actorName, ["summary"] = summary });
        }

        public static void TaskWaiting(string actorName, string reason)
        {
            Console.WriteLine($"{Pre()} {ActorTag(actorName)} {Blue}⏸ waiting{Reset} {Dim}{Truncate(reason, 70)}{Reset}");
            EventBus.Emit("taskWaiting", new JObject { ["actorName"] = actorName, ["reason"] = reason });
        }

        public static void TaskBlocked(string actorName, string reason)
        {
            Console.WriteLine($"{Pre()} {ActorTag(actorName)} {Yellow}◆ blocked{Reset} {Dim}{Truncate(reason, 70)}{Reset}");
            EventBus.Emit("taskBlocked", new JObject { ["actorName"] = actorName, ["reason"] = reason });
        }

        public static void TaskError(string actorName, string error)
        {
            Console.WriteLine($"{Pre()} {ActorTag(actorName)} {Red}✗ error{Reset} {Dim}{Truncate(error, 70)}{Reset}");
            EventBus.Emit("taskError", new JObject { ["actorName"] = actorName, ["error"] = error });
        }

        public static void ArtifactLog(string action, string path, int? chars = null)
        {
            var detail = chars.HasValue ? $" {Dim}({chars.Value:N0} chars){Reset}" : "";
            Console.WriteLine($"{Pre()} {Cyan}📄{Reset} {action} {Bold}{path}{Reset}{detail}");
            EventBus.Emit("artifact", new JObject { ["action"] = action, ["path"] = path, ["chars"] = chars });
        }

        public static void MemoryStatus(int itemCount, int pendingTokens, int observationTokens, int generation)
        {
            Console.WriteLine($"{Pre()} {Magenta}🧠{Reset} {Dim}pending: {pendingTokens} tokens ({itemCount} items) | observations: {observationTokens} tokens (gen {generation}){Reset}");
            EventBus.Emit("memory.status", new JObject { ["itemCount"] = itemCount, ["pendingTokens"] = pendingTokens, ["observationTokens"] = observationTokens, ["generation"] = generation });
        }

        public static void MemoryObserved(int itemCount, int observationLines, int tokens, int sealedSeq)
        {
            Console.WriteLine($"{Pre()} {Magenta}🧠 observed{Reset} {itemCount} items → {observationLines} lines ({tokens} tokens, sealed through #{sealedSeq})");
            EventBus.Emit("memory.observed", new JObject { ["itemCount"] = itemCount, ["observationLines"] = observationLines, ["tokens"] = tokens, ["sealedSeq"] = sealedSeq });
        }

        public static void MemoryReflected(int tokensBefore, int tokensAfter, int level, int generation)
        {
            Console.WriteLine($"{Pre()} {Magenta}🧠 reflected{Reset} {tokensBefore} → {tokensAfter} tokens (level {level}, gen {generation})");
            EventBus.Emit("memory.reflected", new JObject { ["tokensBefore"] = tokensBefore, ["tokensAfter"] = tokensAfter, ["level"] = level, ["generation"] = generation });
        }

        public static void MemorySkipped(int pendingTokens, int threshold)
        {
            Console.WriteLine($"{Pre()} {Magenta}🧠{Reset} {Dim}below threshold ({pendingTokens} < {threshold}), skipped{Reset}");
            EventBus.Emit("memory.skipped", new JObject { ["pendingTokens"] = pendingTokens, ["threshold"] = threshold });
        }

        public static void MemoryPersisted(string filename)
        {
            Console.WriteLine($"{Pre()} {Magenta}🧠{Reset} {Dim}persisted {filename}{Reset}");
            EventBus.Emit("memory.persisted", new JObject { ["filename"] = filename });
        }

        public static void Summary(string label, object value)
        {
            Console.WriteLine($"  {Dim}{label}:{Reset} {Bold}{value}{Reset}");
            EventBus.Emit("summary", new JObject { ["label"] = label, ["value"] = value?.ToString() });
        }

        public static void Done()
        {
            EventBus.Emit("done", new JObject());
        }

        public static ScopedLog Scoped(string actorName) => new ScopedLog(actorName);
    }
}
