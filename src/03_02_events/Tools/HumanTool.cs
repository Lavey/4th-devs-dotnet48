using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FourthDevs.Events.Models;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Events.Tools
{
    public static class HumanTool
    {
        public static Tool Create()
        {
            var definition = new ToolDefinition
            {
                Type = "function",
                Name = "request_human",
                Description = "Pause and request a human decision before continuing.",
                Parameters = JObject.FromObject(new
                {
                    type = "object",
                    properties = new
                    {
                        question = new { type = "string", description = "Clear and specific question for the human, include options if useful." },
                        wait_id = new { type = "string", description = "Optional explicit wait id. If omitted it is generated automatically." }
                    },
                    required = new[] { "question" }
                })
            };

            return new Tool
            {
                Definition = definition,
                Handler = HandleAsync
            };
        }

        private static Task<ToolResult> HandleAsync(JObject args, ToolRuntimeContext ctx)
        {
            var question = args.Value<string>("question");
            if (string.IsNullOrWhiteSpace(question))
            {
                return Task.FromResult(ToolResult.Text("Error: question is required"));
            }

            var waitId = args.Value<string>("wait_id");
            if (string.IsNullOrWhiteSpace(waitId))
            {
                waitId = "wait-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            }

            return Task.FromResult(ToolResult.HumanRequest(waitId, question.Trim()));
        }
    }
}
