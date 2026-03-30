using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Acornima;
using Jint;
using Jint.Runtime;
using FourthDevs.Garden.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Garden.Tools
{
    /// <summary>
    /// Execute JavaScript scripts via Jint with codemode vault helpers.
    /// Port of 04_01_garden/src/tools/code-mode.ts.
    /// </summary>
    internal static class CodeModeTool
    {
        private const string ResultMarker = "__CODE_MODE_RESULT__=";
        private const string ErrorMarker = "__CODE_MODE_ERROR__=";
        private const int DefaultTimeout = 30;
        private const int MaxTimeout = 300;

        private static readonly string BaseDir = AppDomain.CurrentDomain.BaseDirectory;
        private static readonly string VaultDir = Path.Combine(BaseDir, "vault");

        public static readonly LocalToolDefinition Definition = new LocalToolDefinition
        {
            Name = "code_mode",
            Description =
                "Execute JavaScript in a sandbox. Provide inline script or script_path to a skill script. " +
                "The script receives `input` (JSON) and `codemode` helpers (vault.read/write/list/search/move, " +
                "runtime.exec, output.set/get). All calls are synchronous (no async/await).",
            Parameters = new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["script"] = new JObject
                    {
                        ["type"] = "string",
                        ["description"] = "Inline JavaScript function body."
                    },
                    ["script_path"] = new JObject
                    {
                        ["type"] = "string",
                        ["description"] = "Path to skill script under vault/system/skills/**/scripts/*."
                    },
                    ["input"] = new JObject
                    {
                        ["type"] = "object",
                        ["description"] = "JSON passed to the script as `input`.",
                        ["additionalProperties"] = true
                    },
                    ["timeout_seconds"] = new JObject
                    {
                        ["type"] = "integer",
                        ["description"] = "Timeout in seconds (1-" + MaxTimeout + ")."
                    }
                },
                ["additionalProperties"] = false
            },
            Handler = ExecuteAsync
        };

        private static Task<ToolExecutionResult> ExecuteAsync(JObject args)
        {
            try
            {
                string script = ResolveScript(args);
                int timeout = DefaultTimeout;
                if (args["timeout_seconds"] != null && args["timeout_seconds"].Type == JTokenType.Integer)
                {
                    timeout = (int)args["timeout_seconds"];
                    timeout = Math.Max(1, Math.Min(timeout, MaxTimeout));
                }

                JObject inputObj = args["input"] as JObject;
                if (inputObj == null) inputObj = new JObject();

                string rawOutput = RunInJint(script, inputObj, timeout);
                ParsedOutput parsed = ParseOutput(rawOutput);

                var resultObj = new JObject { ["ok"] = parsed.Ok };
                if (parsed.Result != null)
                    resultObj["result"] = parsed.Result;
                if (parsed.Error != null)
                    resultObj["error"] = parsed.Error;
                resultObj["logs"] = parsed.Logs;

                return Task.FromResult(new ToolExecutionResult(
                    parsed.Ok,
                    resultObj.ToString(Formatting.Indented)));
            }
            catch (Exception ex)
            {
                var errorObj = new JObject
                {
                    ["ok"] = false,
                    ["error"] = ex.Message
                };
                return Task.FromResult(new ToolExecutionResult(
                    false,
                    errorObj.ToString(Formatting.Indented)));
            }
        }

        // ----------------------------------------------------------------
        // Script resolution
        // ----------------------------------------------------------------

        private static string ResolveScript(JObject args)
        {
            string inline = null;
            string scriptPath = null;

            if (args["script"] != null && args["script"].Type == JTokenType.String)
            {
                string val = ((string)args["script"]).Trim();
                if (val.Length > 0) inline = val;
            }

            if (args["script_path"] != null && args["script_path"].Type == JTokenType.String)
            {
                string val = ((string)args["script_path"]).Trim();
                if (val.Length > 0) scriptPath = val;
            }

            if (inline != null && scriptPath != null)
                throw new InvalidOperationException("Provide either \"script\" or \"script_path\", not both.");
            if (inline == null && scriptPath == null)
                throw new InvalidOperationException("One of \"script\" or \"script_path\" is required.");

            if (inline != null) return inline;

            // Resolve script_path relative to project base
            string fullPath = Path.Combine(BaseDir, scriptPath.Replace('/', Path.DirectorySeparatorChar));
            fullPath = Path.GetFullPath(fullPath);
            if (!File.Exists(fullPath))
                throw new FileNotFoundException("script_path not found: " + scriptPath);

            return File.ReadAllText(fullPath, Encoding.UTF8);
        }

        // ----------------------------------------------------------------
        // Jint execution
        // ----------------------------------------------------------------

        private static string RunInJint(string userScript, JObject inputObj, int timeoutSeconds)
        {
            var logs = new List<string>();
            object codemodeResult = null;

            var engine = new Engine(cfg =>
            {
                cfg.LimitMemory(64 * 1024 * 1024);
                cfg.TimeoutInterval(TimeSpan.FromSeconds(timeoutSeconds));
            });

            // console object
            engine.SetValue("__sandbox_log", new Action<string>(s => logs.Add(s)));
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

            // codemode.vault helpers
            engine.SetValue("__vault_read", new Func<string, string>(VaultRead));
            engine.SetValue("__vault_write", new Func<string, string, string>(VaultWrite));
            engine.SetValue("__vault_list", new Func<string, string>(VaultList));
            engine.SetValue("__vault_search", new Func<string, string, int, string>(VaultSearch));
            engine.SetValue("__vault_move", new Func<string, string, string>(VaultMove));
            engine.SetValue("__runtime_exec", new Func<string, string>(RuntimeExec));

            // codemode.output helpers via closures
            engine.SetValue("__output_set", new Action<string>(v =>
            {
                codemodeResult = v;
            }));
            engine.SetValue("__output_get", new Func<string>(() =>
            {
                if (codemodeResult == null) return "undefined";
                return codemodeResult.ToString();
            }));

            // Build the codemode JS object and provide input
            string inputJson = inputObj.ToString(Formatting.None);
            string setupCode = @"
var __codemode_value = undefined;
var codemode = {
  vault: {
    read: function(path) { return __vault_read(path); },
    write: function(path, content) { return JSON.parse(__vault_write(path, typeof content === 'string' ? content : String(content))); },
    list: function(path) { return JSON.parse(__vault_list(path || '')); },
    search: function(path, pattern, max) { return JSON.parse(__vault_search(path || '', pattern || '', max || 200)); },
    move: function(from, to) { return JSON.parse(__vault_move(from, to)); }
  },
  runtime: {
    exec: function(command) { return JSON.parse(__runtime_exec(command)); }
  },
  output: {
    set: function(v) { __codemode_value = v; __output_set(typeof v === 'object' ? JSON.stringify(v) : String(v)); },
    get: function() { return __codemode_value; }
  }
};
var input = " + inputJson + @";
";
            engine.Execute(setupCode);

            // Strip async/await from user script for sync Jint execution
            string syncScript = StripAsyncAwait(userScript);

            // Wrap user script in runner
            string wrappedScript = @"
var __user_result = (function() {
" + syncScript + @"
})();
var __final = codemode.output.get();
if (__final === undefined) __final = __user_result;
if (__final === undefined) __final = { status: 'completed' };
console.log('" + ResultMarker + @"' + JSON.stringify(__final));
";

            try
            {
                engine.Execute(wrappedScript);
            }
            catch (JavaScriptException jsEx)
            {
                logs.Add(ErrorMarker + JsonConvert.SerializeObject(new { message = jsEx.Message }));
            }
            catch (TimeoutException)
            {
                logs.Add(ErrorMarker + JsonConvert.SerializeObject(new { message = "Execution timed out (" + timeoutSeconds + "s limit)" }));
            }
            catch (MemoryLimitExceededException)
            {
                logs.Add(ErrorMarker + JsonConvert.SerializeObject(new { message = "Memory limit exceeded (64 MB)" }));
            }
            catch (ParseErrorException pe)
            {
                logs.Add(ErrorMarker + JsonConvert.SerializeObject(new { message = "Syntax error: " + pe.Message }));
            }

            return string.Join("\n", logs);
        }

        /// <summary>
        /// Strips async/await keywords so scripts can run synchronously in Jint.
        /// </summary>
        private static string StripAsyncAwait(string script)
        {
            // Remove "await " keyword
            string result = Regex.Replace(script, @"\bawait\s+", "");
            // Remove "async " keyword before function declarations and arrows
            result = Regex.Replace(result, @"\basync\s+", "");
            return result;
        }

        // ----------------------------------------------------------------
        // Vault helper implementations
        // ----------------------------------------------------------------

        private static string ToAbsolute(string relativePath)
        {
            string normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
            string abs = Path.Combine(VaultDir, normalized);
            return Path.GetFullPath(abs);
        }

        private static string VaultRead(string path)
        {
            string abs = ToAbsolute(path);
            return File.ReadAllText(abs, Encoding.UTF8);
        }

        private static string VaultWrite(string path, string content)
        {
            string abs = ToAbsolute(path);
            string dir = Path.GetDirectoryName(abs);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(abs, content, Encoding.UTF8);
            int bytes = Encoding.UTF8.GetByteCount(content);
            var result = new JObject { ["path"] = path, ["bytes_written"] = bytes };
            return result.ToString(Formatting.None);
        }

        private static string VaultList(string path)
        {
            string abs = string.IsNullOrEmpty(path) ? VaultDir : ToAbsolute(path);
            var entries = new JArray();
            if (Directory.Exists(abs))
            {
                foreach (string item in Directory.GetFileSystemEntries(abs))
                {
                    bool isDir = Directory.Exists(item);
                    entries.Add(new JObject
                    {
                        ["name"] = Path.GetFileName(item),
                        ["is_dir"] = isDir
                    });
                }
            }
            return entries.ToString(Formatting.None);
        }

        private static string VaultSearch(string path, string pattern, int maxResults)
        {
            string abs = string.IsNullOrEmpty(path) ? VaultDir : ToAbsolute(path);
            var matches = new JArray();
            SearchDirectory(abs, path, pattern, maxResults, matches);
            return matches.ToString(Formatting.None);
        }

        private static void SearchDirectory(string absPath, string relPath, string pattern, int max, JArray matches)
        {
            if (matches.Count >= max) return;
            if (!Directory.Exists(absPath)) return;

            try
            {
                foreach (string file in Directory.GetFiles(absPath))
                {
                    if (matches.Count >= max) break;
                    try
                    {
                        string text = File.ReadAllText(file, Encoding.UTF8);
                        string[] lines = text.Split(new[] { '\n' }, StringSplitOptions.None);
                        string childRel = string.IsNullOrEmpty(relPath)
                            ? Path.GetFileName(file)
                            : relPath + "/" + Path.GetFileName(file);
                        for (int i = 0; i < lines.Length; i++)
                        {
                            if (matches.Count >= max) break;
                            if (lines[i].IndexOf(pattern, StringComparison.Ordinal) >= 0)
                            {
                                matches.Add(new JObject
                                {
                                    ["path"] = childRel,
                                    ["line"] = i + 1,
                                    ["content"] = lines[i]
                                });
                            }
                        }
                    }
                    catch { /* skip unreadable files */ }
                }

                foreach (string dir in Directory.GetDirectories(absPath))
                {
                    if (matches.Count >= max) break;
                    string dirName = Path.GetFileName(dir);
                    string childRel = string.IsNullOrEmpty(relPath) ? dirName : relPath + "/" + dirName;
                    SearchDirectory(dir, childRel, pattern, max, matches);
                }
            }
            catch { /* skip inaccessible directories */ }
        }

        private static string VaultMove(string from, string to)
        {
            string absFrom = ToAbsolute(from);
            string absTo = ToAbsolute(to);
            string dir = Path.GetDirectoryName(absTo);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.Move(absFrom, absTo);
            var result = new JObject { ["from"] = from, ["to"] = to };
            return result.ToString(Formatting.None);
        }

        private static string RuntimeExec(string command)
        {
            int exitCode = 0;
            string stdout = string.Empty;
            string stderr = string.Empty;

            try
            {
                bool isWindows = Environment.OSVersion.Platform == PlatformID.Win32NT;
                string shell = isWindows ? "cmd.exe" : "/bin/sh";
                string shellArgs = isWindows ? "/c " + command : "-c " + command;

                var psi = new ProcessStartInfo
                {
                    FileName = shell,
                    Arguments = shellArgs,
                    WorkingDirectory = VaultDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = new Process { StartInfo = psi })
                {
                    process.Start();
                    stdout = process.StandardOutput.ReadToEnd();
                    stderr = process.StandardError.ReadToEnd();
                    process.WaitForExit(30000);
                    exitCode = process.ExitCode;
                }
            }
            catch (Exception ex)
            {
                stderr = ex.Message;
                exitCode = 1;
            }

            var result = new JObject
            {
                ["exit_code"] = exitCode,
                ["stdout"] = stdout.TrimEnd(),
                ["stderr"] = stderr.TrimEnd()
            };
            return result.ToString(Formatting.None);
        }

        // ----------------------------------------------------------------
        // Output parsing
        // ----------------------------------------------------------------

        private sealed class ParsedOutput
        {
            public bool Ok;
            public JToken Result;
            public string Error;
            public string Logs = string.Empty;
        }

        private static ParsedOutput ParseOutput(string raw)
        {
            if (raw == null) raw = string.Empty;
            string[] lines = raw.Replace("\r\n", "\n").Split(new[] { '\n' }, StringSplitOptions.None);

            var logLines = new List<string>();
            foreach (string line in lines)
            {
                if (!line.StartsWith(ResultMarker) && !line.StartsWith(ErrorMarker))
                    logLines.Add(line);
            }
            string logs = string.Join("\n", logLines).Trim();

            // Search from end for markers
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                if (lines[i].StartsWith(ErrorMarker))
                {
                    string payload = lines[i].Substring(ErrorMarker.Length);
                    string message = "Script failed";
                    try
                    {
                        var obj = JObject.Parse(payload);
                        if (obj["message"] != null)
                            message = (string)obj["message"];
                    }
                    catch { /* use default message */ }

                    return new ParsedOutput { Ok = false, Error = message, Logs = logs };
                }

                if (lines[i].StartsWith(ResultMarker))
                {
                    string payload = lines[i].Substring(ResultMarker.Length);
                    JToken result = null;
                    try { result = JToken.Parse(payload); }
                    catch { /* null result */ }

                    return new ParsedOutput { Ok = true, Result = result, Logs = logs };
                }
            }

            return new ParsedOutput { Ok = false, Error = "No result marker in output", Logs = logs };
        }
    }
}
