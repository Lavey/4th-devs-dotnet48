using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Observability.Agent
{
    /// <summary>
    /// Provides tool definitions (Chat Completions format) and execution logic.
    /// </summary>
    internal static class ToolExecutor
    {
        /// <summary>
        /// Tool definitions in the Chat Completions "tools" array format.
        /// </summary>
        public static readonly List<object> ToolDefinitions = new List<object>
        {
            new Dictionary<string, object>
            {
                { "type", "function" },
                { "function", new Dictionary<string, object>
                    {
                        { "name", "get_current_time" },
                        { "description", "Returns current UTC time in ISO 8601 format." },
                        { "parameters", new Dictionary<string, object>
                            {
                                { "type", "object" },
                                { "properties", new Dictionary<string, object>() },
                                { "additionalProperties", false }
                            }
                        }
                    }
                }
            },
            new Dictionary<string, object>
            {
                { "type", "function" },
                { "function", new Dictionary<string, object>
                    {
                        { "name", "sum_numbers" },
                        { "description", "Returns the sum of an array of numbers." },
                        { "parameters", new Dictionary<string, object>
                            {
                                { "type", "object" },
                                { "properties", new Dictionary<string, object>
                                    {
                                        { "numbers", new Dictionary<string, object>
                                            {
                                                { "type", "array" },
                                                { "items", new Dictionary<string, object> { { "type", "number" } } },
                                                { "description", "The numbers to sum." }
                                            }
                                        }
                                    }
                                },
                                { "required", new[] { "numbers" } },
                                { "additionalProperties", false }
                            }
                        }
                    }
                }
            }
        };

        /// <summary>
        /// Executes the named tool with the raw JSON arguments string.
        /// </summary>
        public static Task<string> ExecuteAsync(string name, string rawArgs)
        {
            switch (name)
            {
                case "get_current_time":
                    return Task.FromResult(DateTime.UtcNow.ToString("o"));

                case "sum_numbers":
                    return Task.FromResult(SumNumbers(rawArgs));

                default:
                    return Task.FromResult(string.Format("Unknown tool: {0}", name));
            }
        }

        private static string SumNumbers(string rawArgs)
        {
            try
            {
                JObject args = JObject.Parse(rawArgs);
                JToken numsTok = args["numbers"];
                if (numsTok == null || numsTok.Type != JTokenType.Array)
                    return "Error: 'numbers' must be an array.";

                double sum = 0;
                foreach (JToken n in numsTok)
                {
                    sum += n.Value<double>();
                }
                return sum.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            catch (Exception ex)
            {
                return "Error: " + ex.Message;
            }
        }
    }
}
