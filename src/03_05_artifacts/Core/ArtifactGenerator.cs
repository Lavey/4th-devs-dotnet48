using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FourthDevs.Artifacts.Models;
using FourthDevs.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Artifacts.Core
{
    internal static class ArtifactGenerator
    {
        private static readonly string HtmlTemplate =
@"<!doctype html>
<html lang=""en"">
  <head>
    <meta charset=""UTF-8"" />
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
    <title>{TITLE}</title>
{PACK_SCRIPTS}
    <style>body {{ margin: 0; font-family: Inter, ui-sans-serif, system-ui, sans-serif; }}</style>
  </head>
  <body>
{BODY}
  </body>
</html>";

        public static async Task<ArtifactDocument> GenerateAsync(
            string prompt,
            string[] packs,
            string serverBaseUrl)
        {
            if (packs == null || packs.Length == 0)
                packs = new[] { "core" };

            string manifest = ArtifactCapabilities.GetCapabilityManifestForPrompt();
            string packList = string.Join(", ", packs);

            string instructions = string.Format(
@"You generate interactive browser artifacts.
Return JSON only, no markdown, with this shape:
{{""title"":""string"",""html"":""string""}}
Rules for html:
- must be self-contained, no external scripts, no external styles, no network calls
- include semantic markup and clear UI states
- keep JavaScript inline and small
- body must render immediately
- prefer preloaded globals from selected packs when appropriate ({0})
- if tailwind pack is selected, use utility classes

{1}", packList, manifest);

            var body = new JObject
            {
                ["model"] = AiConfig.ResolveModel("gpt-4.1"),
                ["instructions"] = instructions,
                ["input"] = new JArray
                {
                    new JObject
                    {
                        ["type"] = "message",
                        ["role"] = "user",
                        ["content"] = prompt
                    }
                },
                ["reasoning"] = new JObject { ["effort"] = "high" }
            };

            JObject response = await ApiClient.PostAsync(body);
            string rawText = ApiClient.ExtractText(response);
            string responseId = response["id"]?.ToString();

            JObject payload = ParseArtifactPayload(rawText);

            string title = payload["title"]?.ToString() ?? "Artifact";
            string htmlBody = payload["html"]?.ToString() ?? string.Empty;

            string packScripts = ArtifactCapabilities.GetPackScriptTags(packs);

            string fullHtml = HtmlTemplate
                .Replace("{TITLE}", title)
                .Replace("{PACK_SCRIPTS}", packScripts)
                .Replace("{BODY}", htmlBody);

            return new ArtifactDocument
            {
                Id = Guid.NewGuid().ToString("N"),
                Title = title,
                Prompt = prompt,
                Html = fullHtml,
                Model = AiConfig.ResolveModel("gpt-4.1"),
                Packs = new List<string>(packs),
                CreatedAt = DateTime.UtcNow.ToString("o")
            };
        }

        private static JObject ParseArtifactPayload(string rawText)
        {
            if (string.IsNullOrWhiteSpace(rawText))
                throw new InvalidOperationException("LLM returned empty response.");

            // Try raw JSON first
            try { return JObject.Parse(rawText.Trim()); }
            catch { }

            // Try extracting from ```json ... ``` or ``` ... ```
            var fenceMatch = Regex.Match(rawText, @"```(?:json)?\s*([\s\S]*?)```");
            if (fenceMatch.Success)
            {
                try { return JObject.Parse(fenceMatch.Groups[1].Value.Trim()); }
                catch { }
            }

            // Try extracting first {...} block
            int start = rawText.IndexOf('{');
            int end = rawText.LastIndexOf('}');
            if (start >= 0 && end > start)
            {
                try { return JObject.Parse(rawText.Substring(start, end - start + 1)); }
                catch { }
            }

            throw new InvalidOperationException(
                "Could not parse artifact JSON from response: " +
                (rawText.Length > 200 ? rawText.Substring(0, 200) + "..." : rawText));
        }
    }
}
