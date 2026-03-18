using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FourthDevs.Common;
using FourthDevs.Common.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Lesson03_McpCore
{
    /// <summary>
    /// In-process simulation of an MCP server.
    ///
    /// Exposes the same tools, resources, and prompts as the JavaScript MCP server
    /// in 01_03_mcp_core/src/server.js:
    ///   Tools:     calculate, summarize_with_confirmation
    ///   Resources: config://project, data://stats
    ///   Prompts:   code-review
    ///
    /// The summarize_with_confirmation tool demonstrates two advanced MCP features:
    ///   • Elicitation — the server requests additional input from the user
    ///   • Sampling    — the server delegates an LLM call back through the client
    /// </summary>
    internal static class McpServer
    {
        // ----------------------------------------------------------------
        // Descriptor types (mirrors MCP SDK types)
        // ----------------------------------------------------------------

        public sealed class ToolDescriptor
        {
            public string Name        { get; set; }
            public string Description { get; set; }
        }

        public sealed class ResourceDescriptor
        {
            public string Uri         { get; set; }
            public string Description { get; set; }
        }

        public sealed class PromptDescriptor
        {
            public string Name        { get; set; }
            public string Description { get; set; }
        }

        public sealed class PromptMessage
        {
            public string Role    { get; set; }
            public string Content { get; set; }
        }

        // ----------------------------------------------------------------
        // Tools
        // ----------------------------------------------------------------

        public static List<ToolDescriptor> ListTools()
        {
            return new List<ToolDescriptor>
            {
                new ToolDescriptor
                {
                    Name        = "calculate",
                    Description = "Basic arithmetic (add, subtract, multiply, divide)"
                },
                new ToolDescriptor
                {
                    Name        = "summarize_with_confirmation",
                    Description = "Summarizes text after elicitation (user confirmation) and sampling (LLM completion)"
                }
            };
        }

        /// <summary>Calls a synchronous tool and returns its result object.</summary>
        public static object CallTool(string name, JObject args)
        {
            if (name == "calculate")
                return Calculate(args);

            throw new InvalidOperationException(
                string.Format("Unknown tool (or use CallToolAsync for async tools): {0}", name));
        }

        /// <summary>Calls a tool that may require async work (sampling / elicitation).</summary>
        public static async Task<object> CallToolAsync(string name, JObject args)
        {
            if (name == "summarize_with_confirmation")
                return await SummarizeWithConfirmation(args);

            return CallTool(name, args);
        }

        static object Calculate(JObject args)
        {
            string op = args["operation"]?.ToString() ?? string.Empty;
            double a  = args["a"]?.Value<double>()  ?? 0;
            double b  = args["b"]?.Value<double>()  ?? 0;

            switch (op)
            {
                case "add":      return new { result = a + b };
                case "subtract": return new { result = a - b };
                case "multiply": return new { result = a * b };
                case "divide":
                    if (b == 0) return new { error = "Division by zero" };
                    return new { result = a / b };
                default:
                    return new { error = string.Format("Unknown operation: {0}", op) };
            }
        }

        /// <summary>
        /// Demonstrates MCP elicitation + sampling:
        ///   1. Elicitation – asks the user (console) whether to proceed.
        ///   2. Sampling    – delegates the actual summarisation LLM call back through
        ///                    the client (here: a direct Responses API call).
        /// </summary>
        static async Task<object> SummarizeWithConfirmation(JObject args)
        {
            string text      = args["text"]?.ToString()          ?? string.Empty;
            int    maxLength = args["maxLength"]?.Value<int>()   ?? 100;

            // Elicitation: the server asks the client for user confirmation
            Console.Write(
                string.Format("\n[MCP elicitation] Summarize text (max {0} words)? (y/n): ", maxLength));
            string answer = Console.ReadLine() ?? string.Empty;

            if (!string.Equals(answer.Trim(), "y", StringComparison.OrdinalIgnoreCase))
                return new { cancelled = true, reason = "User declined" };

            // Sampling: the server delegates an LLM call to the client
            string summary = await LlmSummarize(text, maxLength);
            return new { summary };
        }

        static async Task<string> LlmSummarize(string text, int maxLength)
        {
            using (var client = new ResponsesApiClient())
            {
                var request = new ResponsesRequest
                {
                    Model = AiConfig.ResolveModel("gpt-4.1-mini"),
                    Input = new List<InputMessage>
                    {
                        new InputMessage
                        {
                            Role    = "user",
                            Content = string.Format(
                                "Summarize the following text in at most {0} words:\n\n{1}",
                                maxLength, text)
                        }
                    }
                };

                var response = await client.SendAsync(request);
                return ResponsesApiClient.ExtractText(response);
            }
        }

        // ----------------------------------------------------------------
        // Resources
        // ----------------------------------------------------------------

        public static List<ResourceDescriptor> ListResources()
        {
            return new List<ResourceDescriptor>
            {
                new ResourceDescriptor
                {
                    Uri         = "config://project",
                    Description = "Static project configuration"
                },
                new ResourceDescriptor
                {
                    Uri         = "data://stats",
                    Description = "Dynamic runtime statistics"
                }
            };
        }

        public static object ReadResource(string uri)
        {
            switch (uri)
            {
                case "config://project":
                    return new
                    {
                        name     = "4th-devs-dotnet48",
                        version  = "1.0.0",
                        env      = "development",
                        locale   = "en-US",
                        features = new { tools = true, resources = true, prompts = true }
                    };

                case "data://stats":
                    return new
                    {
                        requestsHandled = 42,
                        toolCallsToday  = 17,
                        avgResponseMs   = 234,
                        lastUpdated     = DateTime.UtcNow.ToString("O")
                    };

                default:
                    throw new InvalidOperationException(
                        string.Format("Resource not found: {0}", uri));
            }
        }

        // ----------------------------------------------------------------
        // Prompts
        // ----------------------------------------------------------------

        public static List<PromptDescriptor> ListPrompts()
        {
            return new List<PromptDescriptor>
            {
                new PromptDescriptor
                {
                    Name        = "code-review",
                    Description = "Code review template with args (code, language, focus)"
                }
            };
        }

        public static List<PromptMessage> GetPrompt(
            string name,
            Dictionary<string, string> args)
        {
            if (name != "code-review")
                throw new InvalidOperationException(string.Format("Prompt not found: {0}", name));

            string code     = GetArg(args, "code",     "");
            string language = GetArg(args, "language", "unknown");
            string focus    = GetArg(args, "focus",    "general");

            return new List<PromptMessage>
            {
                new PromptMessage
                {
                    Role    = "system",
                    Content = string.Format(
                        "You are an expert {0} code reviewer. Focus on {1}.", language, focus)
                },
                new PromptMessage
                {
                    Role    = "user",
                    Content = string.Format(
                        "Please review this {0} code:\n\n```{0}\n{1}\n```", language, code)
                }
            };
        }

        static string GetArg(Dictionary<string, string> args, string key, string defaultValue)
        {
            string val;
            return args.TryGetValue(key, out val) ? val : defaultValue;
        }
    }
}
