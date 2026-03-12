using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FourthDevs.Common;
using FourthDevs.Common.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Lesson02_Tools
{
    /// <summary>
    /// Lesson 02 – Tools (Minimal function-calling demo)
    /// Demonstrates the tool-calling workflow with two simple tools:
    ///   • get_weather  – returns hardcoded weather data for a city
    ///   • send_email   – mocks sending an email and returns a confirmation
    ///
    /// The model decides whether to call a tool and with what arguments.
    /// We execute the tool locally, then feed the result back to the model.
    ///
    /// Source: 01_02_tools/app.js (i-am-alice/4th-devs)
    /// </summary>
    internal static class Program
    {
        private const string Model       = "gpt-4.1-mini";
        private const int    MaxSteps    = 5;

        static void Main(string[] args)
        {
            MainAsync().GetAwaiter().GetResult();
        }

        static async Task MainAsync()
        {
            const string query =
                "Check the current weather in Kraków. " +
                "Then send a short email with the answer to student@example.com.";

            Console.WriteLine($"Q: {query}");
            Console.WriteLine();

            string answer = await Chat(query);

            Console.WriteLine($"A: {answer}");
        }

        // =====================================================================
        // Step 1 – Tool definitions (sent to the model, never executed by it)
        // =====================================================================

        static readonly List<ToolDefinition> Tools = new List<ToolDefinition>
        {
            new ToolDefinition
            {
                Type        = "function",
                Name        = "get_weather",
                Description = "Get current weather for a given location",
                Parameters  = new
                {
                    type       = "object",
                    properties = new
                    {
                        location = new { type = "string", description = "City name" }
                    },
                    required             = new[] { "location" },
                    additionalProperties = false
                },
                Strict = true
            },
            new ToolDefinition
            {
                Type        = "function",
                Name        = "send_email",
                Description = "Send a short email message to a recipient",
                Parameters  = new
                {
                    type       = "object",
                    properties = new
                    {
                        to      = new { type = "string", description = "Recipient email address" },
                        subject = new { type = "string", description = "Email subject" },
                        body    = new { type = "string", description = "Plain-text email body" }
                    },
                    required             = new[] { "to", "subject", "body" },
                    additionalProperties = false
                },
                Strict = true
            }
        };

        // =====================================================================
        // Step 2 – Tool implementations (never called by the model)
        // =====================================================================

        static object ExecuteTool(string name, JObject args)
        {
            switch (name)
            {
                case "get_weather":
                {
                    string city = args["location"]?.ToString() ?? string.Empty;
                    var weatherData = new Dictionary<string, object>
                    {
                        { "Kraków", new { temp = -2, conditions = "snow" } },
                        { "London", new { temp =  8, conditions = "rain" } },
                        { "Tokyo",  new { temp = 15, conditions = "cloudy" } }
                    };

                    return weatherData.TryGetValue(city, out var weather)
                        ? weather
                        : new { temp = (int?)null, conditions = "unknown" };
                }

                case "send_email":
                {
                    string to      = RequireText(args, "to");
                    string subject = RequireText(args, "subject");
                    string body    = RequireText(args, "body");

                    return new { success = true, status = "sent", to, subject, body };
                }

                default:
                    throw new InvalidOperationException($"Unknown tool: {name}");
            }
        }

        static string RequireText(JObject obj, string field)
        {
            string val = obj[field]?.ToString()?.Trim();
            if (string.IsNullOrWhiteSpace(val))
                throw new ArgumentException($"\"{field}\" must be a non-empty string.");
            return val;
        }

        // =====================================================================
        // Step 3 – Tool-calling loop
        // =====================================================================

        static async Task<string> Chat(string userQuery)
        {
            using (var client = new ResponsesApiClient())
            {
                // Start with the user message
                var inputItems = new List<object>
                {
                    new { type = "message", role = "user", content = userQuery }
                };

                for (int step = 0; step < MaxSteps; step++)
                {
                    var request = new ResponsesRequest
                    {
                        Model = AiConfig.ResolveModel(Model),
                        Tools = Tools,
                        // We serialize inputItems ourselves because the type is mixed
                        // (messages and tool results). We pass them as a raw JSON token.
                        Input = null  // handled below via SerializeRequest
                    };

                    string responseText = await SendWithRawInput(client, request, inputItems);
                    if (responseText != null) return responseText;

                    // responseText == null means we hit tool calls; inputItems was updated in place
                }

                throw new InvalidOperationException($"Tool calling did not finish within {MaxSteps} steps.");
            }
        }

        /// <summary>
        /// Sends a Responses API request with a raw input list (mixed message / tool_result items).
        /// Returns the final answer text, or null if tool calls were found and processed.
        /// </summary>
        static async Task<string> SendWithRawInput(
            ResponsesApiClient client,
            ResponsesRequest baseRequest,
            List<object> inputItems)
        {
            // Build the JSON body manually so we can embed the raw inputItems list
            var body = new JObject
            {
                ["model"] = baseRequest.Model,
                ["input"] = JArray.FromObject(inputItems),
                ["tools"] = JArray.FromObject(baseRequest.Tools)
            };

            string json = body.ToString(Formatting.None);

            // Reuse the internal HTTP plumbing via reflection isn't ideal – instead,
            // call our own helper that accepts raw JSON.
            string responseBody = await PostRawAsync(json);
            var parsed = JsonConvert.DeserializeObject<ResponsesResponse>(responseBody);

            if (parsed?.Error != null)
                throw new InvalidOperationException(parsed.Error.Message);

            var toolCalls = ResponsesApiClient.GetToolCalls(parsed);

            if (toolCalls.Count == 0)
            {
                // No tools requested – return the final text
                return ResponsesApiClient.ExtractText(parsed);
            }

            // Append the assistant output items to the conversation
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
                else if (item.Type == "message")
                {
                    inputItems.Add(new
                    {
                        type    = "message",
                        role    = item.Role,
                        content = item.Content
                    });
                }
            }

            // Execute each tool call and append the results
            foreach (var call in toolCalls)
            {
                var args   = JObject.Parse(call.Arguments ?? "{}");
                var result = ExecuteTool(call.Name, args);

                Console.WriteLine($"  [tool] {call.Name}({call.Arguments}) → {JsonConvert.SerializeObject(result)}");

                inputItems.Add(new
                {
                    type    = "function_call_output",
                    call_id = call.CallId,
                    output  = JsonConvert.SerializeObject(result)
                });
            }

            // Signal that we need another round-trip
            return null;
        }

        // Simple raw POST helper to avoid coupling to ResponsesApiClient internals
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
