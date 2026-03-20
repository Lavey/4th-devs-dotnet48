using System;

namespace FourthDevs.Lesson05_Agent.Events
{
    /// <summary>
    /// Event-driven lifecycle logging.
    ///
    /// Subscribes to the agent event emitter and produces structured,
    /// human-readable coloured console logs for every lifecycle transition.
    ///
    /// Mirrors 01_05_agent/src/lib/event-logger.ts (i-am-alice/4th-devs).
    /// </summary>
    internal static class EventLogger
    {
        private static string Ts()
        {
            return DateTime.UtcNow.ToString("HH:mm:ss.fff");
        }

        private static string FmtTokens(TokenUsage u)
        {
            if (u == null) return string.Empty;
            string cached = u.CachedTokens > 0
                ? string.Format(" ({0} cached)", u.CachedTokens)
                : string.Empty;
            return string.Format("{0} in, {1} out{2}", u.InputTokens, u.OutputTokens, cached);
        }

        private static string Truncate(string s, int max = 120)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Length > max ? s.Substring(0, max) + "…" : s;
        }

        /// <summary>
        /// Subscribe to all events and log them. Returns an unsubscribe action.
        /// </summary>
        internal static Action Subscribe(AgentEventEmitter emitter)
        {
            return emitter.OnAny(HandleEvent);
        }

        private static void HandleEvent(AgentEvent evt)
        {
            switch (evt.Type)
            {
                case "agent.started":
                    var started = (AgentStartedEvent)evt;
                    LogInfo(evt.Ctx,
                        string.Format("started — {0} ({1})",
                            started.AgentName ?? "agent", started.Model));
                    break;

                case "agent.completed":
                    var completed = (AgentCompletedEvent)evt;
                    var secs = (completed.DurationMs / 1000.0).ToString("F1");
                    var tokens = FmtTokens(completed.Usage);
                    LogInfo(evt.Ctx,
                        string.Format("completed — {0}s{1}",
                            secs,
                            tokens.Length > 0 ? ", " + tokens : string.Empty));
                    break;

                case "agent.failed":
                    var failed = (AgentFailedEvent)evt;
                    LogError(evt.Ctx, "failed — " + failed.Error);
                    break;

                case "agent.cancelled":
                    LogWarn(evt.Ctx, "cancelled");
                    break;

                case "agent.waiting":
                    var waiting = (AgentWaitingEvent)evt;
                    int count = waiting.WaitingFor != null ? waiting.WaitingFor.Count : 0;
                    LogInfo(evt.Ctx,
                        string.Format("waiting for {0} tool(s)", count));
                    break;

                case "agent.resumed":
                    var resumed = (AgentResumedEvent)evt;
                    LogInfo(evt.Ctx,
                        string.Format("resumed — {0} remaining", resumed.Remaining));
                    break;

                case "turn.started":
                    var turnStarted = (TurnStartedEvent)evt;
                    LogInfo(evt.Ctx,
                        string.Format("turn {0}", turnStarted.TurnCount));
                    break;

                case "turn.completed":
                    var turnDone = (TurnCompletedEvent)evt;
                    var turnTokens = FmtTokens(turnDone.Usage);
                    LogInfo(evt.Ctx,
                        string.Format("turn {0} done{1}",
                            turnDone.TurnCount,
                            turnTokens.Length > 0 ? " — " + turnTokens : string.Empty));
                    break;

                case "generation.completed":
                    var gen = (GenerationCompletedEvent)evt;
                    var genSecs = (gen.DurationMs / 1000.0).ToString("F1");
                    var genTokens = FmtTokens(gen.Usage);
                    LogInfo(evt.Ctx,
                        string.Format("generation {0} — {1}s{2}",
                            gen.Model, genSecs,
                            genTokens.Length > 0 ? ", " + genTokens : string.Empty));
                    break;

                case "tool.called":
                    var called = (ToolCalledEvent)evt;
                    LogTool(evt.Ctx, called.Name + " called");
                    break;

                case "tool.completed":
                    var toolDone = (ToolCompletedEvent)evt;
                    var toolSecs = (toolDone.DurationMs / 1000.0).ToString("F1");
                    LogTool(evt.Ctx,
                        string.Format("{0} ok — {1}s", toolDone.Name, toolSecs));
                    break;

                case "tool.failed":
                    var toolFail = (ToolFailedEvent)evt;
                    LogWarn(evt.Ctx,
                        string.Format("{0} failed — {1}", toolFail.Name, toolFail.Error));
                    break;

                default:
                    LogInfo(evt.Ctx, evt.Type);
                    break;
            }
        }

        // ── Coloured console helpers ─────────────────────────────────

        private static void LogInfo(EventContext ctx, string msg)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("[" + Ts() + "] ");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("[agent] ");
            if (ctx != null && ctx.Depth > 0)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write("d" + ctx.Depth + " ");
            }
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(msg);
            Console.ResetColor();
        }

        private static void LogWarn(EventContext ctx, string msg)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("[" + Ts() + "] ");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("[agent] ");
            Console.WriteLine(msg);
            Console.ResetColor();
        }

        private static void LogError(EventContext ctx, string msg)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("[" + Ts() + "] ");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write("[agent] ");
            Console.WriteLine(msg);
            Console.ResetColor();
        }

        private static void LogTool(EventContext ctx, string msg)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("[" + Ts() + "] ");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("⚡ ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(msg);
            Console.ResetColor();
        }
    }
}
