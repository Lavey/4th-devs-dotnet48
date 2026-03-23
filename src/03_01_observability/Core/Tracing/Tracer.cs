using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using FourthDevs.Observability.Models;
using Newtonsoft.Json;

namespace FourthDevs.Observability.Core.Tracing
{
    // -----------------------------------------------------------------
    // Parameter types for the various tracing scopes
    // -----------------------------------------------------------------

    internal sealed class TraceParams
    {
        public string Name { get; set; }
        public string SessionId { get; set; }
        public string UserId { get; set; }
        public object Input { get; set; }
    }

    internal sealed class AgentParams
    {
        public string Name { get; set; }
        public string AgentId { get; set; }
        public string Task { get; set; }
    }

    internal sealed class GenerationParams
    {
        public string Name { get; set; }
        public string Model { get; set; }
        public object Input { get; set; }
    }

    internal sealed class ToolParams
    {
        public string Name { get; set; }
        public string CallId { get; set; }
        public object Input { get; set; }
    }

    // -----------------------------------------------------------------
    // GenerationHandle – tracks a single LLM generation span
    // -----------------------------------------------------------------

    internal sealed class GenerationHandle
    {
        private readonly Stopwatch _sw;
        private readonly string _name;
        private long _firstTokenMs;

        public string Id { get; }

        internal GenerationHandle(string id, string name)
        {
            Id = id;
            _name = name;
            _sw = Stopwatch.StartNew();
        }

        public void RecordFirstToken()
        {
            _firstTokenMs = _sw.ElapsedMilliseconds;
            if (TracingManager.IsActive)
            {
                TracingManager.LogTrace("debug", "generation.first_token", new Dictionary<string, object>
                {
                    { "span", _name },
                    { "ttftMs", _firstTokenMs }
                });
            }
        }

        public void End(object output = null, Usage usage = null)
        {
            _sw.Stop();
            if (TracingManager.IsActive)
            {
                var data = new Dictionary<string, object>
                {
                    { "span", _name },
                    { "durationMs", _sw.ElapsedMilliseconds }
                };
                if (output != null) data["output"] = output;
                if (usage != null) data["usage"] = usage;
                TracingManager.LogTrace("info", "generation.end", data);
            }
        }

        public void Error(string code, string message)
        {
            _sw.Stop();
            if (TracingManager.IsActive)
            {
                TracingManager.LogTrace("error", "generation.error", new Dictionary<string, object>
                {
                    { "span", _name },
                    { "code", code },
                    { "error", message },
                    { "durationMs", _sw.ElapsedMilliseconds }
                });
            }
        }
    }

    // -----------------------------------------------------------------
    // Tracer – static methods for scoped tracing
    // -----------------------------------------------------------------

    internal static class Tracer
    {
        /// <summary>Wraps an entire request in a trace scope.</summary>
        public static async Task<T> WithTrace<T>(TraceParams traceParams, Func<Task<T>> fn)
        {
            string traceId = Guid.NewGuid().ToString("N").Substring(0, 16);
            var sw = Stopwatch.StartNew();

            if (TracingManager.IsActive)
            {
                TracingManager.LogTrace("info", "trace.start", new Dictionary<string, object>
                {
                    { "traceId", traceId },
                    { "name", traceParams.Name },
                    { "sessionId", traceParams.SessionId },
                    { "input", traceParams.Input }
                });
            }

            try
            {
                T result = await fn().ConfigureAwait(false);
                sw.Stop();

                if (TracingManager.IsActive)
                {
                    TracingManager.LogTrace("info", "trace.end", new Dictionary<string, object>
                    {
                        { "traceId", traceId },
                        { "name", traceParams.Name },
                        { "durationMs", sw.ElapsedMilliseconds }
                    });
                }

                return result;
            }
            catch (Exception ex)
            {
                sw.Stop();
                if (TracingManager.IsActive)
                {
                    TracingManager.LogTrace("error", "trace.error", new Dictionary<string, object>
                    {
                        { "traceId", traceId },
                        { "name", traceParams.Name },
                        { "error", ex.Message },
                        { "durationMs", sw.ElapsedMilliseconds }
                    });
                }
                throw;
            }
        }

        /// <summary>Records output for the current trace (logged to structured log).</summary>
        public static void SetTraceOutput(object output)
        {
            if (TracingManager.IsActive)
            {
                TracingManager.LogTrace("debug", "trace.output", new Dictionary<string, object>
                {
                    { "output", output is string s ? (object)s : JsonConvert.SerializeObject(output) }
                });
            }
        }

        /// <summary>Wraps agent execution in a span and sets up the <see cref="TracingContextStore"/>.</summary>
        public static async Task<T> WithAgent<T>(AgentParams agentParams, Func<Task<T>> fn)
        {
            var sw = Stopwatch.StartNew();

            if (TracingManager.IsActive)
            {
                TracingManager.LogTrace("info", "agent.start", new Dictionary<string, object>
                {
                    { "agent", agentParams.Name },
                    { "agentId", agentParams.AgentId },
                    { "task", agentParams.Task }
                });
            }

            try
            {
                T result = await TracingContextStore.WithAgentContext(
                    agentParams.Name, agentParams.AgentId, fn).ConfigureAwait(false);

                sw.Stop();
                if (TracingManager.IsActive)
                {
                    TracingManager.LogTrace("info", "agent.end", new Dictionary<string, object>
                    {
                        { "agent", agentParams.Name },
                        { "durationMs", sw.ElapsedMilliseconds }
                    });
                }
                return result;
            }
            catch (Exception ex)
            {
                sw.Stop();
                if (TracingManager.IsActive)
                {
                    TracingManager.LogTrace("error", "agent.error", new Dictionary<string, object>
                    {
                        { "agent", agentParams.Name },
                        { "error", ex.Message },
                        { "durationMs", sw.ElapsedMilliseconds }
                    });
                }
                throw;
            }
        }

        /// <summary>Starts a generation span; caller must call <see cref="GenerationHandle.End"/> when done.</summary>
        public static GenerationHandle StartGeneration(GenerationParams genParams)
        {
            string name = TracingContextStore.FormatGenerationName(genParams.Name ?? "generation");
            string id = Guid.NewGuid().ToString("N").Substring(0, 16);

            if (TracingManager.IsActive)
            {
                TracingManager.LogTrace("info", "generation.start", new Dictionary<string, object>
                {
                    { "span", name },
                    { "model", genParams.Model },
                    { "input", genParams.Input }
                });
            }

            return new GenerationHandle(id, name);
        }

        /// <summary>Wraps a tool execution in a tracing span.</summary>
        public static async Task<T> WithTool<T>(ToolParams toolParams, Func<Task<T>> fn)
        {
            TracingContextStore.AdvanceToolIndex();
            string name = TracingContextStore.FormatToolName(toolParams.Name);
            var sw = Stopwatch.StartNew();

            if (TracingManager.IsActive)
            {
                TracingManager.LogTrace("info", "tool.start", new Dictionary<string, object>
                {
                    { "span", name },
                    { "callId", toolParams.CallId },
                    { "input", toolParams.Input }
                });
            }

            try
            {
                T result = await fn().ConfigureAwait(false);
                sw.Stop();

                if (TracingManager.IsActive)
                {
                    TracingManager.LogTrace("info", "tool.end", new Dictionary<string, object>
                    {
                        { "span", name },
                        { "output", result },
                        { "durationMs", sw.ElapsedMilliseconds }
                    });
                }

                return result;
            }
            catch (Exception ex)
            {
                sw.Stop();
                if (TracingManager.IsActive)
                {
                    TracingManager.LogTrace("error", "tool.error", new Dictionary<string, object>
                    {
                        { "span", name },
                        { "error", ex.Message },
                        { "durationMs", sw.ElapsedMilliseconds }
                    });
                }
                throw;
            }
        }

        /// <summary>Records an error against the current trace.</summary>
        public static void RecordTraceError(string code, string message)
        {
            if (TracingManager.IsActive)
            {
                TracingManager.LogTrace("error", "trace.recordError", new Dictionary<string, object>
                {
                    { "code", code },
                    { "error", message }
                });
            }
        }
    }
}
