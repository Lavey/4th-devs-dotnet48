using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FourthDevs.Common;
using FourthDevs.Render.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Render.Core
{
    internal static class SpecGenerator
    {
        private const int MaxElements = 120;

        public static async Task<RenderDocument> GenerateAsync(string prompt, string[] packs)
        {
            if (packs == null || packs.Length == 0)
                packs = RenderCatalog.GetDefaultPackIds();

            packs = RenderCatalog.ResolvePackIds(packs);
            string[] allowedComponents = RenderCatalog.GetComponentsForPacks(packs);
            string catalog = RenderCatalog.GetCatalogManifestForPrompt(packs);
            string packList = string.Join(", ", packs);

            string instructions = string.Format(
@"You generate static, data-first dashboard specs constrained to allowed components.
Return JSON only, no markdown, using this shape:
{{""title"":""string"",""summary"":""string or null"",""spec"":{{""root"":""string"",""elements"":{{""id"":{{""type"":""Component"",""props"":{{}},""children"":[]}}}}}},""state"":{{}}}}

Strict rules:
- use ONLY allowed components from selected packs: {0}
- do NOT output HTML, CSS, JavaScript, markdown, or code fences
- do NOT use interactive fields like on, repeat, visible
- keep UI as a static snapshot focused on data communication
- place realistic synthetic data in state
- for chart/table data props, bind to state via {{""$state"":""/path""}}
- keep elements concise and under {1} elements

{2}",
                string.Join(", ", allowedComponents),
                MaxElements,
                catalog);

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

            JObject payload = ParsePayload(rawText);

            string title   = payload["title"]?.ToString() ?? "Dashboard";
            string summary = payload["summary"]?.Type == JTokenType.Null
                ? null
                : payload["summary"]?.ToString();

            // Parse spec
            JObject specObj = payload["spec"] as JObject;
            if (specObj == null)
                throw new InvalidOperationException("Spec generator returned no 'spec' object.");

            RenderSpec spec = ParseSpec(specObj, allowedComponents);

            // Parse state – preserve as raw object so JObject/JArray tokens remain navigable
            Dictionary<string, object> state = ParseState(payload["state"]);

            // Render HTML
            string html = SpecToHtml.RenderToHtml(title, spec, state);

            return new RenderDocument
            {
                Id        = Guid.NewGuid().ToString("N"),
                Title     = title,
                Prompt    = prompt,
                Summary   = summary,
                Spec      = spec,
                State     = state,
                Html      = html,
                Model     = AiConfig.ResolveModel("gpt-4.1"),
                Packs     = new List<string>(packs),
                CreatedAt = DateTime.UtcNow.ToString("o"),
            };
        }

        private static RenderSpec ParseSpec(JObject specObj, string[] allowedComponents)
        {
            string root = specObj["root"]?.ToString() ?? string.Empty;
            var elementsObj = specObj["elements"] as JObject;

            var elements = new Dictionary<string, RenderSpecElement>();

            if (elementsObj != null)
            {
                if (elementsObj.Count > MaxElements)
                    throw new InvalidOperationException(
                        string.Format("Spec has {0} elements, exceeding max of {1}.", elementsObj.Count, MaxElements));

                var allowed = new HashSet<string>(allowedComponents, StringComparer.Ordinal);

                foreach (var prop in elementsObj.Properties())
                {
                    var elObj = prop.Value as JObject;
                    if (elObj == null) continue;

                    string type = elObj["type"]?.ToString() ?? string.Empty;
                    if (!allowed.Contains(type))
                        throw new InvalidOperationException(
                            string.Format("Component '{0}' is not allowed in the selected packs.", type));

                    var el = new RenderSpecElement
                    {
                        Type     = type,
                        Props    = ParseProps(elObj["props"]),
                        Children = ParseChildren(elObj["children"]),
                    };
                    elements[prop.Name] = el;
                }
            }

            return new RenderSpec { Root = root, Elements = elements };
        }

        private static Dictionary<string, object> ParseProps(JToken propsToken)
        {
            var result = new Dictionary<string, object>();
            var jObj = propsToken as JObject;
            if (jObj == null) return result;

            // Keep raw JToken values so ResolveDynamic can process $state refs and arrays
            foreach (var prop in jObj.Properties())
                result[prop.Name] = prop.Value;

            return result;
        }

        private static List<string> ParseChildren(JToken childrenToken)
        {
            var result = new List<string>();
            var jArr = childrenToken as JArray;
            if (jArr == null) return result;

            foreach (JToken item in jArr)
                result.Add(item.ToString());

            return result;
        }

        /// <summary>
        /// Preserves JObject/JArray tokens in the state so that JSON Pointer resolution works.
        /// </summary>
        private static Dictionary<string, object> ParseState(JToken stateToken)
        {
            var result = new Dictionary<string, object>();
            var jObj = stateToken as JObject;
            if (jObj == null) return result;

            foreach (var prop in jObj.Properties())
                result[prop.Name] = prop.Value; // keep raw JToken

            return result;
        }

        private static JObject ParsePayload(string rawText)
        {
            if (string.IsNullOrWhiteSpace(rawText))
                throw new InvalidOperationException("Spec generator returned empty response.");

            // Strip markdown code fences if present
            string text = rawText.Trim();
            var fenceMatch = Regex.Match(text, @"```(?:json)?\s*([\s\S]*?)```", RegexOptions.IgnoreCase);
            if (fenceMatch.Success)
                text = fenceMatch.Groups[1].Value.Trim();

            // Find first '{' and last '}'
            int start = text.IndexOf('{');
            int end   = text.LastIndexOf('}');
            if (start >= 0 && end > start)
                text = text.Substring(start, end - start + 1);

            try
            {
                return JObject.Parse(text);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "Could not parse spec generator JSON: " + ex.Message + "\n\nRaw:\n" + rawText);
            }
        }
    }
}
