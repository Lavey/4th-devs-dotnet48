using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FourthDevs.System.Agent;

namespace FourthDevs.System
{
    /// <summary>
    /// Lesson 19 – Multi-Agent System (04_04_system)
    ///
    /// Multi-agent system where a markdown knowledge base drives agent behavior.
    /// Workflows, templates, rules, and agent identities are all vault notes — not code.
    ///
    /// Three modes:
    ///   (no args)       — Interactive: ask Alice anything about the knowledge base
    ///   daily-news      — Workflow: run the daily-news pipeline (research → assemble → deliver)
    ///   examples [N]    — Run example queries (all 7, or just query #N)
    ///
    /// Source: 04_04_system (i-am-alice/4th-devs)
    /// </summary>
    internal static class Program
    {
        private static readonly string DemoDir = Path.Combine("demo");

        static void Main(string[] args)
        {
            MainAsync(args).GetAwaiter().GetResult();
        }

        static async Task MainAsync(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            string mode = args.Length > 0 ? args[0].ToLowerInvariant() : string.Empty;

            Console.WriteLine();
            Console.WriteLine("========================================");
            Console.WriteLine("  04_04_system — Multi-Agent System");
            Console.WriteLine("========================================");
            Console.WriteLine();

            switch (mode)
            {
                case "daily-news":
                    await RunDailyNews();
                    break;
                case "examples":
                    int? queryNum = args.Length > 1 ? (int?)int.Parse(args[1]) : null;
                    await RunExamples(queryNum);
                    break;
                default:
                    await RunInteractive();
                    break;
            }
        }

        // ----------------------------------------------------------------
        // Interactive mode
        // ----------------------------------------------------------------

        private static async Task RunInteractive()
        {
            Console.WriteLine("Mode: Interactive — ask Alice about the knowledge base.");
            Console.WriteLine("Type 'exit' or 'quit' to stop.");
            Console.WriteLine();

            if (!ConfirmRun())
                return;

            while (true)
            {
                Console.Write("You: ");
                string query = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(query))
                    continue;

                string lower = query.Trim().ToLowerInvariant();
                if (lower == "exit" || lower == "quit")
                    break;

                string result = await AgentRunner.RunAgentAsync("alice", query);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine();
                Console.WriteLine($"[Alice] {result}");
                Console.ResetColor();
                Console.WriteLine();
            }
        }

        // ----------------------------------------------------------------
        // Daily-news workflow
        // ----------------------------------------------------------------

        private static async Task RunDailyNews()
        {
            Console.WriteLine("Mode: Daily News Workflow");
            Console.WriteLine();

            if (!ConfirmRun())
                return;

            string today = DateTime.UtcNow.ToString("yyyy-MM-dd");

            // Clean output directory for today
            string outputDir = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "workspace", "ops", "daily-news", today);
            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, true);

            string prompt = string.Join(" ", new[]
            {
                $"Run the daily-news workflow for {today}.",
                "",
                "Steps:",
                "1. Read workspace/ops/daily-news/_info.md to understand the workflow, sources, and phases.",
                "2. Read each phase file (01-research.md, 02-assemble.md, 03-deliver.md) to learn the agent assignments.",
                "3. Execute phases strictly sequentially: delegate phase 1, wait for its result, then delegate phase 2, wait for its result, then delegate phase 3.",
                "   NEVER delegate multiple phases in the same turn — each phase depends on the previous one's output files existing.",
                "4. After all phases complete, summarize what was produced."
            });

            string result = await AgentRunner.RunAgentAsync("alice", prompt);

            Console.WriteLine();
            Console.WriteLine("========================================");
            Console.WriteLine("  Daily News Result");
            Console.WriteLine("========================================");
            Console.WriteLine();
            Console.WriteLine(result);
        }

        // ----------------------------------------------------------------
        // Example queries
        // ----------------------------------------------------------------

        private static readonly string[] ExampleQueries = new[]
        {
            "I just had an idea: what if we built a CLI tool that watches the workspace folder and auto-generates a graph of wikilinks between notes? Could be useful for visualization.",
            "Add a knowledge note about RAG (Retrieval-Augmented Generation). It combines vector search with LLM generation to ground answers in external documents. Key concepts: embedding, chunking, retrieval, reranking. Good source: https://arxiv.org/abs/2005.11401",
            "Add my friend Marcin Kowalski. We went to university together, now he works at Google as a senior engineer. We chat on Signal, he lives in Zurich. Big fan of Rust and distributed systems.",
            "I want to try building a small Bun script that uses the marked library to parse all markdown files in a folder and output a JSON graph of wikilinks. Call it \"wikilink-graph-parser\". Just a quick experiment to see if the parsing is reliable enough.",
            "Add Andrej Karpathy YouTube channel as a source. He covers deep learning, neural networks, and building AI from scratch. URL: https://www.youtube.com/@AndrejKarpathy — I watch it weekly, one of the best AI educators out there.",
            "Add a shared note. Title: \"Building AI Agents from Scratch\". Description: \"Workshop teaching JavaScript developers how to build LLM agents with tool-calling patterns\". Format: workshop. Audience: mid-level JS devs new to LLM tool-calling. Core message: agents are just loops with tools, no magic. Distribution: eduweb.pl.",
            "Add a note about Claude Code — it's an AI coding tool by Anthropic that runs in the terminal. I use it daily for programming. URL: https://docs.anthropic.com/en/docs/claude-code"
        };

        private static async Task RunExamples(int? queryNumber)
        {
            Console.WriteLine("Mode: Example Queries");
            Console.WriteLine();

            if (!ConfirmRun())
                return;

            List<string> selected;

            if (queryNumber.HasValue)
            {
                int idx = queryNumber.Value - 1;
                if (idx < 0 || idx >= ExampleQueries.Length)
                {
                    Console.Error.WriteLine($"Invalid query number. Use 1-{ExampleQueries.Length} or omit for all.");
                    return;
                }
                selected = new List<string> { ExampleQueries[idx] };
            }
            else
            {
                selected = new List<string>(ExampleQueries);
            }

            // Clean up files that may have been created by previous runs
            CleanupExampleOutputs();

            for (int i = 0; i < selected.Count; i++)
            {
                string query = selected[i];
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine($"[USER] Query {i + 1}: {(query.Length > 120 ? query.Substring(0, 120) + "…" : query)}");
                Console.ResetColor();
                Console.WriteLine();

                string result = await AgentRunner.RunAgentAsync("alice", query);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[Alice] {result}");
                Console.ResetColor();
                Console.WriteLine();
            }
        }

        private static void CleanupExampleOutputs()
        {
            string workspace = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "workspace");
            string[] paths = new[]
            {
                "craft/ideas/wikilink-graph-cli.md",
                "craft/knowledge/AI/retrieval-augmented-generation.md",
                "craft/lab/wikilink-graph-parser.md",
                "craft/shared/building-ai-agents-from-scratch.md",
                "world/people/marcin-kowalski.md",
                "world/sources/andrej-karpathy.md",
                "world/tools/claude-code.md",
            };

            foreach (string rel in paths)
            {
                string full = Path.Combine(workspace, rel.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(full))
                {
                    try { File.Delete(full); } catch { }
                }
            }
        }

        // ----------------------------------------------------------------
        // Confirm
        // ----------------------------------------------------------------

        private static bool ConfirmRun()
        {
            Console.WriteLine("⚠️  UWAGA: Uruchomienie tego agenta może zużyć zauważalną liczbę tokenów.");
            Console.WriteLine($"   Jeśli nie chcesz uruchamiać go teraz, najpierw sprawdź plik demo:");
            Console.WriteLine($"   Demo: {DemoDir}");
            Console.WriteLine();
            Console.Write("Czy chcesz kontynuować? (yes/y): ");
            string answer = (Console.ReadLine() ?? string.Empty).Trim().ToLowerInvariant();
            if (answer != "yes" && answer != "y")
            {
                Console.WriteLine("Przerwano.");
                return false;
            }
            return true;
        }
    }
}
