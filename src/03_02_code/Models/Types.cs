using System;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Code.Models
{
    // ─────────────────────────────────────────────────────────────────────────
    // Permission levels for the Deno sandbox
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Controls the Deno sandbox permission flags.
    /// </summary>
    internal enum PermissionLevel
    {
        /// <summary>No file or network access.</summary>
        Safe,
        /// <summary>Read/write to workspace only.</summary>
        Standard,
        /// <summary>Workspace + network access.</summary>
        Network,
        /// <summary>Full (--allow-all).</summary>
        Full
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tool definition (internal, not the Common ToolDefinition)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Describes a tool the agent can call, with a handler function.
    /// </summary>
    internal class LocalToolDefinition
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public JObject Parameters { get; set; }
        public Func<JObject, Task<object>> Handler { get; set; }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Execution result from the Deno sandbox
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Result of running code in the Deno sandbox.
    /// </summary>
    internal class ExecutionResult
    {
        public string Stdout { get; set; } = string.Empty;
        public string Stderr { get; set; } = string.Empty;
        public int ExitCode { get; set; }
        public bool TimedOut { get; set; }

        public override string ToString()
        {
            if (TimedOut)
                return "[Timed out]\nStdout:\n" + Stdout + "\nStderr:\n" + Stderr;
            if (ExitCode != 0)
                return "Exit code: " + ExitCode + "\nStdout:\n" + Stdout + "\nStderr:\n" + Stderr;
            return Stdout;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Options for sandbox execution
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Configuration options for running code in the Deno sandbox.
    /// </summary>
    internal class SandboxOptions
    {
        /// <summary>Timeout in milliseconds (default: 30000).</summary>
        public int Timeout { get; set; } = 30000;

        /// <summary>Sandbox permission level.</summary>
        public PermissionLevel PermissionLevel { get; set; } = PermissionLevel.Standard;

        /// <summary>Workspace directory path (for read/write access).</summary>
        public string Workspace { get; set; }

        /// <summary>TypeScript prelude code injected before user code.</summary>
        public string Prelude { get; set; } = string.Empty;

        /// <summary>HTTP bridge port (0 = no bridge).</summary>
        public int BridgePort { get; set; }
    }
}
