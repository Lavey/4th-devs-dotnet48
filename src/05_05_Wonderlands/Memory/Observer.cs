using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FourthDevs.Wonderlands.Ai;
using FourthDevs.Wonderlands.Models;

namespace FourthDevs.Wonderlands.Memory
{
    public class ObserverResult
    {
        public string Observations { get; set; }
        public string Raw { get; set; }
        public TokenUsage Usage { get; set; }
    }

    public static class Observer
    {
        private const int MaxSectionChars = 6000;
        private const int MaxToolChars = 3000;
        private const double CharsPerToken = 3.5;
        private const double SafetyMargin = 1.15;

        public static int EstimateTokens(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            return (int)Math.Ceiling(text.Length / CharsPerToken * SafetyMargin);
        }

        private static string Truncate(string text, int limit)
        {
            if (text == null) return "";
            return text.Length <= limit ? text : text.Substring(0, limit - 3) + "\u2026";
        }

        public static string SerializeItems(List<Item> items, Dictionary<string, string> runAgents)
        {
            var sorted = items.OrderBy(i => i.Sequence).ToList();
            var sb = new StringBuilder();
            foreach (var item in sorted)
            {
                string agent = "system";
                if (item.RunId != null && runAgents.ContainsKey(item.RunId))
                    agent = runAgents[item.RunId];

                switch (item.Type)
                {
                    case "message":
                        sb.AppendLine(string.Format("[{0}] {1}", agent,
                            Truncate(item.Content != null && item.Content["text"] != null ? item.Content["text"].ToString()
                                : item.Content != null && item.Content["role"] != null ? item.Content["role"].ToString() : "", MaxSectionChars)));
                        break;
                    case "decision":
                        sb.AppendLine(string.Format("[{0}] decided: {1}", agent,
                            Truncate(item.Content != null && item.Content["text"] != null ? item.Content["text"].ToString() : "", MaxSectionChars)));
                        break;
                    case "invocation":
                        sb.AppendLine(string.Format("[{0}] called {1}({2})", agent,
                            item.Content != null && item.Content["tool"] != null ? item.Content["tool"].ToString() : "",
                            Truncate(item.Content != null && item.Content["input"] != null ? item.Content["input"].ToString() : "{}", MaxToolChars)));
                        break;
                    case "result":
                        sb.AppendLine(string.Format("[{0}] {1} \u2192 {2}", agent,
                            item.Content != null && item.Content["tool"] != null ? item.Content["tool"].ToString() : "",
                            Truncate(item.Content != null && item.Content["output"] != null ? item.Content["output"].ToString() : "", MaxToolChars)));
                        break;
                }
            }
            return sb.ToString().TrimEnd();
        }

        private const string SystemPrompt = @"You are the memory consciousness of a multi-agent task system.
Your observations will be the ONLY historical context agents have about past work.

Extract high-fidelity observations from the task execution log below.
Do not chat. Do not explain. Output only structured XML.

Rules:
1) Prioritize user goals, completed deliverables, and key decisions.
2) Priority markers:
   - high: user goals, completed artifacts, critical decisions, final outcomes.
   - medium: active work, research findings, tool results, delegation patterns.
   - low: tentative details, intermediate steps.
3) Preserve concrete details: artifact paths, job titles, agent names, specific findings.
4) Capture inter-job relationships: delegations, dependencies, artifact usage.
5) Keep observations concise but information-dense.
6) Do NOT repeat observations that already exist in previous observations.

Output format (strict):
<observations>
* [high] ...
* [medium] ...
</observations>

<current-focus>
Primary: ...
</current-focus>";

        private static string BuildPrompt(string previousObservations, string itemHistory)
        {
            return string.Join("\n", new[]
            {
                "## Previous Observations", "",
                string.IsNullOrEmpty(previousObservations) ? "[none]" : previousObservations,
                "", "---", "",
                "Do not repeat these existing observations. Only extract new ones.",
                "", "## New Task Execution Log", "",
                string.IsNullOrEmpty(itemHistory) ? "[none]" : itemHistory,
                "", "---", "",
                "Extract new observations. Return only XML with <observations> and <current-focus>.",
            });
        }

        private static string ExtractTag(string text, string tag)
        {
            var match = Regex.Match(text, "<" + tag + ">([\\s\\S]*?)</" + tag + ">", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value.Trim() : null;
        }

        public static async Task<ObserverResult> RunObserver(string previousObservations, List<Item> items, Dictionary<string, string> runAgents)
        {
            var history = SerializeItems(items, runAgents);
            if (string.IsNullOrWhiteSpace(history))
                return new ObserverResult { Observations = "", Raw = "", Usage = TokenUsage.Empty() };

            var result = await AiClient.GenerateText(SystemPrompt, BuildPrompt(previousObservations, history));
            var observations = ExtractTag(result.Text, "observations") ?? result.Text.Trim();

            return new ObserverResult
            {
                Observations = observations,
                Raw = result.Text,
                Usage = result.Usage ?? TokenUsage.Empty(),
            };
        }
    }
}
