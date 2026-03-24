using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Events.Models
{
    // =========================================================================
    //  All model types for the multi-agent event architecture
    //  Port of types.ts from i-am-alice/4th-devs 03_02_events
    // =========================================================================

    // ---- Message & Session --------------------------------------------------

    public class Message
    {
        [JsonProperty("role")]
        public string Role { get; set; }

        [JsonProperty("content")]
        public string Content { get; set; }

        [JsonProperty("tool_calls", NullValueHandling = NullValueHandling.Ignore)]
        public JArray ToolCalls { get; set; }
    }

    public class MemoryState
    {
        [JsonProperty("active_observations")]
        public List<string> ActiveObservations { get; set; } = new List<string>();

        [JsonProperty("last_observed_index")]
        public int LastObservedIndex { get; set; }

        [JsonProperty("observation_token_count")]
        public int ObservationTokenCount { get; set; }

        [JsonProperty("generation_count")]
        public int GenerationCount { get; set; }

        [JsonProperty("observer_ran_this_request")]
        public bool ObserverRanThisRequest { get; set; }
    }

    public class Session
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("messages")]
        public List<JObject> Messages { get; set; } = new List<JObject>();

        [JsonProperty("memory")]
        public MemoryState Memory { get; set; } = new MemoryState();
    }

    // ---- Agent Template -----------------------------------------------------

    public class AgentTemplate
    {
        public string Name { get; set; }
        public string Model { get; set; }
        public List<string> Tools { get; set; } = new List<string>();
        public List<string> Capabilities { get; set; } = new List<string>();
        public string SystemPrompt { get; set; }
    }

    // ---- Tool Types ---------------------------------------------------------

    public class ToolDefinition
    {
        [JsonProperty("type")]
        public string Type { get; set; } = "function";

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("parameters")]
        public JObject Parameters { get; set; }
    }

    public class ToolResult
    {
        public string Kind { get; set; } = "text";
        public string Content { get; set; } = string.Empty;
        public string WaitId { get; set; }
        public string Question { get; set; }

        public static ToolResult Text(string content)
        {
            return new ToolResult { Kind = "text", Content = content ?? string.Empty };
        }

        public static ToolResult HumanRequest(string waitId, string question)
        {
            return new ToolResult
            {
                Kind = "human_request",
                WaitId = waitId,
                Question = question,
                Content = "Human decision requested (" + waitId + "): " + question
            };
        }
    }

    public class ToolRuntimeContext
    {
        public string Agent { get; set; }
        public string WorkspacePath { get; set; }
        public System.Threading.CancellationToken AbortSignal { get; set; }
    }

    public delegate System.Threading.Tasks.Task<ToolResult> ToolHandler(JObject args, ToolRuntimeContext ctx);

    public class Tool
    {
        public ToolDefinition Definition { get; set; }
        public ToolHandler Handler { get; set; }
    }

    // ---- Task Types ---------------------------------------------------------

    public static class TaskStatus
    {
        public const string Open = "open";
        public const string InProgress = "in-progress";
        public const string Blocked = "blocked";
        public const string WaitingHuman = "waiting-human";
        public const string Done = "done";
    }

    public static class TaskPriority
    {
        public const string Critical = "critical";
        public const string High = "high";
        public const string Medium = "medium";
        public const string Low = "low";
    }

    public class TaskFrontmatter
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Status { get; set; } = Models.TaskStatus.Open;
        public string Priority { get; set; } = Models.TaskPriority.Medium;
        public string AssignedTo { get; set; }
        public string RunId { get; set; }
        public List<string> DependsOn { get; set; } = new List<string>();
        public List<string> Capabilities { get; set; } = new List<string>();
        public string CreatedAt { get; set; }
        public string UpdatedAt { get; set; }
        public string StartedAt { get; set; }
        public string CompletedAt { get; set; }
        public string BlockedReason { get; set; }
        public string WaitId { get; set; }
        public string WaitQuestion { get; set; }
        public string WaitAnswer { get; set; }
        public string OutputFile { get; set; }
        public string OutputType { get; set; }
        public int Attempts { get; set; }
        public int MaxAttempts { get; set; } = 3;
        public string Phase { get; set; }
        public int Order { get; set; }
        public string WorkflowId { get; set; }
        public string Agent { get; set; }
        public List<string> Tags { get; set; } = new List<string>();
        public string ErrorMessage { get; set; }
    }

    public class TaskRecord
    {
        public string Path { get; set; }
        public string Slug { get; set; }
        public TaskFrontmatter Frontmatter { get; set; }
        public string Body { get; set; }
    }

    // ---- Heartbeat & Agent --------------------------------------------------

    public class HeartbeatEvent
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("round")]
        public int Round { get; set; }

        [JsonProperty("at")]
        public string At { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("agent", NullValueHandling = NullValueHandling.Ignore)]
        public string Agent { get; set; }

        [JsonProperty("taskId", NullValueHandling = NullValueHandling.Ignore)]
        public string TaskId { get; set; }

        [JsonProperty("data", NullValueHandling = NullValueHandling.Ignore)]
        public JObject Data { get; set; }
    }

    public class AgentUsage
    {
        public int TotalEstimatedTokens { get; set; }
        public int TotalActualTokens { get; set; }
        public int Turns { get; set; }
        public double CalibrationRatio { get; set; } = 1.0;
    }

    public class AgentRunResult
    {
        public string Status { get; set; }
        public string Response { get; set; }
        public string WaitId { get; set; }
        public string WaitQuestion { get; set; }
        public string Error { get; set; }
        public AgentUsage Usage { get; set; } = new AgentUsage();
    }

    // ---- Create-task input --------------------------------------------------

    public class CreateTaskInput
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Body { get; set; }
        public string Priority { get; set; } = Models.TaskPriority.Medium;
        public List<string> DependsOn { get; set; } = new List<string>();
        public List<string> Capabilities { get; set; } = new List<string>();
        public string Phase { get; set; }
        public int Order { get; set; }
        public string WorkflowId { get; set; }
        public string Agent { get; set; }
        public string OutputFile { get; set; }
        public string OutputType { get; set; }
    }
}
