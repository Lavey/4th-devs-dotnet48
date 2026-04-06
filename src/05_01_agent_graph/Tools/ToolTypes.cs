using System;
using System.Collections.Generic;
using System.Linq;
using FourthDevs.AgentGraph.Ai;
using FourthDevs.AgentGraph.Core;
using FourthDevs.AgentGraph.Models;
using Newtonsoft.Json.Linq;

namespace FourthDevs.AgentGraph.Tools
{
    public static class ActorToolNames
    {
        public static readonly string[] All = {
            "create_actor", "delegate_task", "read_artifact",
            "write_artifact", "send_email", "complete_task", "block_task"
        };

        public static bool IsValid(string name) => Array.IndexOf(All, name) >= 0;
    }

    public class ActorToolConfig
    {
        public string Instructions { get; set; }
        public string[] Tools { get; set; }
        public bool WebSearch { get; set; }
    }

    public class ToolExecutionOutcome
    {
        public string Status { get; set; } // "continue", "completed", "blocked"
        public string Output { get; set; }
        public string Message { get; set; }
    }

    public class ToolContext
    {
        public ToolCall Call { get; set; }
        public AgentTask Task { get; set; }
        public Actor Actor { get; set; }
        public Runtime Rt { get; set; }
    }

    public delegate System.Threading.Tasks.Task<ToolExecutionOutcome> ToolHandler(ToolContext ctx);

    // ── Argument helpers ─────────────────────────────────────────────────

    public static class Args
    {
        public static string GetString(JObject args, string field)
        {
            var val = args[field]?.ToString()?.Trim();
            if (string.IsNullOrEmpty(val))
                throw new Exception(field + " must be a non-empty string");
            return val;
        }

        public static string GetOptionalString(JObject args, string field)
        {
            var val = args[field]?.ToString()?.Trim();
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
            return arr.Select(t => t?.ToString()?.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToArray();
        }

        public static string[] GetToolNameArray(JObject args, string field)
        {
            var tools = GetStringArray(args, field).Where(ActorToolNames.IsValid).Distinct().ToArray();
            if (tools.Length == 0)
                throw new Exception(field + " must include at least one valid tool");
            return tools;
        }
    }
}
