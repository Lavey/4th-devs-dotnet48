using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FourthDevs.Common;
using FourthDevs.Common.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Lesson03_McpNative
{
    /// <summary>
    /// Lesson 03 – MCP Native
    /// One agent that uses both "MCP-style" server tools and plain native tools
    /// through a single unified tool-call dispatch loop.
    ///
    /// MCP tools  (would come from an MCP server): get_weather, get_time
    /// Native tools (plain C# functions):          calculate, uppercase
    ///
    /// The model sees all four tools identically — it does not know which are
    /// "MCP" and which are "native".
    ///
    /// Source: 01_03_mcp_native/app.js (i-am-alice/4th-devs)
    /// </summary>
    internal static class Program
    {
        private const string Model    = "gpt-4.1-mini";
        private const int    MaxSteps = 10;

        private const string MCP_LABEL    = "[mcp]";
        private const string NATIVE_LABEL = "[native]";

        static void Main(string[] args)
        {
            MainAsync().GetAwaiter().GetResult();
        }

        static async Task MainAsync()
        {
            Console.WriteLine("MCP Native — unified MCP + native tool agent\n");
            Console.WriteLine("MCP tools:    " + string.Join(", ", McpTools.Names));
            Console.WriteLine("Native tools: " + string.Join(", ", NativeTools.Names));
            Console.WriteLine();

            var queries = new[]
            {
                "What's the weather in Tokyo?",
                "What time is it in Europe/London?",
                "Calculate 42 multiplied by 17",
                "Convert 'hello world' to uppercase",
                "What's 25 + 17, and what's the weather in Paris?"
            };

            foreach (string query in queries)
            {
                Console.WriteLine("Q: " + query);
                string answer = await RunQuery(query);
                Console.WriteLine("A: " + answer);
                Console.WriteLine();
            }
        }

        // ----------------------------------------------------------------
        // Unified tool definitions (MCP + native, indistinguishable to the model)
        // ----------------------------------------------------------------

        static readonly List<ToolDefinition> AllTools = new List<ToolDefinition>
        {
            // --- MCP tools ---
            new ToolDefinition
            {
                Type        = "function",
                Name        = "get_weather",
                Description = "Get current weather for a given city (MCP server tool)",
                Parameters  = new
                {
                    type       = "object",
                    properties = new { city = new { type = "string", description = "City name" } },
                    required   = new[] { "city" }, additionalProperties = false
                },
                Strict = true
            },
            new ToolDefinition
            {
                Type        = "function",
                Name        = "get_time",
                Description = "Get current time in a given timezone (MCP server tool)",
                Parameters  = new
                {
                    type       = "object",
                    properties = new { timezone = new { type = "string", description = "IANA timezone name, e.g. Europe/London" } },
                    required   = new[] { "timezone" }, additionalProperties = false
                },
                Strict = true
            },
            // --- Native tools ---
            new ToolDefinition
            {
                Type        = "function",
                Name        = "calculate",
                Description = "Perform basic arithmetic (add, subtract, multiply, divide)",
                Parameters  = new
                {
                    type       = "object",
                    properties = new
                    {
                        operation = new { type = "string", @enum = new[] { "add", "subtract", "multiply", "divide" } },
                        a         = new { type = "number" },
                        b         = new { type = "number" }
                    },
                    required   = new[] { "operation", "a", "b" },
                    additionalProperties = false
                },
                Strict = true
            },
            new ToolDefinition
            {
                Type        = "function",
                Name        = "uppercase",
                Description = "Convert a text string to uppercase",
                Parameters  = new
                {
                    type       = "object",
                    properties = new { text = new { type = "string" } },
                    required   = new[] { "text" }, additionalProperties = false
                },
                Strict = true
            }
        };

        // ----------------------------------------------------------------
        // Tool dispatch — routes to the correct handler and logs the source
        // ----------------------------------------------------------------

        static object ExecuteTool(string name, JObject args)
        {
            // MCP tools (would be dispatched to an MCP server in the full version)
            if (McpTools.Handles(name))
            {
                Console.WriteLine(string.Format("  {0} {1}({2})", MCP_LABEL, name, args));
                return McpTools.Execute(name, args);
            }

            // Native tools (executed directly in this process)
            if (NativeTools.Handles(name))
            {
                Console.WriteLine(string.Format("  {0} {1}({2})", NATIVE_LABEL, name, args));
                return NativeTools.Execute(name, args);
            }

            throw new InvalidOperationException(string.Format("Unknown tool: {0}", name));
        }

        // ----------------------------------------------------------------
        // Agent loop
        // ----------------------------------------------------------------

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
                    ["tools"] = JArray.FromObject(AllTools)
                };

                string responseJson = await PostRawAsync(body.ToString(Formatting.None));
                var parsed = JsonConvert.DeserializeObject<ResponsesResponse>(responseJson);

                if (parsed?.Error != null)
                    throw new InvalidOperationException(parsed.Error.Message);

                var toolCalls = ResponsesApiClient.GetToolCalls(parsed);

                if (toolCalls.Count == 0)
                    return ResponsesApiClient.ExtractText(parsed);

                foreach (var item in parsed.Output)
                {
                    if (item.Type == "function_call")
                    {
                        inputItems.Add(new
                        {
                            type      = "function_call",
                            call_id   = item.CallId,
                            name      = item.Name,
                            arguments = item.Arguments
                        });
                    }
                }

                foreach (var call in toolCalls)
                {
                    var toolArgs   = JObject.Parse(call.Arguments ?? "{}");
                    var toolResult = ExecuteTool(call.Name, toolArgs);

                    inputItems.Add(new
                    {
                        type    = "function_call_output",
                        call_id = call.CallId,
                        output  = JsonConvert.SerializeObject(toolResult)
                    });
                }
            }

            throw new InvalidOperationException(
                string.Format("Tool calling did not finish within {0} steps.", MaxSteps));
        }

        static async Task<string> PostRawAsync(string jsonBody)
        {
            using (var http = new System.Net.Http.HttpClient())
            {
                http.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AiConfig.ApiKey);

                if (AiConfig.Provider == "openrouter")
                {
                    if (!string.IsNullOrWhiteSpace(AiConfig.HttpReferer))
                        http.DefaultRequestHeaders.TryAddWithoutValidation("HTTP-Referer", AiConfig.HttpReferer);
                    if (!string.IsNullOrWhiteSpace(AiConfig.AppName))
                        http.DefaultRequestHeaders.TryAddWithoutValidation("X-Title", AiConfig.AppName);
                }

                using (var content = new System.Net.Http.StringContent(
                    jsonBody, System.Text.Encoding.UTF8, "application/json"))
                using (var response = await http.PostAsync(AiConfig.ApiEndpoint, content))
                {
                    return await response.Content.ReadAsStringAsync();
                }
            }
        }
    }
}
