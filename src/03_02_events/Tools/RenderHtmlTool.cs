using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FourthDevs.Events.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Events.Tools
{
    public static class RenderHtmlTool
    {
        private const string ContentPlaceholder = "{{CONTENT}}";

        public static Tool Create()
        {
            var definition = new ToolDefinition
            {
                Type = "function",
                Name = "render_html",
                Description = "Convert a markdown file to a styled HTML document using the project template.",
                Parameters = JObject.FromObject(new
                {
                    type = "object",
                    properties = new
                    {
                        markdown_path = new { type = "string", description = "Workspace-relative path to the source markdown file." },
                        output_path = new { type = "string", description = "Workspace-relative path for the output HTML file." },
                        title = new { type = "string", description = "Optional HTML title override." }
                    },
                    required = new[] { "markdown_path", "output_path" }
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
            var mdPath = CommonToolHelpers.AsWorkspaceSafePath(args.Value<string>("markdown_path"));
            var outPath = CommonToolHelpers.AsWorkspaceSafePath(args.Value<string>("output_path"));

            if (mdPath == null || outPath == null)
            {
                return Task.FromResult(ToolResult.Text("Error: markdown_path and output_path must be valid workspace-relative paths."));
            }

            var templatePath = Path.Combine(CommonToolHelpers.WorkspaceRootDir, "template.html");
            if (!File.Exists(templatePath))
            {
                return Task.FromResult(ToolResult.Text("Error: template.html not found."));
            }

            var template = File.ReadAllText(templatePath);
            if (!template.Contains(ContentPlaceholder))
            {
                return Task.FromResult(ToolResult.Text($"Error: template.html does not contain placeholder \"{ContentPlaceholder}\"."));
            }

            var fullMdPath = Path.Combine(CommonToolHelpers.WorkspaceDir, mdPath);
            if (!File.Exists(fullMdPath))
            {
                return Task.FromResult(ToolResult.Text($"Error: could not read markdown file at \"{mdPath}\"."));
            }

            var markdown = File.ReadAllText(fullMdPath);
            var htmlContent = SimpleMarkdownToHtml(markdown);

            var titleArg = args.Value<string>("title") ?? "";
            var h1Match = Regex.Match(markdown, @"^#\s+(.+)$", RegexOptions.Multiline);
            var pageTitle = !string.IsNullOrWhiteSpace(titleArg) ? titleArg.Trim()
                : h1Match.Success ? h1Match.Groups[1].Value : "Document";

            var output = template.Replace(ContentPlaceholder, htmlContent);
            output = Regex.Replace(output, @"<title>[^<]*</title>", $"<title>{pageTitle}</title>");

            var absoluteOutPath = Path.Combine(CommonToolHelpers.WorkspaceDir, outPath);
            var outDir = Path.GetDirectoryName(absoluteOutPath);
            if (!Directory.Exists(outDir))
            {
                Directory.CreateDirectory(outDir);
            }
            File.WriteAllText(absoluteOutPath, output);

            var result = new JObject
            {
                ["success"] = true,
                ["markdown_path"] = mdPath,
                ["output_path"] = outPath,
                ["title"] = pageTitle,
                ["content_length"] = htmlContent.Length
            };

            return Task.FromResult(ToolResult.Text(result.ToString(Formatting.Indented)));
        }

        private static string SimpleMarkdownToHtml(string markdown)
        {
            var lines = markdown.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            var sb = new System.Text.StringBuilder();
            bool inList = false;

            foreach (var line in lines)
            {
                var trimmed = line.TrimStart();

                if (trimmed.StartsWith("### "))
                {
                    if (inList) { sb.AppendLine("</ul>"); inList = false; }
                    sb.AppendLine($"<h3>{trimmed.Substring(4)}</h3>");
                }
                else if (trimmed.StartsWith("## "))
                {
                    if (inList) { sb.AppendLine("</ul>"); inList = false; }
                    sb.AppendLine($"<h2>{trimmed.Substring(3)}</h2>");
                }
                else if (trimmed.StartsWith("# "))
                {
                    if (inList) { sb.AppendLine("</ul>"); inList = false; }
                    sb.AppendLine($"<h1>{trimmed.Substring(2)}</h1>");
                }
                else if (trimmed.StartsWith("- ") || trimmed.StartsWith("* "))
                {
                    if (!inList) { sb.AppendLine("<ul>"); inList = true; }
                    sb.AppendLine($"<li>{trimmed.Substring(2)}</li>");
                }
                else if (string.IsNullOrWhiteSpace(trimmed))
                {
                    if (inList) { sb.AppendLine("</ul>"); inList = false; }
                    sb.AppendLine();
                }
                else
                {
                    if (inList) { sb.AppendLine("</ul>"); inList = false; }
                    sb.AppendLine($"<p>{trimmed}</p>");
                }
            }

            if (inList) sb.AppendLine("</ul>");
            return sb.ToString();
        }
    }
}
