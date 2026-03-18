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

namespace FourthDevs.Lesson03_UploadMcp
{
    /// <summary>
    /// Lesson 03 – MCP Upload Agent
    /// Scans workspace/ for files, "uploads" each one (via a configurable HTTP
    /// endpoint), and maintains uploaded.md as a tracking ledger.
    ///
    /// The original JS version (01_03_upload_mcp) connects to two MCP servers
    /// simultaneously — a local stdio files-mcp for reading and a remote
    /// UploadThing HTTP-MCP server for uploading. This .NET 4.8 port uses:
    ///   • native filesystem tools (no MCP required) for reading
    ///   • an agentic tool-call loop that decides what to upload
    ///   • the UPLOAD_ENDPOINT setting (App.config) for the actual upload
    ///
    /// Without a real UploadThing key the agent operates in "dry-run" mode and
    /// logs what it would upload without sending network requests.
    ///
    /// Source: 01_03_upload_mcp/app.js (i-am-alice/4th-devs)
    /// </summary>
    internal static class Program
    {
        private const string Model    = "gpt-4.1-mini";
        private const int    MaxSteps = 20;

        static void Main(string[] args)
        {
            MainAsync().GetAwaiter().GetResult();
        }

        static async Task MainAsync()
        {
            string exeDir      = AppDomain.CurrentDomain.BaseDirectory;
            string workspaceDir = Path.Combine(exeDir, "workspace");
            Directory.CreateDirectory(workspaceDir);

            // Seed the workspace with sample files if it is empty
            EnsureSampleFiles(workspaceDir);

            Console.WriteLine("=== MCP Upload Agent ===");
            Console.WriteLine("Upload workspace files — track results in uploaded.md\n");
            Console.WriteLine("Workspace: " + workspaceDir);
            Console.WriteLine();

            // Determine if we have an upload endpoint configured
            string uploadEndpoint = System.Configuration.ConfigurationManager
                .AppSettings["UPLOAD_ENDPOINT"]?.Trim() ?? string.Empty;
            bool dryRun = string.IsNullOrWhiteSpace(uploadEndpoint);
            if (dryRun)
                Console.WriteLine("Note: UPLOAD_ENDPOINT not set — running in dry-run mode.\n");

            // Run the agentic upload loop
            var conversation = new List<object>
            {
                new
                {
                    type = "message",
                    role = "user",
                    content = "Check the workspace for files, upload any that have not been " +
                              "uploaded yet, and update uploaded.md with the results (filename, URL, timestamp)."
                }
            };

            await RunAgentLoop(conversation, workspaceDir, uploadEndpoint, dryRun);
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
                    Name        = "fs_list",
                    Description = "List files in the workspace directory (or a subdirectory)",
                    Parameters  = new
                    {
                        type       = "object",
                        properties = new
                        {
                            path = new { type = "string", description = "Relative path inside workspace (use '.' for root)" }
                        },
                        required = new[] { "path" }, additionalProperties = false
                    },
                    Strict = true
                },
                new ToolDefinition
                {
                    Type        = "function",
                    Name        = "fs_read",
                    Description = "Read text content of a file in the workspace",
                    Parameters  = new
                    {
                        type       = "object",
                        properties = new
                        {
                            path = new { type = "string", description = "Relative file path inside workspace" }
                        },
                        required = new[] { "path" }, additionalProperties = false
                    },
                    Strict = true
                },
                new ToolDefinition
                {
                    Type        = "function",
                    Name        = "fs_write",
                    Description = "Write or overwrite a text file in the workspace",
                    Parameters  = new
                    {
                        type       = "object",
                        properties = new
                        {
                            path    = new { type = "string", description = "Relative file path inside workspace" },
                            content = new { type = "string", description = "Text content to write" }
                        },
                        required = new[] { "path", "content" }, additionalProperties = false
                    },
                    Strict = true
                },
                new ToolDefinition
                {
                    Type        = "function",
                    Name        = "upload_file",
                    Description = "Upload a file from the workspace to the remote storage service",
                    Parameters  = new
                    {
                        type       = "object",
                        properties = new
                        {
                            path = new { type = "string", description = "Relative file path inside workspace" },
                            name = new { type = "string", description = "Display name for the uploaded file" }
                        },
                        required = new[] { "path", "name" }, additionalProperties = false
                    },
                    Strict = true
                }
            };
        }

        // ----------------------------------------------------------------
        // Tool execution
        // ----------------------------------------------------------------

        static object ExecuteTool(
            string name, JObject args,
            string workspaceDir, string uploadEndpoint, bool dryRun)
        {
            string rel     = args["path"]?.ToString() ?? ".";
            string absPath = ResolvePath(workspaceDir, rel);

            switch (name)
            {
                case "fs_list":
                {
                    if (absPath == null)          return new { error = "Access denied" };
                    if (!Directory.Exists(absPath)) return new { error = "Directory not found: " + rel };

                    var entries = new List<object>();
                    foreach (string d in Directory.GetDirectories(absPath))
                        entries.Add(new { type = "directory", name = Path.GetFileName(d) });
                    foreach (string f in Directory.GetFiles(absPath))
                        entries.Add(new { type = "file", name = Path.GetFileName(f),
                                          size = new FileInfo(f).Length });
                    return new { path = rel, entries };
                }

                case "fs_read":
                {
                    if (absPath == null)       return new { error = "Access denied" };
                    if (!File.Exists(absPath)) return new { error = "File not found: " + rel };
                    return new { path = rel, content = File.ReadAllText(absPath, Encoding.UTF8) };
                }

                case "fs_write":
                {
                    if (absPath == null) return new { error = "Access denied" };
                    string content = args["content"]?.ToString() ?? string.Empty;
                    Directory.CreateDirectory(Path.GetDirectoryName(absPath));
                    File.WriteAllText(absPath, content, Encoding.UTF8);
                    return new { success = true, path = rel };
                }

                case "upload_file":
                {
                    if (absPath == null)       return new { error = "Access denied" };
                    if (!File.Exists(absPath)) return new { error = "File not found: " + rel };

                    string fileName = args["name"]?.ToString() ?? Path.GetFileName(rel);

                    if (dryRun)
                    {
                        string mockUrl = string.Format("https://utfs.io/f/dry-run/{0}", Uri.EscapeDataString(fileName));
                        Console.WriteLine(string.Format("  [dry-run] would upload: {0} → {1}", rel, mockUrl));
                        return new { success = true, url = mockUrl, dryRun = true };
                    }

                    // Real upload (synchronous wrapper for async upload)
                    return UploadFileAsync(absPath, fileName, uploadEndpoint).GetAwaiter().GetResult();
                }

                default:
                    throw new InvalidOperationException("Unknown tool: " + name);
            }
        }

        static async Task<object> UploadFileAsync(string filePath, string fileName, string endpoint)
        {
            try
            {
                using (var http = new HttpClient())
                using (var form = new MultipartFormDataContent())
                {
                    byte[] bytes = File.ReadAllBytes(filePath);
                    var fileContent = new ByteArrayContent(bytes);
                    fileContent.Headers.ContentType =
                        new MediaTypeHeaderValue(GuessMimeType(filePath));
                    form.Add(fileContent, "file", fileName);

                    using (var response = await http.PostAsync(endpoint, form))
                    {
                        string body = await response.Content.ReadAsStringAsync();
                        if (!response.IsSuccessStatusCode)
                            return new { error = string.Format("Upload failed ({0}): {1}", (int)response.StatusCode, body) };

                        var json = JObject.Parse(body);
                        string url = json["url"]?.ToString()
                                  ?? json["fileUrl"]?.ToString()
                                  ?? json["data"]?[0]?["url"]?.ToString()
                                  ?? "(no url in response)";
                        return new { success = true, url };
                    }
                }
            }
            catch (Exception ex)
            {
                return new { error = ex.Message };
            }
        }

        // ----------------------------------------------------------------
        // Agent loop
        // ----------------------------------------------------------------

        static async Task RunAgentLoop(
            List<object> inputItems,
            string workspaceDir, string uploadEndpoint, bool dryRun)
        {
            var tools = BuildTools();

            for (int step = 0; step < MaxSteps; step++)
            {
                var body = new JObject
                {
                    ["model"] = AiConfig.ResolveModel(Model),
                    ["input"] = JArray.FromObject(inputItems),
                    ["tools"] = JArray.FromObject(tools)
                };

                string responseJson = await PostRawAsync(body.ToString(Formatting.None));
                var parsed = JsonConvert.DeserializeObject<ResponsesResponse>(responseJson);

                if (parsed?.Error != null)
                    throw new InvalidOperationException(parsed.Error.Message);

                var toolCalls = ResponsesApiClient.GetToolCalls(parsed);

                if (toolCalls.Count == 0)
                {
                    Console.WriteLine("\n" + ResponsesApiClient.ExtractText(parsed));
                    return;
                }

                foreach (var item in parsed.Output)
                {
                    if (item.Type == "function_call")
                        inputItems.Add(new
                        {
                            type      = "function_call",
                            call_id   = item.CallId,
                            name      = item.Name,
                            arguments = item.Arguments
                        });
                }

                foreach (var call in toolCalls)
                {
                    var toolArgs   = JObject.Parse(call.Arguments ?? "{}");
                    var toolResult = ExecuteTool(call.Name, toolArgs, workspaceDir, uploadEndpoint, dryRun);
                    string resultJson = JsonConvert.SerializeObject(toolResult);
                    Console.WriteLine(string.Format("  [tool] {0}({1}) → {2}", call.Name, call.Arguments, resultJson));

                    inputItems.Add(new
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
        // Helpers
        // ----------------------------------------------------------------

        static string ResolvePath(string workspaceDir, string relative)
        {
            string full = Path.GetFullPath(Path.Combine(workspaceDir, relative));
            return full.StartsWith(workspaceDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                   || string.Equals(full, workspaceDir, StringComparison.OrdinalIgnoreCase)
                ? full
                : null;
        }

        static string GuessMimeType(string filePath)
        {
            switch (Path.GetExtension(filePath).ToLowerInvariant())
            {
                case ".txt":  return "text/plain";
                case ".md":   return "text/markdown";
                case ".json": return "application/json";
                case ".png":  return "image/png";
                case ".jpg":
                case ".jpeg": return "image/jpeg";
                case ".pdf":  return "application/pdf";
                default:      return "application/octet-stream";
            }
        }

        static void EnsureSampleFiles(string workspaceDir)
        {
            string note1 = Path.Combine(workspaceDir, "note1.txt");
            string note2 = Path.Combine(workspaceDir, "note2.md");
            string ledger = Path.Combine(workspaceDir, "uploaded.md");

            if (!File.Exists(note1))
                File.WriteAllText(note1, "Hello from note1.txt — created by MCP Upload demo.", Encoding.UTF8);
            if (!File.Exists(note2))
                File.WriteAllText(note2, "# Note 2\n\nCreated by the MCP Upload demo.", Encoding.UTF8);
            if (!File.Exists(ledger))
                File.WriteAllText(ledger, "# Uploaded Files\n\n| Filename | URL | Timestamp |\n|----------|-----|-----------|\n", Encoding.UTF8);
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
