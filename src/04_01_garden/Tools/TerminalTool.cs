using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using FourthDevs.Garden.Models;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Garden.Tools
{
    /// <summary>
    /// Execute shell commands locally in the vault directory.
    /// Port of 04_01_garden/src/tools/terminal.ts.
    /// </summary>
    internal static class TerminalTool
    {
        private const int DefaultTimeoutSeconds = 30;

        private static readonly string VaultDir =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "vault");

        public static readonly LocalToolDefinition Definition = new LocalToolDefinition
        {
            Name = "terminal",
            Description =
                "Execute a shell command and return stdout/stderr text. " +
                "Commands always run from the vault root.",
            Parameters = new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["command"] = new JObject
                    {
                        ["type"] = "string",
                        ["description"] = "Shell command to execute."
                    },
                    ["timeout_seconds"] = new JObject
                    {
                        ["type"] = "integer",
                        ["description"] = "Optional timeout in seconds (default: " + DefaultTimeoutSeconds + ")."
                    }
                },
                ["required"] = new JArray { "command" },
                ["additionalProperties"] = false
            },
            Handler = ExecuteAsync
        };

        private static Task<ToolExecutionResult> ExecuteAsync(JObject args)
        {
            try
            {
                string command = (string)args["command"];
                if (string.IsNullOrWhiteSpace(command))
                    return Task.FromResult(new ToolExecutionResult(false, "\"command\" cannot be empty."));

                int timeout = args["timeout_seconds"] != null && args["timeout_seconds"].Type == JTokenType.Integer
                    ? (int)args["timeout_seconds"]
                    : DefaultTimeoutSeconds;

                if (!Directory.Exists(VaultDir))
                    Directory.CreateDirectory(VaultDir);

                bool isWindows = Environment.OSVersion.Platform == PlatformID.Win32NT;
                string shell = isWindows ? "cmd.exe" : "/bin/sh";
                string shellArgs = isWindows ? "/c " + command : "-c " + command;

                var psi = new ProcessStartInfo
                {
                    FileName = shell,
                    Arguments = shellArgs,
                    WorkingDirectory = VaultDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = new Process { StartInfo = psi })
                {
                    process.Start();
                    string stdout = process.StandardOutput.ReadToEnd();
                    string stderr = process.StandardError.ReadToEnd();

                    bool exited = process.WaitForExit(timeout * 1000);
                    if (!exited)
                    {
                        try { process.Kill(); } catch { /* ignore */ }
                        return Task.FromResult(new ToolExecutionResult(false, "Command timed out after " + timeout + "s"));
                    }

                    string output = (stdout + "\n" + stderr).Trim();
                    if (string.IsNullOrEmpty(output))
                        output = "(no output)";

                    if (process.ExitCode != 0)
                        return Task.FromResult(new ToolExecutionResult(false, "[exit " + process.ExitCode + "]\n" + output));

                    return Task.FromResult(new ToolExecutionResult(true, output));
                }
            }
            catch (Exception ex)
            {
                return Task.FromResult(new ToolExecutionResult(false, "Error: " + ex.Message));
            }
        }
    }
}
