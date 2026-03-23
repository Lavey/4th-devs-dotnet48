using System;
using System.Threading;
using System.Threading.Tasks;

namespace FourthDevs.Evals.Core.Tracing
{
    internal sealed class PromptRef
    {
        public string Name { get; set; }
        public int Version { get; set; }
        public bool IsFallback { get; set; }
    }

    internal sealed class TracingContext
    {
        public string AgentName { get; set; }
        public string AgentId { get; set; }
        public int TurnNumber { get; set; }
        public int ToolIndex { get; set; }
        public PromptRef PromptRef { get; set; }
    }

    /// <summary>
    /// Static helper to manage <see cref="AsyncLocal{T}"/>-based tracing context
    /// for hierarchical trace naming.
    /// </summary>
    internal static class TracingContextStore
    {
        private static readonly AsyncLocal<TracingContext> _storage = new AsyncLocal<TracingContext>();

        public static TracingContext Current
        {
            get { return _storage.Value; }
        }

        public static async Task<T> WithAgentContext<T>(string agentName, string agentId, Func<Task<T>> fn)
        {
            var ctx = new TracingContext
            {
                AgentName = agentName,
                AgentId = agentId,
                TurnNumber = 0,
                ToolIndex = 0
            };
            _storage.Value = ctx;
            try
            {
                return await fn().ConfigureAwait(false);
            }
            finally
            {
                _storage.Value = null;
            }
        }

        /// <summary>Increments and returns the new turn number.</summary>
        public static int AdvanceTurn()
        {
            var ctx = _storage.Value;
            if (ctx == null) return 1;
            ctx.TurnNumber++;
            ctx.ToolIndex = 0;
            return ctx.TurnNumber;
        }

        /// <summary>Increments and returns the new tool index within the current turn.</summary>
        public static int AdvanceToolIndex()
        {
            var ctx = _storage.Value;
            if (ctx == null) return 1;
            ctx.ToolIndex++;
            return ctx.ToolIndex;
        }

        /// <summary>Formats a generation name like "alice:t3:generation".</summary>
        public static string FormatGenerationName(string baseName = "generation")
        {
            var ctx = _storage.Value;
            if (ctx == null) return baseName;
            return string.Format("{0}:t{1}:{2}", ctx.AgentName, ctx.TurnNumber, baseName);
        }

        /// <summary>Formats a tool name like "alice:t3:tool[1]:get_current_time".</summary>
        public static string FormatToolName(string toolName)
        {
            var ctx = _storage.Value;
            if (ctx == null) return toolName;
            return string.Format("{0}:t{1}:tool[{2}]:{3}",
                ctx.AgentName, ctx.TurnNumber, ctx.ToolIndex, toolName);
        }
    }
}
