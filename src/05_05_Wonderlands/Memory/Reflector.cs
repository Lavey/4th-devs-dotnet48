using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FourthDevs.Wonderlands.Ai;
using FourthDevs.Wonderlands.Models;

namespace FourthDevs.Wonderlands.Memory
{
    public class ReflectorResult
    {
        public string Observations { get; set; }
        public int TokenCount { get; set; }
        public int CompressionLevel { get; set; }
        public TokenUsage Usage { get; set; }
    }

    public static class Reflector
    {
        private static readonly string[] CompressionLevels =
        {
            "",
            "Condense older observations more aggressively. Preserve detail for recent ones only.",
            "Heavily condense. Remove redundancy, keep only durable facts, active commitments, and blockers.",
        };

        private const string SystemPrompt = @"You are the observation reflector of a multi-agent task system.
You must reorganize and compress observations while preserving continuity.

Rules:
1) Your output is the ENTIRE memory. Anything omitted is forgotten.
2) Keep user goals and completed deliverables as highest priority.
3) Keep active jobs, blockers, and artifact references.
4) Condense older details first. Preserve recent details more strongly.
5) Resolve contradictions by preferring newer observations.
6) Output only compressed observations in XML:

<observations> ... </observations>";

        private static string BuildPrompt(string observations, string guidance)
        {
            var lines = new List<string>
            {
                "Compress and reorganize the observation memory below."
            };
            if (!string.IsNullOrEmpty(guidance))
                lines.Add("Additional guidance: " + guidance);
            lines.AddRange(new[] { "", "<observations>", observations, "</observations>" });
            return string.Join("\n", lines);
        }

        private static string ExtractTag(string text, string tag)
        {
            var match = Regex.Match(text, "<" + tag + ">([\\s\\S]*?)</" + tag + ">", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value.Trim() : null;
        }

        public static async Task<ReflectorResult> RunReflector(string observations, int targetTokens)
        {
            var bestObservations = observations;
            var bestTokens = Observer.EstimateTokens(observations);
            var bestLevel = -1;
            var cumulative = TokenUsage.Empty();

            for (int level = 0; level < CompressionLevels.Length; level++)
            {
                var result = await AiClient.GenerateText(SystemPrompt, BuildPrompt(observations, CompressionLevels[level]));
                if (result.Usage != null) cumulative = TokenUsage.Add(cumulative, result.Usage);

                var compressed = ExtractTag(result.Text, "observations") ?? result.Text.Trim();
                if (string.IsNullOrEmpty(compressed)) continue;

                var tokens = Observer.EstimateTokens(compressed);
                if (tokens < bestTokens)
                {
                    bestObservations = compressed;
                    bestTokens = tokens;
                    bestLevel = level;
                }

                if (tokens <= targetTokens)
                    return new ReflectorResult { Observations = compressed, TokenCount = tokens, CompressionLevel = level, Usage = cumulative };
            }

            return new ReflectorResult { Observations = bestObservations, TokenCount = bestTokens, CompressionLevel = bestLevel, Usage = cumulative };
        }
    }
}
