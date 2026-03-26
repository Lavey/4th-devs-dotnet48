using System.Threading.Tasks;
using FourthDevs.Common;
using Newtonsoft.Json.Linq;

namespace FourthDevs.ContextAgent.Memory
{
    internal static class Reflector
    {
        private const string SystemPrompt =
            "You are the observation reflector — part of the memory consciousness.\n\n" +
            "You must reorganize and compress observations while preserving continuity.\n\n" +
            "Rules:\n" +
            "1) Your output is the ENTIRE memory. Anything omitted is forgotten.\n" +
            "2) Preserve source tags ([user], [assistant], [tool:name]) on every observation.\n" +
            "3) [user] observations are highest priority — never drop them unless contradicted by a newer [user] observation.\n" +
            "4) [assistant] elaborations are lowest priority — condense or drop them first.\n" +
            "5) [tool:*] outcomes should be kept as concise action records.\n" +
            "6) Condense older details first. Preserve recent details more strongly.\n" +
            "7) Resolve contradictions by preferring newer observations.\n" +
            "8) Use the same bullet format as input. Do NOT restructure into XML attributes or other schemas.\n\n" +
            "Output format:\n" +
            "<observations>\n" +
            "* \U0001f534 [user] ...\n" +
            "* \U0001f7e1 [tool:write_file] ...\n" +
            "</observations>";

        public static async Task<ReflectorResult> RunAsync(string observations)
        {
            string userPrompt =
                "Compress and reorganize the following observations. Target ~200 tokens.\n\n" +
                "<observations>\n" +
                observations +
                "\n</observations>";

            string model = AiConfig.ResolveModel(MemoryConfig.ReflectorModel);

            var inputMessages = new JArray
            {
                new JObject { ["type"] = "message", ["role"] = "user", ["content"] = userPrompt }
            };

            var body = new JObject
            {
                ["model"] = model,
                ["instructions"] = SystemPrompt,
                ["input"] = inputMessages,
                ["store"] = false
            };

            string response = await LlmClient.PostAsync(body).ConfigureAwait(false);

            System.Text.RegularExpressions.Match match =
                System.Text.RegularExpressions.Regex.Match(
                    response, "<observations>(.*?)</observations>",
                    System.Text.RegularExpressions.RegexOptions.Singleline |
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            string compressed = match.Success ? match.Groups[1].Value.Trim() : response.Trim();

            return new ReflectorResult
            {
                Observations = compressed,
                RawResponse = response
            };
        }
    }

    internal class ReflectorResult
    {
        public string Observations { get; set; }
        public string RawResponse { get; set; }
    }
}
