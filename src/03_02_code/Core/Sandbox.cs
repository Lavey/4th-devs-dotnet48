using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FourthDevs.Code.Models;

namespace FourthDevs.Code.Core
{
    /// <summary>
    /// Deno sandbox for executing TypeScript/JavaScript code.
    ///
    /// Mirrors sandbox.ts from 03_02_code (i-am-alice/4th-devs).
    /// </summary>
    internal static class Sandbox
    {
        /// <summary>
        /// Checks that deno is installed and pre-caches the pdfkit npm module
        /// so the sandbox can generate PDFs.
        /// </summary>
        public static async Task EnsureSandboxAsync()
        {
            // Check deno is available
            var denoResult = await RunProcessAsync("deno", "--version", null, 15000);
            if (denoResult.ExitCode != 0)
                throw new InvalidOperationException(
                    "Deno is not installed or not on PATH. " +
                    "Install from https://deno.land. " +
                    "stderr: " + denoResult.Stderr);

            Console.WriteLine("[sandbox] Deno: " + denoResult.Stdout.Split('\n')[0].Trim());

            // Pre-cache pdfkit via deno cache
            Console.WriteLine("[sandbox] Caching npm:pdfkit...");
            var cacheResult = await RunProcessAsync(
                "deno", "cache --node-modules-dir=auto npm:pdfkit", null, 60000);
            if (cacheResult.ExitCode != 0)
                Console.Error.WriteLine("[sandbox] Warning: pdfkit cache failed: " + cacheResult.Stderr);
            else
                Console.WriteLine("[sandbox] pdfkit cached.");
        }

        /// <summary>
        /// Executes TypeScript code in a Deno sandbox with the given options.
        /// </summary>
        public static async Task<ExecutionResult> ExecuteCodeAsync(string code, SandboxOptions options)
        {
            if (options == null) options = new SandboxOptions();

            // Combine prelude + user code
            string fullCode = string.IsNullOrWhiteSpace(options.Prelude)
                ? code
                : options.Prelude + "\n\n// ---- User code ----\n" + code;

            // Write to temp file
            string tempDir = Path.Combine(Path.GetTempPath(), "deno_sandbox_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            string tempFile = Path.Combine(tempDir, "script.ts");
            File.WriteAllText(tempFile, fullCode, Encoding.UTF8);

            try
            {
                // Build permission flags
                var flags = BuildPermissionFlags(
                    options.PermissionLevel, options.Workspace, options.BridgePort);

                var argsList = new List<string> { "run" };
                argsList.AddRange(flags);
                argsList.Add("--node-modules-dir=auto");
                argsList.Add("--no-prompt");
                argsList.Add(tempFile);

                string args = string.Join(" ", argsList);

                return await RunProcessAsync("deno", args, options.Workspace, options.Timeout);
            }
            finally
            {
                // Cleanup temp files
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }

        /// <summary>
        /// Generates Deno permission flags based on the permission level.
        /// </summary>
        internal static List<string> BuildPermissionFlags(
            PermissionLevel level, string workspace, int bridgePort)
        {
            var flags = new List<string>();

            switch (level)
            {
                case PermissionLevel.Safe:
                    // No permissions at all
                    break;

                case PermissionLevel.Standard:
                    if (!string.IsNullOrEmpty(workspace))
                    {
                        flags.Add("--allow-read=" + workspace);
                        flags.Add("--allow-write=" + workspace);
                    }
                    if (bridgePort > 0)
                        flags.Add("--allow-net=127.0.0.1:" + bridgePort);
                    break;

                case PermissionLevel.Network:
                    if (!string.IsNullOrEmpty(workspace))
                    {
                        flags.Add("--allow-read=" + workspace);
                        flags.Add("--allow-write=" + workspace);
                    }
                    flags.Add("--allow-net");
                    break;

                case PermissionLevel.Full:
                    flags.Add("--allow-all");
                    break;
            }

            return flags;
        }

        private static async Task<ExecutionResult> RunProcessAsync(
            string fileName, string arguments, string workingDir, int timeoutMs)
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            if (!string.IsNullOrWhiteSpace(workingDir))
                psi.WorkingDirectory = workingDir;

            var stdoutSb = new StringBuilder();
            var stderrSb = new StringBuilder();

            using (var proc = new Process())
            {
                proc.StartInfo = psi;
                proc.OutputDataReceived += (s, e) =>
                {
                    if (e.Data != null)
                        stdoutSb.AppendLine(e.Data);
                };
                proc.ErrorDataReceived += (s, e) =>
                {
                    if (e.Data != null)
                        stderrSb.AppendLine(e.Data);
                };

                proc.Start();
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();

                bool finished = await WaitForExitAsync(proc, timeoutMs);

                if (!finished)
                {
                    try { proc.Kill(); } catch { }
                    return new ExecutionResult
                    {
                        Stdout = stdoutSb.ToString(),
                        Stderr = stderrSb.ToString(),
                        ExitCode = -1,
                        TimedOut = true
                    };
                }

                return new ExecutionResult
                {
                    Stdout = stdoutSb.ToString(),
                    Stderr = stderrSb.ToString(),
                    ExitCode = proc.ExitCode,
                    TimedOut = false
                };
            }
        }

        private static Task<bool> WaitForExitAsync(Process proc, int timeoutMs)
        {
            var tcs = new TaskCompletionSource<bool>();

            // Register exit handler
            proc.EnableRaisingEvents = true;
            proc.Exited += (s, e) => tcs.TrySetResult(true);

            // If the process already exited before we registered the handler
            if (proc.HasExited)
            {
                tcs.TrySetResult(true);
                return tcs.Task;
            }

            // Timeout
            if (timeoutMs > 0)
            {
                var cts = new CancellationTokenSource(timeoutMs);
                cts.Token.Register(() => tcs.TrySetResult(false), useSynchronizationContext: false);
            }

            return tcs.Task;
        }
    }
}
