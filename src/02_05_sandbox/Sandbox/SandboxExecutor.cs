using System;
using System.Collections.Generic;
using System.Text;
using Acornima;
using Jint;
using Jint.Runtime;
using FourthDevs.Sandbox.Mcp;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Sandbox.Sandbox
{
    /// <summary>
    /// Result of a sandbox code execution.
    /// </summary>
    internal sealed class SandboxResult
    {
        public List<string> Logs { get; } = new List<string>();
        public string Error { get; set; }
    }

    /// <summary>
    /// Executes JavaScript code in an isolated Jint engine.
    /// Loaded MCP tools are exposed as synchronous host functions
    /// (e.g., <c>todo.create({title: "..."}) → {todo: ...}</c>).
    ///
    /// Mirrors 02_05_sandbox/src/sandbox.ts (i-am-alice/4th-devs).
    /// </summary>
    internal static class SandboxExecutor
    {
        /// <summary>
        /// Executes <paramref name="code"/> in a fresh Jint engine.
        /// The engine provides:
        /// <list type="bullet">
        ///   <item>a <c>console</c> object (log, error, warn)</item>
        ///   <item>proxy objects for each loaded MCP server (e.g., <c>todo.create(…)</c>)</item>
        /// </list>
        /// </summary>
        public static SandboxResult Execute(string code)
        {
            var result = new SandboxResult();

            try
            {
                var engine = new Engine(options =>
                {
                    options.LimitMemory(64 * 1024 * 1024);   // 64 MB
                    options.TimeoutInterval(TimeSpan.FromSeconds(10));
                });

                // ---- console ------------------------------------------------
                engine.SetValue("__sandbox_log",
                    new Action<string>(s => result.Logs.Add(s)));

                engine.Execute(@"
var console = {
  log: function() {
    var parts = [];
    for (var i = 0; i < arguments.length; i++) {
      var a = arguments[i];
      parts.push(typeof a === 'object' && a !== null ? JSON.stringify(a) : String(a));
    }
    __sandbox_log(parts.join(' '));
  },
  error: function() {
    var parts = [];
    for (var i = 0; i < arguments.length; i++) {
      var a = arguments[i];
      parts.push(typeof a === 'object' && a !== null ? JSON.stringify(a) : String(a));
    }
    __sandbox_log('[ERROR] ' + parts.join(' '));
  },
  warn: function() {
    var parts = [];
    for (var i = 0; i < arguments.length; i++) {
      var a = arguments[i];
      parts.push(typeof a === 'object' && a !== null ? JSON.stringify(a) : String(a));
    }
    __sandbox_log('[WARN] ' + parts.join(' '));
  }
};");

                // ---- Register host functions for each loaded tool -----------
                var loadedTools = new List<Tuple<string, string>>(McpRegistry.GetLoadedTools());

                foreach (var tool in loadedTools)
                {
                    string serverName = tool.Item1;
                    string toolName   = tool.Item2;
                    string hostFnName = $"__call_{serverName}_{toolName}";

                    string capturedServer = serverName;
                    string capturedTool   = toolName;

                    engine.SetValue(hostFnName,
                        new Func<string, string>(json => InvokeTool(capturedServer, capturedTool, json)));
                }

                // ---- Build API wrapper code + user code --------------------
                string guestCode = BuildGuestCode(code, loadedTools);
                engine.Execute(guestCode);
            }
            catch (JavaScriptException jsEx)
            {
                result.Error = jsEx.Message;
            }
            catch (TimeoutException)
            {
                result.Error = "Execution timed out (10 s limit)";
            }
            catch (MemoryLimitExceededException)
            {
                result.Error = "Memory limit exceeded (64 MB)";
            }
            catch (ParseErrorException pe)
            {
                result.Error = $"Syntax error: {pe.Message}";
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
            }

            return result;
        }

        // ----------------------------------------------------------------
        // Tool dispatcher
        // ----------------------------------------------------------------

        private static string InvokeTool(string serverName, string toolName, string inputJson)
        {
            JObject input;
            try { input = string.IsNullOrWhiteSpace(inputJson) ? new JObject() : JObject.Parse(inputJson); }
            catch { input = new JObject(); }

            switch (serverName.ToLowerInvariant())
            {
                case "todo":
                    return InvokeTodo(toolName, input);
                default:
                    return $"{{\"error\":\"Unknown server: {serverName}\"}}";
            }
        }

        private static string InvokeTodo(string toolName, JObject input)
        {
            switch (toolName.ToLowerInvariant())
            {
                case "create":
                    return TodoStore.Create((string)input["title"]);

                case "get":
                    return TodoStore.Get((string)input["id"]);

                case "list":
                {
                    bool? completed = null;
                    if (input["completed"] != null && input["completed"].Type != JTokenType.Null)
                        completed = (bool)input["completed"];
                    return TodoStore.List(completed);
                }

                case "update":
                {
                    string id        = (string)input["id"];
                    string title     = input["title"]?.Type != JTokenType.Null ? (string)input["title"] : null;
                    bool?  completed = input["completed"] != null && input["completed"].Type != JTokenType.Null
                                       ? (bool?)input["completed"]
                                       : null;
                    return TodoStore.Update(id, title, completed);
                }

                case "delete":
                    return TodoStore.Delete((string)input["id"]);

                default:
                    return $"{{\"error\":\"Unknown todo tool: {toolName}\"}}";
            }
        }

        // ----------------------------------------------------------------
        // Guest code builder
        // ----------------------------------------------------------------

        /// <summary>
        /// Wraps user code with API proxy objects for each loaded server.
        /// Mirrors the <c>buildGuestCode</c> function in sandbox.ts.
        /// </summary>
        private static string BuildGuestCode(string userCode, IList<Tuple<string, string>> loadedTools)
        {
            // Group loaded tools by server name
            var byServer = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in loadedTools)
            {
                if (!byServer.TryGetValue(t.Item1, out List<string> tools))
                    byServer[t.Item1] = tools = new List<string>();
                tools.Add(t.Item2);
            }

            var sb = new StringBuilder();
            foreach (var kv in byServer)
            {
                string serverName = kv.Key;
                sb.AppendLine($"var {serverName} = {{");
                for (int i = 0; i < kv.Value.Count; i++)
                {
                    string toolName   = kv.Value[i];
                    string hostFnName = $"__call_{serverName}_{toolName}";
                    string comma      = i < kv.Value.Count - 1 ? "," : string.Empty;
                    sb.AppendLine($"  {toolName}: function(input) {{ return JSON.parse({hostFnName}(JSON.stringify(input || {{}})));  }}{comma}");
                }
                sb.AppendLine("};");
            }

            sb.AppendLine(userCode);
            return sb.ToString();
        }
    }
}
