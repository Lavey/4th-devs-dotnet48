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
    /// Lesson 03 – MCP Core
    /// Demonstrates all core MCP capabilities in-process:
    ///   • Tools     — discover and call tools (calculate, summarize_with_confirmation)
    ///   • Resources — discover and read read-only data (config://project, data://stats)
    ///   • Prompts   — discover and render reusable message templates (code-review)
    ///
    /// In the original JS version (01_03_mcp_core) the client spawns a real MCP server
    /// as a subprocess over stdio transport. Because the official C# MCP SDK targets
    /// .NET 6+, this .NET 4.8 port implements an equivalent in-process McpServer class
    /// that exposes the same tools, resources, and prompts via the same conceptual API.
    ///
    /// Source: 01_03_mcp_core/app.js (i-am-alice/4th-devs)
    /// </summary>
    internal static class Program
    {
        static void Main(string[] args)
        {
            MainAsync().GetAwaiter().GetResult();
        }

        static async Task MainAsync()
        {
            // ----------------------------------------------------------------
            // TOOLS – Actions the server exposes for the LLM to invoke
            // ----------------------------------------------------------------
            Heading("TOOLS", "Actions the server exposes for the LLM to invoke");

            var tools = McpServer.ListTools();
            Log("listTools", string.Join("\n  ",
                tools.ConvertAll(t => string.Format("{0} — {1}", t.Name, t.Description))));

            var calcResult = McpServer.CallTool(
                "calculate",
                JObject.Parse("{\"operation\":\"multiply\",\"a\":42,\"b\":17}"));
            Log("callTool(calculate)", calcResult);

            const string textToSummarize =
                "The Model Context Protocol (MCP) is a standardized protocol that allows " +
                "applications to provide context for LLMs. It separates the concerns of " +
                "providing context from the actual LLM interaction. MCP servers expose " +
                "tools, resources, and prompts that clients can discover and use.";

            var summaryResult = await McpServer.CallToolAsync(
                "summarize_with_confirmation",
                new JObject
                {
                    ["text"]      = textToSummarize,
                    ["maxLength"] = 30
                });
            Log("callTool(summarize_with_confirmation)", summaryResult);

            // ----------------------------------------------------------------
            // RESOURCES – Read-only data the server makes available to clients
            // ----------------------------------------------------------------
            Heading("RESOURCES", "Read-only data the server makes available to clients");

            var resources = McpServer.ListResources();
            Log("listResources", string.Join("\n  ",
                resources.ConvertAll(r => string.Format("{0} — {1}", r.Uri, r.Description))));

            var configResource = McpServer.ReadResource("config://project");
            Log("readResource(config://project)", configResource);

            var statsResource = McpServer.ReadResource("data://stats");
            Log("readResource(data://stats)", statsResource);

            // ----------------------------------------------------------------
            // PROMPTS – Reusable message templates with parameters
            // ----------------------------------------------------------------
            Heading("PROMPTS", "Reusable message templates with parameters");

            var prompts = McpServer.ListPrompts();
            Log("listPrompts", string.Join("\n  ",
                prompts.ConvertAll(p => string.Format("{0} — {1}", p.Name, p.Description))));

            var messages = McpServer.GetPrompt("code-review", new Dictionary<string, string>
            {
                { "code",     "function add(a, b) { return a + b; }" },
                { "language", "javascript" },
                { "focus",    "readability" }
            });
            Log("getPrompt(code-review)", string.Join("\n  ",
                messages.ConvertAll(m => string.Format("[{0}] {1}", m.Role, m.Content))));
        }

        // ----------------------------------------------------------------
        // Display helpers
        // ----------------------------------------------------------------

        static void Heading(string title, string subtitle = null)
        {
            Console.WriteLine();
            Console.WriteLine(subtitle != null
                ? string.Format("=== {0} — {1} ===", title, subtitle)
                : string.Format("=== {0} ===", title));
        }

        static void Log(string label, object value)
        {
            Console.WriteLine();
            Console.WriteLine(string.Format("[{0}]", label));
            string text = value is string s
                ? s
                : JsonConvert.SerializeObject(value, Formatting.Indented);
            Console.WriteLine(text);
        }
    }
}
