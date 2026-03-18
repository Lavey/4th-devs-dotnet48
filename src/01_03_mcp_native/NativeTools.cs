using System;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Lesson03_McpNative
{
    /// <summary>
    /// Native tools — plain C# functions, no MCP server required.
    /// Mirrors the tools in 01_03_mcp_native/src/native/tools.js.
    /// </summary>
    internal static class NativeTools
    {
        public static readonly string[] Names = { "calculate", "uppercase" };

        public static bool Handles(string toolName)
        {
            return Array.IndexOf(Names, toolName) >= 0;
        }

        public static object Execute(string name, JObject args)
        {
            switch (name)
            {
                case "calculate": return Calculate(args);
                case "uppercase": return Uppercase(args);
                default:
                    throw new InvalidOperationException(
                        string.Format("Unknown native tool: {0}", name));
            }
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

        static object Uppercase(JObject args)
        {
            string text = args["text"]?.ToString() ?? string.Empty;
            return new { result = text.ToUpperInvariant() };
        }
    }
}
