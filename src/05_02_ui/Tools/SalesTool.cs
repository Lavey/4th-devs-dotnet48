using FourthDevs.ChatUi.Data;
using Newtonsoft.Json.Linq;

namespace FourthDevs.ChatUi.Tools
{
    internal static class SalesTool
    {
        public static JObject GetSalesReportDef()
        {
            return new JObject
            {
                ["type"] = "function",
                ["name"] = "get_sales_report",
                ["description"] = "Retrieve sales data and revenue figures for a given quarter/year.",
                ["parameters"] = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["quarter"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "Quarter to retrieve (Q1, Q2, Q3, Q4, or 'all')"
                        },
                        ["year"] = new JObject
                        {
                            ["type"] = "integer",
                            ["description"] = "Year for the report"
                        }
                    },
                    ["required"] = new JArray("quarter", "year")
                }
            };
        }

        public static JObject RenderChartDef()
        {
            return new JObject
            {
                ["type"] = "function",
                ["name"] = "render_chart",
                ["description"] = "Generate a chart visualization from sales data.",
                ["parameters"] = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["type"] = new JObject
                        {
                            ["type"] = "string",
                            ["enum"] = new JArray("bar", "pie", "line"),
                            ["description"] = "Type of chart to render"
                        },
                        ["title"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "Chart title"
                        }
                    },
                    ["required"] = new JArray("type", "title")
                }
            };
        }

        public static ToolResult GetSalesReport(JObject args)
        {
            string quarter = args["quarter"]?.ToString() ?? "all";
            return new ToolResult
            {
                Ok = true,
                Output = new JObject
                {
                    ["summary"] = string.Format("{0} 2025 Revenue: $431K across 5 product lines", quarter.ToUpperInvariant()),
                    ["rows"] = MockData.ProductRows
                }
            };
        }

        public static ToolResult RenderChart(JObject args, string dataDir)
        {
            string chartType = args["type"]?.ToString() ?? "bar";
            string title = args["title"]?.ToString() ?? "Chart";
            string preview = MockData.GetChartPreview(chartType);

            string chartId = "chart_" + System.Guid.NewGuid().ToString("N").Substring(0, 8);

            // Persist chart preview to .data/
            ToolHelpers.PersistFile(dataDir, "charts/" + chartId + ".txt", preview);

            return new ToolResult
            {
                Ok = true,
                Output = new JObject
                {
                    ["chartId"] = chartId,
                    ["title"] = title,
                    ["preview"] = preview
                }
            };
        }
    }
}
