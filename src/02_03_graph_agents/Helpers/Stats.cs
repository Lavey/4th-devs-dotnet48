using System;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Lesson08_GraphAgents.Helpers
{
    /// <summary>
    /// Token usage statistics tracker.
    /// Mirrors 02_03_graph_agents/src/helpers/stats.js (i-am-alice/4th-devs)
    /// </summary>
    internal static class Stats
    {
        private static int _input;
        private static int _output;
        private static int _reasoning;
        private static int _cached;
        private static int _requests;

        internal static void RecordUsage(JToken usage)
        {
            if (usage == null) return;
            _input     += usage["input_tokens"]?.ToObject<int>() ?? 0;
            _output    += usage["output_tokens"]?.ToObject<int>() ?? 0;
            _reasoning += usage["output_tokens_details"]?["reasoning_tokens"]?.ToObject<int>() ?? 0;
            _cached    += usage["input_tokens_details"]?["cached_tokens"]?.ToObject<int>() ?? 0;
            _requests  += 1;
        }

        internal static void LogStats()
        {
            int visible = _output - _reasoning;
            string summary = _requests + " requests, " + _input + " in";
            if (_cached > 0) summary += " (" + _cached + " cached)";
            summary += ", " + _output + " out";
            if (_reasoning > 0) summary += " (" + _reasoning + " reasoning + " + visible + " visible)";
            Console.WriteLine("\n📊 Stats: " + summary + "\n");
        }

        internal static void ResetStats()
        {
            _input = _output = _reasoning = _cached = _requests = 0;
        }
    }
}
