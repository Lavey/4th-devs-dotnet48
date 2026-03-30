using System;
using System.Collections.Generic;
using System.Text;

namespace FourthDevs.Render.Core
{
    /// <summary>
    /// Defines component packs and their components used for prompt building and validation.
    /// </summary>
    internal static class RenderCatalog
    {
        // Pack → component list
        private static readonly Dictionary<string, string[]> PackComponents =
            new Dictionary<string, string[]>
            {
                ["analytics-core"]     = new[] { "Stack", "Grid", "Card", "Heading", "Text", "Badge", "Separator" },
                ["analytics-viz"]      = new[] { "Metric", "LineChart", "BarChart" },
                ["analytics-table"]    = new[] { "Table" },
                ["analytics-insight"]  = new[] { "Alert", "Callout", "Accordion" },
                ["analytics-controls"] = new[] { "Input", "Select", "RadioGroup", "Switch", "Button" },
            };

        private static readonly string[] DefaultPacks = new[]
        {
            "analytics-core",
            "analytics-viz",
            "analytics-table",
        };

        // Component descriptions for prompt building
        private static readonly Dictionary<string, string> ComponentDescriptions =
            new Dictionary<string, string>
            {
                ["Stack"]      = "Flexible layout container. Props: direction (\"vertical\"|\"horizontal\"), gap (\"sm\"|\"md\"|\"lg\"), align, justify",
                ["Grid"]       = "Grid layout. Props: columns (1..4), gap (\"sm\"|\"md\"|\"lg\")",
                ["Card"]       = "Panel container. Props: title?, description?",
                ["Heading"]    = "Section heading. Props: text, level? (\"h1\"..\"h4\")",
                ["Text"]       = "Body text. Props: content, muted?",
                ["Badge"]      = "Status pill. Props: text, variant? (\"default\"|\"success\"|\"warning\"|\"danger\")",
                ["Separator"]  = "Visual divider. No props.",
                ["Metric"]     = "KPI value with trend. Props: label, value, detail?, trend? (\"up\"|\"down\"|\"neutral\")",
                ["LineChart"]  = "Time-series chart. Props: title?, data, xKey, yKey, height?",
                ["BarChart"]   = "Category chart. Props: title?, data, xKey, yKey, height?",
                ["Table"]      = "Tabular data. Props: columns (array of {key, label}), data, emptyMessage?",
                ["Alert"]      = "High-priority alert. Props: title, message?, tone? (\"info\"|\"success\"|\"warning\"|\"danger\")",
                ["Callout"]    = "Narrative insight block. Props: title?, content, type? (\"info\"|\"tip\"|\"warning\"|\"important\")",
                ["Accordion"]  = "Collapsible insights. Props: items (array of {title, content})",
                ["Input"]      = "Text input. Props: label?, value?, placeholder?",
                ["Select"]     = "Dropdown. Props: label?, value?, options (array of {value, label})",
                ["RadioGroup"] = "Choice group. Props: label?, value?, options (array of {value, label})",
                ["Switch"]     = "Boolean toggle. Props: label?, checked?",
                ["Button"]     = "Action button. Props: label, variant? (\"default\"|\"secondary\"|\"danger\")",
            };

        public static string[] GetAllPackIds()
        {
            return new[]
            {
                "analytics-core",
                "analytics-viz",
                "analytics-table",
                "analytics-insight",
                "analytics-controls",
            };
        }

        public static string[] GetDefaultPackIds()
        {
            return (string[])DefaultPacks.Clone();
        }

        public static string[] ResolvePackIds(IEnumerable<string> requested)
        {
            var valid = new List<string>();
            foreach (string id in requested)
            {
                if (PackComponents.ContainsKey(id) && !valid.Contains(id))
                    valid.Add(id);
            }
            // Always include analytics-core
            if (!valid.Contains("analytics-core"))
                valid.Insert(0, "analytics-core");
            return valid.ToArray();
        }

        public static string[] GetComponentsForPacks(IEnumerable<string> packIds)
        {
            var components = new List<string>();
            foreach (string packId in packIds)
            {
                string[] comps;
                if (PackComponents.TryGetValue(packId, out comps))
                {
                    foreach (string c in comps)
                    {
                        if (!components.Contains(c))
                            components.Add(c);
                    }
                }
            }
            return components.ToArray();
        }

        public static string GetCatalogManifestForPrompt(IEnumerable<string> packIds)
        {
            var sb = new StringBuilder();
            sb.AppendLine("## Component Catalog");
            sb.AppendLine();

            foreach (string packId in packIds)
            {
                string[] comps;
                if (!PackComponents.TryGetValue(packId, out comps))
                    continue;

                sb.AppendLine(string.Format("### Pack: {0}", packId));
                foreach (string comp in comps)
                {
                    string desc;
                    ComponentDescriptions.TryGetValue(comp, out desc);
                    sb.AppendLine(string.Format("- **{0}**: {1}", comp, desc ?? string.Empty));
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}
