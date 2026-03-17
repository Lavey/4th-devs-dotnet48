using Aype.AI.Common.Models;

namespace Aype.AI.Tools.Tools
{
    /// <summary>
    /// Definition for the calculator tool.
    /// Evaluates a mathematical expression and returns the numeric result.
    /// </summary>
    public static class CalculatorTool
    {
        public static ToolDefinition Build()
        {
            return new ToolDefinition
            {
                Type        = "function",
                Name        = "calculator",
                Description = "Evaluate a mathematical expression and return the numeric result.",
                Parameters  = new
                {
                    type       = "object",
                    properties = new
                    {
                        expression = new
                        {
                            type        = "string",
                            description = "Math expression, e.g. '42 * 17' or 'sqrt(144)'"
                        }
                    },
                    required             = new[] { "expression" },
                    additionalProperties = false
                },
                Strict = true
            };
        }
    }
}
