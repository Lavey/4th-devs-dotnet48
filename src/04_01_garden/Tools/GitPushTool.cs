using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using FourthDevs.Garden.Models;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Garden.Tools
{
    /// <summary>
    /// Git add, commit, and push vault changes.
    /// Port of 04_01_garden/src/tools/git-push.ts.
    /// </summary>
    internal static class GitPushTool
    {
        private static readonly string VaultDir =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "vault");

        private static readonly string ProjectRoot =
            Path.GetDirectoryName(VaultDir);

        public static readonly LocalToolDefinition Definition = new LocalToolDefinition
        {
            Name = "git_push",
            Description =
                "Commit vault changes and push to GitHub. " +
                "This triggers CI to build and deploy to GitHub Pages.",
            Parameters = new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["message"] = new JObject
                    {
                        ["type"] = "string",
                        ["description"] = "Git commit message"
                    }
                },
                ["required"] = new JArray { "message" },
                ["additionalProperties"] = false
            },
            Handler = ExecuteAsync
        };

        private static Task<ToolExecutionResult> ExecuteAsync(JObject args)
        {
            try
            {
                string message = (string)args["message"];
                if (string.IsNullOrWhiteSpace(message))
                    return Task.FromResult(new ToolExecutionResult(false, "\"message\" cannot be empty."));

                // git add vault/
                RunGit("add vault/");

                // git status --porcelain vault/
                string status = RunGit("status --porcelain vault/");
                if (string.IsNullOrWhiteSpace(status))
                    return Task.FromResult(new ToolExecutionResult(true, "No changes to push."));

                // git commit
                RunGit("commit -m \"" + message.Replace("\"", "\\\"") + "\" -- vault/");

                // git push
                RunGit("push");

                return Task.FromResult(new ToolExecutionResult(true, "Pushed: " + message));
            }
            catch (Exception ex)
            {
                return Task.FromResult(new ToolExecutionResult(false, "Error: " + ex.Message));
            }
        }

        private static string RunGit(string arguments)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "-C \"" + ProjectRoot + "\" " + arguments,
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
                process.WaitForExit(30000);
                return (stdout + "\n" + stderr).Trim();
            }
        }
    }
}
