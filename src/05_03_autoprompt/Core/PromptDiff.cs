using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FourthDevs.AutoPrompt.Core
{
    public static class PromptDiff
    {
        public static string ComputeDiff(string before, string after)
        {
            var beforeLines = before.Split(new[] { '\n' }, StringSplitOptions.None);
            var afterLines = after.Split(new[] { '\n' }, StringSplitOptions.None);
            var beforeSet = new HashSet<string>(beforeLines);
            var afterSet = new HashSet<string>(afterLines);

            var parts = new List<string>();

            foreach (var line in beforeLines)
            {
                if (!afterSet.Contains(line) && line.Trim().Length > 0)
                    parts.Add("- " + line.Trim());
            }

            foreach (var line in afterLines)
            {
                if (!beforeSet.Contains(line) && line.Trim().Length > 0)
                    parts.Add("+ " + line.Trim());
            }

            return parts.Count > 0 ? string.Join("\n", parts) : "(no textual diff)";
        }

        public static string SummarizeDiff(string before, string after)
        {
            var beforeLines = before.Split(new[] { '\n' }, StringSplitOptions.None);
            var afterLines = after.Split(new[] { '\n' }, StringSplitOptions.None);
            var beforeSet = new HashSet<string>(beforeLines);
            var afterSet = new HashSet<string>(afterLines);

            int added = afterLines.Count(l => !beforeSet.Contains(l) && l.Trim().Length > 0);
            int removed = beforeLines.Count(l => !afterSet.Contains(l) && l.Trim().Length > 0);

            return string.Format("+{0}/-{1} lines", added, removed);
        }
    }
}
