using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using FourthDevs.Common;
using FourthDevs.Common.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Lesson04_Reports
{
    /// <summary>
    /// Lesson 04 – Reports
    /// Agentic document analysis and report generation.
    ///
    /// The agent uses filesystem tools (read, list, write) to:
    ///   1. Discover documents in workspace/docs/
    ///   2. Read each document's content
    ///   3. Generate a structured Markdown report
    ///   4. Save the report to workspace/output/
    ///
    /// Source: 01_04_reports/app.js (i-am-alice/4th-devs)
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
            string exeDir  = AppDomain.CurrentDomain.BaseDirectory;
            string docsDir = Path.Combine(exeDir, "workspace", "docs");
            string outDir  = Path.Combine(exeDir, "workspace", "output");

            Directory.CreateDirectory(docsDir);
            Directory.CreateDirectory(outDir);

            Console.WriteLine("=== Reports Agent ===");
            Console.WriteLine("Analyse documents and generate structured reports\n");

            // Seed sample documents if none exist
            EnsureSampleDocs(docsDir);

            string userPrompt =
                "List all documents in workspace/docs/, read their contents, " +
                "and write a comprehensive structured Markdown report to " +
                "workspace/output/report.md. The report must include: " +
                "an executive summary, key findings per document, " +
                "cross-document themes, and recommendations.";

            Console.WriteLine("Task: " + userPrompt + "\n");

            var conversation = new List<object>
            {
                new { type = "message", role = "user", content = userPrompt }
            };

            await RunAgentLoop(conversation, docsDir, outDir);
        }

        // ----------------------------------------------------------------
        // Tool definitions
        // ----------------------------------------------------------------

        static List<ToolDefinition> BuildTools(string docsDir, string outDir)
        {
            return new List<ToolDefinition>
            {
                new ToolDefinition
                {
                    Type        = "function",
                    Name        = "list_documents",
                    Description = "List all documents available for analysis in workspace/docs/",
                    Parameters  = new
                    {
                        type       = "object",
                        properties = new { },
                        required   = new string[0], additionalProperties = false
                    },
                    Strict = true
                },
                new ToolDefinition
                {
                    Type        = "function",
                    Name        = "read_document",
                    Description = "Read the text content of a document from workspace/docs/",
                    Parameters  = new
                    {
                        type       = "object",
                        properties = new
                        {
                            filename = new { type = "string", description = "Document filename (basename only)" }
                        },
                        required = new[] { "filename" }, additionalProperties = false
                    },
                    Strict = true
                },
                new ToolDefinition
                {
                    Type        = "function",
                    Name        = "write_report",
                    Description = "Write the final Markdown report to workspace/output/",
                    Parameters  = new
                    {
                        type       = "object",
                        properties = new
                        {
                            filename = new { type = "string", description = "Report filename (basename), e.g. report.md" },
                            content  = new { type = "string", description = "Full Markdown content of the report" }
                        },
                        required = new[] { "filename", "content" }, additionalProperties = false
                    },
                    Strict = true
                }
            };
        }

        // ----------------------------------------------------------------
        // Tool execution
        // ----------------------------------------------------------------

        static object ExecuteTool(string name, JObject args, string docsDir, string outDir)
        {
            switch (name)
            {
                case "list_documents":
                {
                    var files = new List<object>();
                    foreach (string f in Directory.GetFiles(docsDir))
                    {
                        var fi = new FileInfo(f);
                        files.Add(new
                        {
                            filename  = fi.Name,
                            extension = fi.Extension,
                            sizeBytes = fi.Length
                        });
                    }
                    return new { documents = files, count = files.Count };
                }

                case "read_document":
                {
                    string filename = args["filename"]?.ToString() ?? string.Empty;
                    // Sanitise: strip any directory component
                    filename = Path.GetFileName(filename);
                    string fullPath = Path.Combine(docsDir, filename);

                    if (!File.Exists(fullPath))
                        return new { error = "File not found: " + filename };

                    string content = File.ReadAllText(fullPath, Encoding.UTF8);
                    return new { filename, content };
                }

                case "write_report":
                {
                    string filename = Path.GetFileName(args["filename"]?.ToString() ?? "report.md");
                    string content  = args["content"]?.ToString() ?? string.Empty;
                    string fullPath = Path.Combine(outDir, filename);

                    File.WriteAllText(fullPath, content, Encoding.UTF8);
                    Console.WriteLine("\nReport saved: workspace/output/" + filename);
                    return new { success = true, path = "workspace/output/" + filename };
                }

                default:
                    throw new InvalidOperationException("Unknown tool: " + name);
            }
        }

        // ----------------------------------------------------------------
        // Agent loop
        // ----------------------------------------------------------------

        static async Task RunAgentLoop(List<object> inputItems, string docsDir, string outDir)
        {
            var tools = BuildTools(docsDir, outDir);

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
                    string finalText = ResponsesApiClient.ExtractText(parsed);
                    if (!string.IsNullOrWhiteSpace(finalText))
                        Console.WriteLine("\n" + finalText);
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
                    var toolResult = ExecuteTool(call.Name, toolArgs, docsDir, outDir);
                    string resultJson = JsonConvert.SerializeObject(toolResult);

                    // Truncate large results in the console log
                    string logResult = resultJson.Length > 200
                        ? resultJson.Substring(0, 200) + "..."
                        : resultJson;
                    Console.WriteLine(string.Format(
                        "  [tool] {0}({1}) → {2}", call.Name, call.Arguments, logResult));

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
        // Sample documents
        // ----------------------------------------------------------------

        static void EnsureSampleDocs(string docsDir)
        {
            string[] files = Directory.GetFiles(docsDir);
            if (files.Length > 0) return;

            File.WriteAllText(Path.Combine(docsDir, "q1_sales.md"),
                "# Q1 Sales Report\n\n" +
                "## Summary\nTotal revenue: $1,240,000 (+12% YoY)\n\n" +
                "## Top Products\n- Product A: $420,000\n- Product B: $310,000\n- Product C: $200,000\n\n" +
                "## Challenges\nSupply chain delays impacted Product B delivery by 2 weeks.\n",
                Encoding.UTF8);

            File.WriteAllText(Path.Combine(docsDir, "customer_feedback.md"),
                "# Customer Feedback Analysis — Q1\n\n" +
                "## Sentiment Overview\n- Positive: 68%\n- Neutral: 22%\n- Negative: 10%\n\n" +
                "## Top Themes\n1. Delivery speed (positive mentions +40%)\n" +
                "2. Product quality (stable)\n3. Customer support response time (needs improvement)\n\n" +
                "## Notable Issues\nRecurring complaints about long wait times for email support.\n",
                Encoding.UTF8);

            File.WriteAllText(Path.Combine(docsDir, "tech_roadmap.md"),
                "# Technology Roadmap 2026\n\n" +
                "## H1 Goals\n- Deploy new inventory management system\n" +
                "- Migrate reporting pipeline to cloud\n" +
                "- Launch customer portal v2.0\n\n" +
                "## H2 Goals\n- AI-powered demand forecasting\n- Mobile app redesign\n\n" +
                "## Risks\nKey personnel dependency on legacy system knowledge.\n",
                Encoding.UTF8);

            Console.WriteLine("Created 3 sample documents in workspace/docs/\n");
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
