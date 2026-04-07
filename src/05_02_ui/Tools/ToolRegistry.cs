using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using FourthDevs.ChatUi.Models;
using Newtonsoft.Json.Linq;

namespace FourthDevs.ChatUi.Tools
{
    /// <summary>
    /// Registry of all available tools. Provides JSON Schema definitions
    /// for the Responses API and executes tools by name.
    /// </summary>
    internal sealed class ToolRegistry
    {
        private readonly string _dataDir;
        private readonly Dictionary<string, Func<JObject, ToolResult>> _handlers;
        private readonly JArray _definitions;

        public ToolRegistry(string dataDir)
        {
            _dataDir = dataDir;

            _handlers = new Dictionary<string, Func<JObject, ToolResult>>
            {
                ["get_sales_report"] = SalesTool.GetSalesReport,
                ["render_chart"] = args => SalesTool.RenderChart(args, dataDir),
                ["lookup_contact_context"] = EmailTool.LookupContactContext,
                ["send_email"] = EmailTool.SendEmail,
                ["create_artifact"] = args => ArtifactTool.CreateArtifact(args, dataDir),
                ["search_notes"] = NotesTool.SearchNotes
            };

            _definitions = new JArray
            {
                SalesTool.GetSalesReportDef(),
                SalesTool.RenderChartDef(),
                EmailTool.LookupContactContextDef(),
                EmailTool.SendEmailDef(),
                ArtifactTool.CreateArtifactDef(),
                NotesTool.SearchNotesDef()
            };
        }

        public JArray GetDefinitionsArray()
        {
            return _definitions;
        }

        public ToolResult Execute(string name, JObject args)
        {
            Func<JObject, ToolResult> handler;
            if (_handlers.TryGetValue(name, out handler))
            {
                try
                {
                    return handler(args);
                }
                catch (Exception ex)
                {
                    return new ToolResult
                    {
                        Ok = false,
                        Output = new JObject { ["error"] = ex.Message }
                    };
                }
            }

            return new ToolResult
            {
                Ok = false,
                Output = new JObject { ["error"] = "Unknown tool: " + name }
            };
        }
    }

    internal sealed class ToolResult
    {
        public bool Ok;
        public JObject Output;
        public ArtifactEvent Artifact;
    }
}
