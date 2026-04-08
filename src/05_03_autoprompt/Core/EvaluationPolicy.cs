using System.Collections.Generic;
using System.Linq;
using System.Text;
using FourthDevs.AutoPrompt.Models;

namespace FourthDevs.AutoPrompt.Core
{
    public static class EvaluationPolicy
    {
        public static string Format(EvaluationConfig evaluation)
        {
            var sb = new StringBuilder();
            bool first = true;

            foreach (var section in evaluation.Sections)
            {
                if (!first) sb.AppendLine().AppendLine();
                first = false;

                sb.AppendLine(string.Format("Section: {0}", section.Key));
                sb.AppendLine(string.Format("- Weight: {0}", section.Weight));
                sb.AppendLine(string.Format("- Match items by: {0}", string.Join(", ", section.MatchBy)));
                sb.AppendLine("- Fields:");

                foreach (var kvp in section.Fields)
                {
                    sb.AppendLine(string.Format("  - {0}: {1}", kvp.Key, kvp.Value));
                }
            }

            return sb.ToString().TrimEnd();
        }
    }
}
