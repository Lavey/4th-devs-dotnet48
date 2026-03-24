using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using FourthDevs.Events.Helpers;

namespace FourthDevs.Events.Memory
{
    /// <summary>
    /// Compresses observations when they exceed the token budget.
    /// </summary>
    internal static class Reflector
    {
        private const string SystemPrompt =
@"You are a memory compressor. Given a list of observations, compress them into fewer, more concise observations that preserve the most important information.
Return ONLY a JSON array of strings. Aim to reduce the count by roughly half while keeping key facts.";

        public static async Task<List<string>> CompressObservations(
            List<string> observations, string model)
        {
            if (observations == null || observations.Count <= 2)
                return observations ?? new List<string>();

            string input = "Observations to compress:\n";
            for (int i = 0; i < observations.Count; i++)
            {
                input += (i + 1) + ". " + observations[i] + "\n";
            }

            try
            {
                string responseText = await Observer.CallChatCompletions(
                    model, SystemPrompt, input, 0.2);

                responseText = responseText.Trim();
                if (responseText.StartsWith("["))
                {
                    var arr = JArray.Parse(responseText);
                    var result = new List<string>();
                    foreach (var item in arr)
                    {
                        string s = item.ToString();
                        if (!string.IsNullOrWhiteSpace(s))
                            result.Add(s);
                    }
                    return result;
                }
            }
            catch (Exception ex)
            {
                Core.Logger.Warn("reflector", "Failed to compress: " + ex.Message);
            }

            return observations;
        }
    }
}
