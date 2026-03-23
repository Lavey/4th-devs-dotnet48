using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FourthDevs.Common.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Evals.Agent
{
    /// <summary>
    /// Provides tool definitions (Responses API format) and execution logic.
    /// </summary>
    internal static class ToolExecutor
    {
        /// <summary>
        /// Tool definitions in the Responses API flat format.
        /// </summary>
        public static readonly List<ToolDefinition> ToolDefinitions = new List<ToolDefinition>
        {
            new ToolDefinition
            {
                Type = "function",
                Name = "get_current_time",
                Description = "Returns current UTC time in ISO 8601 format.",
                Parameters = new
                {
                    type = "object",
                    properties = new { },
                    additionalProperties = false
                },
                Strict = false
            },
            new ToolDefinition
            {
                Type = "function",
                Name = "sum_numbers",
                Description = "Returns the sum of an array of numbers.",
                Parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        numbers = new
                        {
                            type = "array",
                            items = new { type = "number" },
                            description = "The numbers to sum."
                        }
                    },
                    required = new[] { "numbers" },
                    additionalProperties = false
                },
                Strict = false
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
                    return Task.FromResult(
                        JsonConvert.SerializeObject(new { nowUtc = DateTime.UtcNow.ToString("o") }));

                case "sum_numbers":
                    return Task.FromResult(SumNumbers(rawArgs));

                default:
                    return Task.FromResult(
                        JsonConvert.SerializeObject(new { error = string.Format("Unknown tool: {0}", name) }));
            }
        }

        private static string SumNumbers(string rawArgs)
        {
            try
            {
                JObject args = JObject.Parse(rawArgs);
                JToken numsTok = args["numbers"];
                if (numsTok == null || numsTok.Type != JTokenType.Array)
                    return JsonConvert.SerializeObject(new { error = "'numbers' must be an array" });

                var numbers = new List<double>();
                foreach (JToken n in numsTok)
                {
                    double val = n.Value<double>();
                    if (!double.IsNaN(val) && !double.IsInfinity(val))
                        numbers.Add(val);
                }

                if (numbers.Count == 0)
                    return JsonConvert.SerializeObject(
                        new { error = "numbers must contain at least one numeric value" });

                double sum = 0;
                foreach (double d in numbers)
                    sum += d;

                return JsonConvert.SerializeObject(new { count = numbers.Count, sum });
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new { error = ex.Message });
            }
        }
    }
}
