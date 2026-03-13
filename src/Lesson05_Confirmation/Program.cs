using System;
using System.Collections.Generic;
using System.Configuration;
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

namespace FourthDevs.Lesson05_Confirmation
{
    /// <summary>
    /// Lesson 05 – Confirmation (File &amp; Email Agent)
    ///
    /// Interactive REPL where an agent can:
    ///   1. Read, write, list and search workspace files
    ///   2. Send emails via the Resend API
    ///
    /// Before executing send_email the agent pauses and asks the user
    /// for confirmation (Y/N) or to trust the tool for the session (T).
    /// Recipients are validated against workspace/whitelist.json.
    ///
    /// REPL commands: 'exit' | 'clear' | 'untrust'
    ///
    /// Source: 01_05_confirmation/app.js (i-am-alice/4th-devs)
    /// </summary>
    internal static class Program
    {
        private const string Model    = "gpt-4.1-mini";
        private const int    MaxSteps = 50;

        // Tools that pause for user confirmation before execution
        private static readonly HashSet<string> ConfirmationRequired =
            new HashSet<string> { "send_email" };

        // Tools trusted for the current session (skip confirmation)
        private static readonly HashSet<string> TrustedTools =
            new HashSet<string>();

        private static string _workspaceRoot;
        private static string _whitelistPath;

        // ----------------------------------------------------------------
        // Resend config (read directly from App.config)
        // ----------------------------------------------------------------

        private static string ResendApiKey =>
            ConfigurationManager.AppSettings["RESEND_API_KEY"]?.Trim() ?? string.Empty;

        private static string ResendFrom =>
            ConfigurationManager.AppSettings["RESEND_FROM"]?.Trim() ?? string.Empty;

        // ----------------------------------------------------------------
        // Entry point
        // ----------------------------------------------------------------

        static void Main(string[] args)
        {
            MainAsync().GetAwaiter().GetResult();
        }

        static async Task MainAsync()
        {
            _workspaceRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "workspace");
            _whitelistPath = Path.Combine(_workspaceRoot, "whitelist.json");

            EnsureWorkspace();

            Console.WriteLine("=== File & Email Agent ===");
            Console.WriteLine("Type your query. Special commands: 'exit' | 'clear' | 'untrust'");
            Console.WriteLine();

            PrintExamples();

            var conversation = new List<object>();

            while (true)
            {
                Console.Write("You: ");
                string input = Console.ReadLine();

                if (input == null) break; // EOF / Ctrl-Z

                string trimmed = input.Trim();

                if (string.IsNullOrEmpty(trimmed)) continue;

                string lower = trimmed.ToLowerInvariant();

                if (lower == "exit" || lower == "quit")
                    break;

                if (lower == "clear")
                {
                    conversation.Clear();
                    ColorLine("  [Conversation cleared]", ConsoleColor.DarkGray);
                    Console.WriteLine();
                    continue;
                }

                if (lower == "untrust")
                {
                    TrustedTools.Clear();
                    ColorLine("  [Trusted tools cleared]", ConsoleColor.DarkGray);
                    Console.WriteLine();
                    continue;
                }

                conversation.Add(new { type = "message", role = "user", content = trimmed });

                try
                {
                    string response = await RunAgentLoop(conversation);
                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write("Agent: ");
                    Console.ResetColor();
                    Console.WriteLine(response);
                    Console.WriteLine();
                }
                catch (Exception ex)
                {
                    ColorLine("  [Error] " + ex.Message, ConsoleColor.Red);
                    Console.WriteLine();
                }
            }
        }

        // ----------------------------------------------------------------
        // Agent loop
        // ----------------------------------------------------------------

        static async Task<string> RunAgentLoop(List<object> conversation)
        {
            var tools = BuildTools();

            for (int step = 0; step < MaxSteps; step++)
            {
                var body = new JObject
                {
                    ["model"] = AiConfig.ResolveModel(Model),
                    ["input"] = JArray.FromObject(conversation),
                    ["tools"] = JArray.FromObject(tools)
                };

                string responseJson = await PostRawAsync(body.ToString(Formatting.None));
                var parsed = JsonConvert.DeserializeObject<ResponsesResponse>(responseJson);

                if (parsed?.Error != null)
                    throw new InvalidOperationException(parsed.Error.Message);

                var toolCalls = ResponsesApiClient.GetToolCalls(parsed);

                if (toolCalls.Count == 0)
                {
                    string text = ResponsesApiClient.ExtractText(parsed);
                    // Append assistant message to conversation
                    foreach (var item in parsed.Output)
                    {
                        if (item.Type == "message")
                            conversation.Add(new { type = "message", role = "assistant", content = text });
                    }
                    return text;
                }

                // Append function_call items to conversation
                foreach (var item in parsed.Output)
                {
                    if (item.Type == "function_call")
                        conversation.Add(new
                        {
                            type      = "function_call",
                            call_id   = item.CallId,
                            name      = item.Name,
                            arguments = item.Arguments
                        });
                }

                // Execute tools (with confirmation for sensitive ones)
                foreach (var call in toolCalls)
                {
                    var args        = JObject.Parse(call.Arguments ?? "{}");
                    bool shouldRun  = await ShouldRunTool(call.Name, args);
                    object result;

                    if (shouldRun)
                    {
                        result = await ExecuteToolAsync(call.Name, args);
                    }
                    else
                    {
                        result = new
                        {
                            success  = false,
                            error    = "User rejected the action",
                            rejected = true
                        };
                    }

                    string resultJson = JsonConvert.SerializeObject(result);
                    string logPreview = resultJson.Length > 200
                        ? resultJson.Substring(0, 200) + "..."
                        : resultJson;

                    ColorLine(string.Format("  [tool] {0} → {1}", call.Name, logPreview),
                        ConsoleColor.DarkCyan);

                    conversation.Add(new
                    {
                        type    = "function_call_output",
                        call_id = call.CallId,
                        output  = resultJson
                    });
                }
            }

            throw new InvalidOperationException(
                string.Format("Agent loop did not finish within {0} steps.", MaxSteps));
        }

        // ----------------------------------------------------------------
        // Confirmation UI
        // ----------------------------------------------------------------

        static async Task<bool> ShouldRunTool(string toolName, JObject args)
        {
            if (!ConfirmationRequired.Contains(toolName))
                return true;

            if (TrustedTools.Contains(toolName))
            {
                ColorLine(string.Format("  ⚡ Auto-approved (trusted): {0}", toolName),
                    ConsoleColor.Blue);
                return true;
            }

            if (toolName == "send_email")
                PrintEmailConfirmation(args);
            else
                PrintGenericConfirmation(toolName, args);

            Console.Write("  Your choice (Y/N/T=trust): ");
            string answer = (Console.ReadLine() ?? string.Empty).Trim().ToLowerInvariant();
            Console.WriteLine();

            if (answer == "t" || answer == "trust")
            {
                TrustedTools.Add(toolName);
                ColorLine(string.Format("  ✓ Trusted \"{0}\" for this session", toolName),
                    ConsoleColor.Blue);
                return true;
            }

            if (answer == "y" || answer == "yes")
                return true;

            ColorLine("  ✗ Action cancelled", ConsoleColor.Red);
            return false;
        }

        static void PrintEmailConfirmation(JObject args)
        {
            string to      = FormatTo(args["to"]);
            string subject = args["subject"]?.ToString() ?? "(no subject)";
            string body    = args["body"]?.ToString()    ?? "(empty)";
            string format  = args["format"]?.ToString()  ?? "text";
            string replyTo = args["reply_to"]?.ToString();

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("  ┌──────────────────────────────────────────────────────────────┐");
            Console.WriteLine("  │  📧 EMAIL CONFIRMATION REQUIRED                              │");
            Console.WriteLine("  ├──────────────────────────────────────────────────────────────┤");
            Console.ResetColor();
            Console.WriteLine(string.Format("  │  To:      {0}", to.Length > 50 ? to.Substring(0, 50) : to));
            Console.WriteLine(string.Format("  │  Subject: {0}", subject.Length > 50 ? subject.Substring(0, 50) : subject));
            Console.WriteLine(string.Format("  │  Format:  {0}", format));
            if (!string.IsNullOrEmpty(replyTo))
                Console.WriteLine(string.Format("  │  Reply-To:{0}", replyTo));
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("  ├──────────────────────────────────────────────────────────────┤");
            Console.ResetColor();
            Console.WriteLine("  │  Body:");
            foreach (string line in body.Split('\n'))
                Console.WriteLine("  │    " + line);
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("  └──────────────────────────────────────────────────────────────┘");
            Console.ResetColor();
            Console.WriteLine();
        }

        static void PrintGenericConfirmation(string toolName, JObject args)
        {
            Console.WriteLine();
            ColorLine("  ⚠  Action requires confirmation", ConsoleColor.Yellow);
            Console.WriteLine("     Tool: " + toolName);
            Console.WriteLine("     Args: " + args.ToString(Formatting.Indented));
            Console.WriteLine();
        }

        static string FormatTo(JToken token)
        {
            if (token == null) return "(none)";
            if (token.Type == JTokenType.Array)
                return string.Join(", ", token.Select(t => t.ToString()));
            return token.ToString();
        }

        // ----------------------------------------------------------------
        // Tool definitions
        // ----------------------------------------------------------------

        static List<ToolDefinition> BuildTools()
        {
            return new List<ToolDefinition>
            {
                new ToolDefinition
                {
                    Type        = "function",
                    Name        = "list_files",
                    Description = "List files and directories inside workspace/ (or a sub-path)",
                    Parameters  = new
                    {
                        type       = "object",
                        properties = new
                        {
                            path = new { type = "string", description = "Relative path inside workspace (use '.' for root)" }
                        },
                        required             = new[] { "path" },
                        additionalProperties = false
                    },
                    Strict = true
                },
                new ToolDefinition
                {
                    Type        = "function",
                    Name        = "read_file",
                    Description = "Read the text content of a file inside workspace/",
                    Parameters  = new
                    {
                        type       = "object",
                        properties = new
                        {
                            path = new { type = "string", description = "Relative file path inside workspace/" }
                        },
                        required             = new[] { "path" },
                        additionalProperties = false
                    },
                    Strict = true
                },
                new ToolDefinition
                {
                    Type        = "function",
                    Name        = "write_file",
                    Description = "Create or overwrite a file inside workspace/ with given text content",
                    Parameters  = new
                    {
                        type       = "object",
                        properties = new
                        {
                            path    = new { type = "string", description = "Relative file path inside workspace/" },
                            content = new { type = "string", description = "Text content to write" }
                        },
                        required             = new[] { "path", "content" },
                        additionalProperties = false
                    },
                    Strict = true
                },
                new ToolDefinition
                {
                    Type        = "function",
                    Name        = "search_files",
                    Description = "Search for files in workspace/ whose names match a pattern (supports * wildcard)",
                    Parameters  = new
                    {
                        type       = "object",
                        properties = new
                        {
                            pattern = new { type = "string", description = "Filename pattern, e.g. '*.md' or 'report*'" }
                        },
                        required             = new[] { "pattern" },
                        additionalProperties = false
                    },
                    Strict = true
                },
                new ToolDefinition
                {
                    Type        = "function",
                    Name        = "send_email",
                    Description = "Send an email to one or more recipients. Recipients must be in workspace/whitelist.json.",
                    Parameters  = new
                    {
                        type       = "object",
                        properties = new
                        {
                            to      = new { type = "array", items = new { type = "string" }, description = "Recipient email address(es). Must be in the whitelist." },
                            subject = new { type = "string", description = "Email subject line." },
                            body    = new { type = "string", description = "Email content. Plain text or HTML." },
                            format  = new { type = "string", @enum = new[] { "text", "html" }, description = "Content format: 'text' or 'html'. Default: text" },
                            reply_to = new { type = "string", description = "Optional reply-to email address." }
                        },
                        required             = new[] { "to", "subject", "body" },
                        additionalProperties = false
                    },
                    Strict = false
                }
            };
        }

        // ----------------------------------------------------------------
        // Tool execution
        // ----------------------------------------------------------------

        static async Task<object> ExecuteToolAsync(string name, JObject args)
        {
            switch (name)
            {
                case "list_files":   return ExecuteListFiles(args);
                case "read_file":    return ExecuteReadFile(args);
                case "write_file":   return ExecuteWriteFile(args);
                case "search_files": return ExecuteSearchFiles(args);
                case "send_email":   return await ExecuteSendEmailAsync(args);
                default:
                    throw new InvalidOperationException("Unknown tool: " + name);
            }
        }

        static object ExecuteListFiles(JObject args)
        {
            string rel     = args["path"]?.ToString() ?? ".";
            string absPath = ResolveWorkspacePath(rel);
            if (absPath == null) return new { error = "Access denied: path outside workspace." };
            if (!Directory.Exists(absPath)) return new { error = "Directory not found: " + rel };

            var entries = new List<object>();
            foreach (string d in Directory.GetDirectories(absPath))
                entries.Add(new { type = "directory", name = Path.GetFileName(d) });
            foreach (string f in Directory.GetFiles(absPath))
                entries.Add(new { type = "file", name = Path.GetFileName(f) });

            return new { path = rel, entries };
        }

        static object ExecuteReadFile(JObject args)
        {
            string rel     = args["path"]?.ToString() ?? string.Empty;
            string absPath = ResolveWorkspacePath(rel);
            if (absPath == null) return new { error = "Access denied: path outside workspace." };
            if (!File.Exists(absPath)) return new { error = "File not found: " + rel };

            string content = File.ReadAllText(absPath, Encoding.UTF8);
            return new { path = rel, content };
        }

        static object ExecuteWriteFile(JObject args)
        {
            string rel     = args["path"]?.ToString() ?? string.Empty;
            string content = args["content"]?.ToString() ?? string.Empty;
            string absPath = ResolveWorkspacePath(rel);
            if (absPath == null) return new { error = "Access denied: path outside workspace." };

            string dir = Path.GetDirectoryName(absPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(absPath, content, Encoding.UTF8);
            return new { success = true, path = rel, bytesWritten = Encoding.UTF8.GetByteCount(content) };
        }

        static object ExecuteSearchFiles(JObject args)
        {
            string pattern = args["pattern"]?.ToString() ?? "*";
            var matches    = new List<string>();

            foreach (string f in Directory.GetFiles(_workspaceRoot, pattern, SearchOption.AllDirectories))
            {
                string rel = f.Substring(_workspaceRoot.Length).TrimStart(Path.DirectorySeparatorChar);
                matches.Add(rel);
            }

            return new { pattern, count = matches.Count, files = matches };
        }

        static async Task<object> ExecuteSendEmailAsync(JObject args)
        {
            // 1. Load whitelist
            var whitelist = LoadWhitelist();

            // 2. Validate recipients
            var toToken = args["to"];
            var recipients = new List<string>();

            if (toToken != null)
            {
                if (toToken.Type == JTokenType.Array)
                {
                    foreach (JToken t in toToken)
                        recipients.Add(t.ToString());
                }
                else
                {
                    recipients.Add(toToken.ToString());
                }
            }

            if (recipients.Count == 0)
                return new { success = false, error = "No recipients specified." };

            var blocked = recipients.Where(r => !IsEmailAllowed(r, whitelist)).ToList();
            if (blocked.Count > 0)
            {
                return new
                {
                    success = false,
                    error   = "Recipients not in whitelist: " + string.Join(", ", blocked) +
                              ". Update workspace/whitelist.json to allow them."
                };
            }

            // 3. Check Resend configuration
            if (string.IsNullOrWhiteSpace(ResendApiKey))
            {
                return new
                {
                    success = false,
                    error   = "RESEND_API_KEY is not configured. Set it in App.config."
                };
            }

            if (string.IsNullOrWhiteSpace(ResendFrom))
            {
                return new
                {
                    success = false,
                    error   = "RESEND_FROM is not configured. Set a verified sender address in App.config."
                };
            }

            // 4. Build email
            string subject = args["subject"]?.ToString() ?? "(no subject)";
            string body    = args["body"]?.ToString()    ?? string.Empty;
            string format  = args["format"]?.ToString()  ?? "text";
            string replyTo = args["reply_to"]?.ToString();

            bool   isHtml  = string.Equals(format, "html", StringComparison.OrdinalIgnoreCase);
            string htmlBody = isHtml ? body : TextToHtml(body);
            string textBody = isHtml ? StripHtml(body) : body;

            var payload = new JObject
            {
                ["from"]    = ResendFrom,
                ["to"]      = JArray.FromObject(recipients),
                ["subject"] = subject,
                ["html"]    = htmlBody,
                ["text"]    = textBody
            };

            if (!string.IsNullOrEmpty(replyTo))
                payload["reply_to"] = replyTo;

            // 5. Send via Resend API
            using (var http = new HttpClient())
            {
                http.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", ResendApiKey);

                using (var reqContent = new StringContent(
                    payload.ToString(Formatting.None), Encoding.UTF8, "application/json"))
                using (var resp = await http.PostAsync("https://api.resend.com/emails", reqContent))
                {
                    string respBody = await resp.Content.ReadAsStringAsync();

                    if (!resp.IsSuccessStatusCode)
                    {
                        return new
                        {
                            success = false,
                            error   = string.Format("Resend API error {0}: {1}",
                                (int)resp.StatusCode, respBody)
                        };
                    }

                    var result = JObject.Parse(respBody);
                    string id  = result["id"]?.ToString() ?? "unknown";

                    return new
                    {
                        success = true,
                        id,
                        to      = recipients,
                        subject
                    };
                }
            }
        }

        // ----------------------------------------------------------------
        // Whitelist helpers
        // ----------------------------------------------------------------

        static List<string> LoadWhitelist()
        {
            try
            {
                if (!File.Exists(_whitelistPath)) return new List<string>();
                string json = File.ReadAllText(_whitelistPath, Encoding.UTF8);
                var    obj  = JObject.Parse(json);
                var    arr  = obj["allowed_recipients"] as JArray;
                return arr == null
                    ? new List<string>()
                    : arr.Select(t => t.ToString()).ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        static bool IsEmailAllowed(string email, List<string> whitelist)
        {
            string normalized = email.ToLowerInvariant();
            string domain     = normalized.Contains("@")
                ? "@" + normalized.Split('@')[1]
                : string.Empty;

            foreach (string pattern in whitelist)
            {
                string p = pattern.ToLowerInvariant();
                if (p.StartsWith("@"))
                {
                    if (domain == p) return true;
                }
                else
                {
                    if (normalized == p) return true;
                }
            }

            return false;
        }

        // ----------------------------------------------------------------
        // Workspace path helpers
        // ----------------------------------------------------------------

        static string ResolveWorkspacePath(string relativePath)
        {
            string full = Path.GetFullPath(
                Path.Combine(_workspaceRoot, relativePath));

            return full.StartsWith(_workspaceRoot + Path.DirectorySeparatorChar,
                                   StringComparison.OrdinalIgnoreCase)
                   || string.Equals(full, _workspaceRoot, StringComparison.OrdinalIgnoreCase)
                ? full
                : null;
        }

        static void EnsureWorkspace()
        {
            Directory.CreateDirectory(_workspaceRoot);
            Directory.CreateDirectory(Path.Combine(_workspaceRoot, "docs"));
            Directory.CreateDirectory(Path.Combine(_workspaceRoot, "output"));
        }

        // ----------------------------------------------------------------
        // Text helpers
        // ----------------------------------------------------------------

        static string TextToHtml(string text)
        {
            string escaped = text
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\n", "<br>\n");
            return "<div style=\"font-family: sans-serif; line-height: 1.6; color: #333;\">"
                   + escaped + "</div>";
        }

        static string StripHtml(string html)
        {
            return System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", string.Empty);
        }

        // ----------------------------------------------------------------
        // HTTP helper
        // ----------------------------------------------------------------

        static async Task<string> PostRawAsync(string jsonBody)
        {
            using (var http = new HttpClient())
            {
                http.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", AiConfig.ApiKey);

                if (AiConfig.Provider == "openrouter")
                {
                    if (!string.IsNullOrWhiteSpace(AiConfig.HttpReferer))
                        http.DefaultRequestHeaders.TryAddWithoutValidation(
                            "HTTP-Referer", AiConfig.HttpReferer);
                    if (!string.IsNullOrWhiteSpace(AiConfig.AppName))
                        http.DefaultRequestHeaders.TryAddWithoutValidation(
                            "X-Title", AiConfig.AppName);
                }

                using (var content = new StringContent(jsonBody, Encoding.UTF8, "application/json"))
                using (var response = await http.PostAsync(AiConfig.ApiEndpoint, content))
                {
                    return await response.Content.ReadAsStringAsync();
                }
            }
        }

        // ----------------------------------------------------------------
        // Console helpers
        // ----------------------------------------------------------------

        static void ColorLine(string text, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(text);
            Console.ResetColor();
        }

        static void PrintExamples()
        {
            ColorLine("Example queries:", ConsoleColor.DarkGray);
            string[] examples =
            {
                "List all files in the workspace",
                "Read workspace/docs/sample.md and summarise it",
                "Write 'Hello from the agent!' to workspace/output/hello.txt",
                "Send an email to alice@aidevs.pl with subject 'Hello' and a short greeting",
                "Search for any markdown files in the workspace"
            };
            foreach (string ex in examples)
                ColorLine("  • " + ex, ConsoleColor.DarkGray);
            Console.WriteLine();
        }
    }
}
