using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using FourthDevs.Common;
using FourthDevs.Common.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Lesson02_ToolUse
{
    /// <summary>
    /// Lesson 02 – Tool Use (Sandboxed Filesystem)
    /// The model is given a set of filesystem tools (list, read, write, delete,
    /// mkdir, file_info) that operate exclusively inside a sandbox directory.
    /// Path-traversal attempts are blocked at the tool layer.
    ///
    /// Source: 01_02_tool_use/app.js (i-am-alice/4th-devs)
    /// </summary>
    internal static class Program
    {
        private const string Model    = "gpt-4.1-mini";
        private const int    MaxSteps = 10;

        // Sandbox root – created next to the executable at runtime
        private static string _sandboxRoot;

        static void Main(string[] args)
        {
            MainAsync().GetAwaiter().GetResult();
        }

        static async Task MainAsync()
        {
            _sandboxRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sandbox");
            InitSandbox();
            Console.WriteLine($"Sandbox: {_sandboxRoot}  (empty state)\n");

            var queries = new[]
            {
                "What files are in the sandbox?",
                "Create a file called hello.txt with content: 'Hello, World!'",
                "Read the hello.txt file",
                "Get info about hello.txt",
                "Create a directory called 'docs'",
                "Create a file docs/readme.txt with content: 'Documentation folder'",
                "List files in the docs directory",
                "Delete the hello.txt file",
                "Try to read ../config.js"   // security test – should be blocked
            };

            foreach (string query in queries)
            {
                Console.WriteLine($"Q: {query}");
                string answer = await RunQuery(query);
                Console.WriteLine($"A: {answer}");
                Console.WriteLine();
            }
        }

        // =====================================================================
        // Sandbox helpers
        // =====================================================================

        static void InitSandbox()
        {
            if (Directory.Exists(_sandboxRoot))
            {
                // Wipe contents for a clean run
                foreach (string f in Directory.GetFiles(_sandboxRoot, "*", SearchOption.AllDirectories))
                    File.Delete(f);
                foreach (string d in Directory.GetDirectories(_sandboxRoot))
                    Directory.Delete(d, recursive: true);
            }
            else
            {
                Directory.CreateDirectory(_sandboxRoot);
            }
        }

        /// <summary>
        /// Resolves a relative path inside the sandbox, blocking path traversal.
        /// Returns null if the resolved path escapes the sandbox.
        /// </summary>
        static string ResolveSandboxPath(string relativePath)
        {
            string full = Path.GetFullPath(Path.Combine(_sandboxRoot, relativePath));
            return full.StartsWith(_sandboxRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                   || string.Equals(full, _sandboxRoot, StringComparison.OrdinalIgnoreCase)
                ? full
                : null;
        }

        // =====================================================================
        // Tool definitions
        // =====================================================================

        static readonly List<ToolDefinition> Tools = new List<ToolDefinition>
        {
            new ToolDefinition
            {
                Type = "function", Name = "list_files",
                Description = "List files and directories inside the sandbox (or a sub-path)",
                Parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        path = new { type = "string", description = "Relative path inside the sandbox (use '.' for root)" }
                    },
                    required = new[] { "path" }, additionalProperties = false
                },
                Strict = true
            },
            new ToolDefinition
            {
                Type = "function", Name = "read_file",
                Description = "Read the text content of a file in the sandbox",
                Parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        path = new { type = "string", description = "Relative file path inside the sandbox" }
                    },
                    required = new[] { "path" }, additionalProperties = false
                },
                Strict = true
            },
            new ToolDefinition
            {
                Type = "function", Name = "write_file",
                Description = "Create or overwrite a file in the sandbox with given text content",
                Parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        path    = new { type = "string", description = "Relative file path" },
                        content = new { type = "string", description = "Text content to write" }
                    },
                    required = new[] { "path", "content" }, additionalProperties = false
                },
                Strict = true
            },
            new ToolDefinition
            {
                Type = "function", Name = "delete_file",
                Description = "Delete a file from the sandbox",
                Parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        path = new { type = "string", description = "Relative file path" }
                    },
                    required = new[] { "path" }, additionalProperties = false
                },
                Strict = true
            },
            new ToolDefinition
            {
                Type = "function", Name = "create_directory",
                Description = "Create a directory (and any missing parents) in the sandbox",
                Parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        path = new { type = "string", description = "Relative directory path" }
                    },
                    required = new[] { "path" }, additionalProperties = false
                },
                Strict = true
            },
            new ToolDefinition
            {
                Type = "function", Name = "file_info",
                Description = "Get metadata about a file (size, creation date, last modified)",
                Parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        path = new { type = "string", description = "Relative file path" }
                    },
                    required = new[] { "path" }, additionalProperties = false
                },
                Strict = true
            }
        };

        // =====================================================================
        // Tool execution
        // =====================================================================

        static object ExecuteTool(string name, JObject args)
        {
            switch (name)
            {
                case "list_files":
                {
                    string rel     = args["path"]?.ToString() ?? ".";
                    string absPath = ResolveSandboxPath(rel);
                    if (absPath == null) return new { error = "Access denied: path outside sandbox." };
                    if (!Directory.Exists(absPath)) return new { error = $"Directory not found: {rel}" };

                    var entries = new List<object>();
                    foreach (string d in Directory.GetDirectories(absPath))
                        entries.Add(new { type = "directory", name = Path.GetFileName(d) });
                    foreach (string f in Directory.GetFiles(absPath))
                        entries.Add(new { type = "file", name = Path.GetFileName(f) });

                    return new { path = rel, entries };
                }

                case "read_file":
                {
                    string rel     = args["path"]?.ToString() ?? string.Empty;
                    string absPath = ResolveSandboxPath(rel);
                    if (absPath == null) return new { error = "Access denied: path outside sandbox." };
                    if (!File.Exists(absPath))  return new { error = $"File not found: {rel}" };

                    return new { path = rel, content = File.ReadAllText(absPath, Encoding.UTF8) };
                }

                case "write_file":
                {
                    string rel     = args["path"]?.ToString() ?? string.Empty;
                    string content = args["content"]?.ToString() ?? string.Empty;
                    string absPath = ResolveSandboxPath(rel);
                    if (absPath == null) return new { error = "Access denied: path outside sandbox." };

                    Directory.CreateDirectory(Path.GetDirectoryName(absPath));
                    File.WriteAllText(absPath, content, Encoding.UTF8);
                    return new { success = true, path = rel, bytesWritten = Encoding.UTF8.GetByteCount(content) };
                }

                case "delete_file":
                {
                    string rel     = args["path"]?.ToString() ?? string.Empty;
                    string absPath = ResolveSandboxPath(rel);
                    if (absPath == null) return new { error = "Access denied: path outside sandbox." };
                    if (!File.Exists(absPath))  return new { error = $"File not found: {rel}" };

                    File.Delete(absPath);
                    return new { success = true, path = rel };
                }

                case "create_directory":
                {
                    string rel     = args["path"]?.ToString() ?? string.Empty;
                    string absPath = ResolveSandboxPath(rel);
                    if (absPath == null) return new { error = "Access denied: path outside sandbox." };

                    Directory.CreateDirectory(absPath);
                    return new { success = true, path = rel };
                }

                case "file_info":
                {
                    string rel     = args["path"]?.ToString() ?? string.Empty;
                    string absPath = ResolveSandboxPath(rel);
                    if (absPath == null) return new { error = "Access denied: path outside sandbox." };
                    if (!File.Exists(absPath))  return new { error = $"File not found: {rel}" };

                    var info = new FileInfo(absPath);
                    return new
                    {
                        path         = rel,
                        sizeBytes    = info.Length,
                        created      = info.CreationTimeUtc.ToString("O"),
                        lastModified = info.LastWriteTimeUtc.ToString("O")
                    };
                }

                default:
                    throw new InvalidOperationException($"Unknown tool: {name}");
            }
        }

        // =====================================================================
        // Tool-calling loop
        // =====================================================================

        static async Task<string> RunQuery(string userQuery)
        {
            var inputItems = new List<object>
            {
                new { type = "message", role = "user", content = userQuery }
            };

            for (int step = 0; step < MaxSteps; step++)
            {
                var body = new JObject
                {
                    ["model"] = AiConfig.ResolveModel(Model),
                    ["input"] = JArray.FromObject(inputItems),
                    ["tools"] = JArray.FromObject(Tools)
                };

                string responseJson = await PostRawAsync(body.ToString(Formatting.None));
                var parsed = JsonConvert.DeserializeObject<ResponsesResponse>(responseJson);

                if (parsed?.Error != null)
                    throw new InvalidOperationException(parsed.Error.Message);

                var toolCalls = ResponsesApiClient.GetToolCalls(parsed);

                if (toolCalls.Count == 0)
                    return ResponsesApiClient.ExtractText(parsed);

                // Append assistant output items to the conversation
                foreach (var item in parsed.Output)
                {
                    if (item.Type == "function_call")
                    {
                        inputItems.Add(new
                        {
                            type = "function_call",
                            call_id = item.CallId,
                            name = item.Name,
                            arguments = item.Arguments
                        });
                    }
                }

                // Execute each tool and add results
                foreach (var call in toolCalls)
                {
                    var toolArgs   = JObject.Parse(call.Arguments ?? "{}");
                    var toolResult = ExecuteTool(call.Name, toolArgs);
                    string resultJson = JsonConvert.SerializeObject(toolResult);

                    Console.WriteLine($"  [tool] {call.Name}({call.Arguments}) → {resultJson}");

                    inputItems.Add(new
                    {
                        type    = "function_call_output",
                        call_id = call.CallId,
                        output  = resultJson
                    });
                }
            }

            throw new InvalidOperationException($"Tool calling did not finish within {MaxSteps} steps.");
        }

        static async Task<string> PostRawAsync(string jsonBody)
        {
            using (var http = new HttpClient())
            {
                http.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", AiConfig.ApiKey);

                if (AiConfig.Provider == "openrouter")
                {
                    if (!string.IsNullOrWhiteSpace(AiConfig.HttpReferer))
                        http.DefaultRequestHeaders.TryAddWithoutValidation("HTTP-Referer", AiConfig.HttpReferer);
                    if (!string.IsNullOrWhiteSpace(AiConfig.AppName))
                        http.DefaultRequestHeaders.TryAddWithoutValidation("X-Title", AiConfig.AppName);
                }

                using (var content = new StringContent(jsonBody, Encoding.UTF8, "application/json"))
                using (var response = await http.PostAsync(AiConfig.ApiEndpoint, content))
                {
                    return await response.Content.ReadAsStringAsync();
                }
            }
        }
    }
}
