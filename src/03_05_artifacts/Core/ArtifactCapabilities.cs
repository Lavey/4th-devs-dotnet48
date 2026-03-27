using System;
using System.Collections.Generic;
using System.Text;

namespace FourthDevs.Artifacts.Core
{
    /// <summary>
    /// Defines capability packs (JS libraries) that can be injected into artifacts via CDN.
    /// </summary>
    internal static class ArtifactCapabilities
    {
        private const string CoreBootstrap = @"<script>
(function(){
  if (window.ArtifactKit) return;
  var createStore = function(initial) {
    var value = initial;
    var listeners = [];
    return {
      get: function() { return value; },
      set: function(next) {
        value = typeof next === 'function' ? next(value) : next;
        listeners.forEach(function(l) { l(value); });
      },
      subscribe: function(listener) {
        listeners.push(listener);
        return function() { listeners = listeners.filter(function(l) { return l !== listener; }); };
      }
    };
  };
  var q = function(selector, root) { return (root || document).querySelector(selector); };
  var qa = function(selector, root) { return Array.from((root || document).querySelectorAll(selector)); };
  window.ArtifactKit = { version: '0.1.0', createStore: createStore, q: q, qa: qa };
})();
</script>";

        private const string PreactBootstrap = @"<script>(function(){ if (window.preact && window.htm && !window.html) { window.html = window.htm.bind(window.preact.h); } })();</script>";

        // Pack definitions: ordered list of script tags per pack ID
        private static readonly Dictionary<string, string> PackScripts =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["core"] = CoreBootstrap,
                ["preact"] =
                    "<script src=\"https://unpkg.com/preact@10.25.3/dist/preact.umd.js\"></script>\n" +
                    "    <script src=\"https://unpkg.com/preact@10.25.3/hooks/dist/hooks.umd.js\"></script>\n" +
                    "    <script src=\"https://unpkg.com/htm@3.1.1/dist/htm.umd.js\"></script>\n" +
                    "    " + PreactBootstrap,
                ["tailwind"] =
                    "<script src=\"https://cdn.tailwindcss.com\"></script>",
                ["validation"] =
                    "<script src=\"https://cdn.jsdelivr.net/npm/zod@3/lib/index.umd.min.js\"></script>\n" +
                    "    <script>(function(){ if (window.Zod) { window.z = window.Zod; } })();</script>",
                ["date"] =
                    "<script src=\"https://cdn.jsdelivr.net/npm/dayjs@1/dayjs.min.js\"></script>",
                ["sanitize"] =
                    "<script src=\"https://cdn.jsdelivr.net/npm/dompurify@3/dist/purify.min.js\"></script>",
                ["charts"] =
                    "<script src=\"https://cdn.jsdelivr.net/npm/chart.js@4/dist/chart.umd.min.js\"></script>",
                ["viz"] =
                    "<script src=\"https://cdn.jsdelivr.net/npm/d3@7/dist/d3.min.js\"></script>",
                ["csv"] =
                    "<script src=\"https://cdn.jsdelivr.net/npm/papaparse@5/papaparse.min.js\"></script>",
                ["xlsx"] =
                    "<script src=\"https://cdn.jsdelivr.net/npm/xlsx@0.18.5/dist/xlsx.full.min.js\"></script>",
            };

        public static string[] GetAllPackIds()
        {
            return new[]
            {
                "core", "preact", "tailwind", "validation",
                "date", "sanitize", "charts", "viz", "csv", "xlsx"
            };
        }

        /// <summary>
        /// Returns concatenated HTML script tags for the given pack IDs.
        /// The "core" pack is always prepended if not already present.
        /// </summary>
        public static string GetPackScriptTags(IEnumerable<string> packIds)
        {
            var selected = new List<string>(packIds ?? new string[0]);

            // core is always first
            if (!selected.Contains("core"))
                selected.Insert(0, "core");

            var sb = new StringBuilder();
            bool first = true;
            foreach (string id in selected)
            {
                string scripts;
                if (!PackScripts.TryGetValue(id, out scripts))
                    continue;

                if (!first)
                    sb.AppendLine("    ");
                sb.Append("    ").Append(scripts);
                first = false;
            }
            return sb.ToString();
        }

        /// <summary>
        /// Returns the capability manifest string to include in LLM prompts.
        /// </summary>
        public static string GetCapabilityManifestForPrompt()
        {
            return
@"Capability manifest:
- default packs: core
- network: none
- max_html_bytes: 2000000
- available packs:
- core: Bridge helpers for state, events, and safe DOM utilities. (globals: ArtifactKit)
- preact: Small component runtime for interactive UIs. (globals: preact, preactHooks, html)
- tailwind: Utility-first styling runtime. (globals: (class-based runtime))
- validation: Schema validation for forms and structured inputs. (globals: Zod, z)
- date: Date/time formatting and simple date arithmetic. (globals: dayjs)
- sanitize: HTML sanitization before rendering user input. (globals: DOMPurify)
- charts: Canvas charts for metrics dashboards. (globals: Chart)
- viz: Custom data visualizations and complex SVG layouts. (globals: d3)
- csv: Fast CSV parsing and stringifying. (globals: Papa)
- xlsx: Read/write Excel spreadsheet files. (globals: XLSX)";
        }
    }
}
