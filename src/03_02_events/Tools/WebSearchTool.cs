using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FourthDevs.Events.Models;
using FourthDevs.Common;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Events.Tools
{
    public static class WebSearchTool
    {
        public static Tool Create()
        {
            var definition = new ToolDefinition
            {
                Type = "function",
                Name = "web_search",
                Description = "Search the web and return concise findings with sources.",
                Parameters = JObject.FromObject(new
                {
                    type = "object",
                    properties = new
                    {
                        query = new { type = "string", description = "Search query" }
                    },
                    required = new[] { "query" }
                })
            };

            return new Tool
            {
                Definition = definition,
                Handler = HandleAsync
            };
        }

        private static async Task<ToolResult> HandleAsync(JObject args, ToolRuntimeContext ctx)
        {
            var query = args.Value<string>("query");
            if (string.IsNullOrWhiteSpace(query))
            {
                return ToolResult.Text("Error: query is required");
            }

            try
            {
                var model = AiConfig.ResolveModel(
                    System.Configuration.ConfigurationManager.AppSettings["WEB_SEARCH_MODEL"]
                    ?? System.Configuration.ConfigurationManager.AppSettings["OPENAI_MODEL"]
                    ?? "gpt-4.1");

                var client = new ResponsesApiClient();
                var inputMsgs = new List<FourthDevs.Common.Models.InputMessage>
                {
                    new FourthDevs.Common.Models.InputMessage
                    {
                        Role = "user",
                        Content = "Search the web for: " + query + "\nReturn concise findings with source URLs in markdown."
                    }
                };

                var request = new FourthDevs.Common.Models.ResponsesRequest
                {
                    Model = model,
                    Input = inputMsgs
                };

                var response = await client.SendAsync(request).ConfigureAwait(false);
                var text = ResponsesApiClient.ExtractText(response);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return ToolResult.Text(text);
                }
                return ToolResult.Text("Search completed but returned no text output.");
            }
            catch (Exception ex)
            {
                return ToolResult.Text("Error: web search failed (" + ex.Message + ")");
            }
        }
    }
}
