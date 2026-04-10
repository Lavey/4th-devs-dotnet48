using System;
using System.Linq;
using FourthDevs.Wonderlands.Agents;
using FourthDevs.Wonderlands.Ai;
using FourthDevs.Wonderlands.Core;
using FourthDevs.Wonderlands.Models;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Wonderlands.Tools
{
    public static class NativeToolNames
    {
        public static readonly string[] All = {
            "delegate_to_agent", "read_artifact",
            "write_artifact", "complete_task", "block_task"
        };

        public static bool IsValid(string name) => Array.IndexOf(All, name) >= 0;
    }

    public class AgentToolConfig
    {
        public string Instructions { get; set; }
        public string[] Tools { get; set; }
        public bool WebSearch { get; set; }
    }

    public class ToolExecutionOutcome
    {
        public string Status { get; set; } // "continue", "completed", "blocked", "suspend"
        public string Output { get; set; }
        public string Message { get; set; }
        public WaitDescriptor Wait { get; set; }
    }

    public class ToolContext
    {
        public ToolCall Call { get; set; }
        public Job Job { get; set; }
        public Run Run { get; set; }
        public Runtime Rt { get; set; }
    }

    public delegate System.Threading.Tasks.Task<ToolExecutionOutcome> ToolHandler(ToolContext ctx);

    public static class Args
    {
        public static string GetString(JObject args, string field)
        {
            var val = args[field] != null ? args[field].ToString().Trim() : null;
            if (string.IsNullOrEmpty(val))
                throw new Exception(field + " must be a non-empty string");
            return val;
        }

        public static string GetOptionalString(JObject args, string field)
        {
            var val = args[field] != null ? args[field].ToString().Trim() : null;
            return string.IsNullOrEmpty(val) ? null : val;
        }

        public static int GetPositiveInteger(JObject args, string field, int fallback)
        {
            var token = args[field];
            if (token == null) return fallback;
            int val;
            if (int.TryParse(token.ToString(), out val) && val > 0) return val;
            return fallback;
        }

        public static string[] GetStringArray(JObject args, string field)
        {
            var arr = args[field] as JArray;
            if (arr == null) return new string[0];
            return arr.Select(t => t != null ? t.ToString().Trim() : null)
                .Where(s => !string.IsNullOrEmpty(s))
                .ToArray();
        }
    }
}
