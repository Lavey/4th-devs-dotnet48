using System;
using System.IO;
using System.Threading.Tasks;
using FourthDevs.AutoPrompt.Cli;
using FourthDevs.AutoPrompt.Config;
using FourthDevs.AutoPrompt.Core;
using FourthDevs.AutoPrompt.Llm;
using FourthDevs.AutoPrompt.Project;
using FourthDevs.AutoPrompt.RunArtifacts;

namespace FourthDevs.AutoPrompt
{
    class Program
    {
        static int Main(string[] args)
        {
            try
            {
                return MainAsync(args).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("\nFatal: " + ex.Message);
                return 1;
            }
        }

        static async Task<int> MainAsync(string[] args)
        {
            if (args.Length == 0)
            {
                PrintUsage();
                return 1;
            }

            string command = args[0];

            if (command == "optimize")
            {
                return await RunOptimize(args);
            }
            else if (command == "verify")
            {
                return await RunVerify(args);
            }
            else if (command == "--help" || command == "-h")
            {
                PrintUsage();
                return 0;
            }
            else
            {
                Console.Error.WriteLine("Unknown command: " + command);
                PrintUsage();
                return 1;
            }
        }

        static void PrintUsage()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  05_03_autoprompt optimize <project-dir> [--iterations N] [--runs N]");
            Console.WriteLine("  05_03_autoprompt verify <project-dir> [--prompt path/to/prompt.md]");
        }

        static async Task<int> RunOptimize(string[] args)
        {
            string projectDir = "";
            int iterations = Defaults.MAX_ITERATIONS;
            int runs = Defaults.EVAL_RUNS;

            for (int i = 1; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg == "--help" || arg == "-h")
                {
                    Console.WriteLine("Usage: 05_03_autoprompt optimize <project-dir> [--iterations N] [--runs N]");
                    return 0;
                }
                else if (arg == "--iterations")
                {
                    i++;
                    if (i >= args.Length || !int.TryParse(args[i], out iterations) || iterations <= 0)
                    {
                        Console.Error.WriteLine("--iterations must be a positive integer");
                        return 1;
                    }
                }
                else if (arg == "--runs")
                {
                    i++;
                    if (i >= args.Length || !int.TryParse(args[i], out runs) || runs <= 0)
                    {
                        Console.Error.WriteLine("--runs must be a positive integer");
                        return 1;
                    }
                }
                else if (arg.StartsWith("--"))
                {
                    Console.Error.WriteLine("Unknown flag: " + arg);
                    return 1;
                }
                else
                {
                    if (!string.IsNullOrEmpty(projectDir))
                    {
                        Console.Error.WriteLine("Only one project directory may be provided");
                        return 1;
                    }
                    projectDir = arg;
                }
            }

            if (string.IsNullOrEmpty(projectDir))
            {
                Console.WriteLine("Usage: 05_03_autoprompt optimize <project-dir> [--iterations N] [--runs N]");
                return 1;
            }

            var project = ProjectLoader.Load(Path.GetFullPath(projectDir));
            var reporter = new ConsoleReporter();

            using (var llm = new LlmClient())
            {
                var optimizer = new OptimizeProject(llm);
                var run = await optimizer.RunAsync(project, iterations, runs, reporter);

                string runDir = RunWriter.WriteOptimizeRun(project, run);
                Console.WriteLine("\n  run: " + runDir);
                Console.WriteLine("  best prompt: " + Path.Combine(runDir, "prompt.best.md") + "\n");
            }

            return 0;
        }

        static async Task<int> RunVerify(string[] args)
        {
            string projectDir = "";
            string promptPath = "";

            for (int i = 1; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg == "--help" || arg == "-h")
                {
                    Console.WriteLine("Usage: 05_03_autoprompt verify <project-dir> [--prompt path/to/prompt.md]");
                    return 0;
                }
                else if (arg == "--prompt")
                {
                    i++;
                    if (i >= args.Length)
                    {
                        Console.Error.WriteLine("--prompt requires a path");
                        return 1;
                    }
                    promptPath = args[i];
                }
                else if (arg.StartsWith("--"))
                {
                    Console.Error.WriteLine("Unknown flag: " + arg);
                    return 1;
                }
                else
                {
                    if (!string.IsNullOrEmpty(projectDir))
                    {
                        Console.Error.WriteLine("Only one project directory may be provided");
                        return 1;
                    }
                    projectDir = arg;
                }
            }

            if (string.IsNullOrEmpty(projectDir))
            {
                Console.WriteLine("Usage: 05_03_autoprompt verify <project-dir> [--prompt path/to/prompt.md]");
                return 1;
            }

            var project = ProjectLoader.Load(Path.GetFullPath(projectDir));

            string resolvedPromptPath = !string.IsNullOrEmpty(promptPath)
                ? Path.GetFullPath(promptPath)
                : project.PromptPath;

            string prompt = File.ReadAllText(resolvedPromptPath);

            var reporter = new ConsoleReporter();

            using (var llm = new LlmClient())
            {
                var evaluator = new RunEvaluation(llm);
                var result = await evaluator.RunSingleAsync(
                    prompt,
                    project.VerifyCases,
                    project.ExtractionSchema,
                    project.Evaluation,
                    project.Models);

                reporter.PrintVerifyResult(project, resolvedPromptPath, result);
            }

            return 0;
        }
    }
}
