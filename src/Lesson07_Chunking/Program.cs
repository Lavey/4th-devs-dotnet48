using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using FourthDevs.Lesson07_Chunking.Strategies;
using Newtonsoft.Json;

namespace FourthDevs.Lesson07_Chunking
{
    /// <summary>
    /// Lesson 07 – Chunking
    ///
    /// Runs four text chunking strategies on workspace/example.md and saves
    /// each result as JSONL to workspace/example-[strategy].jsonl.
    ///
    /// Strategies:
    ///   1. Characters — fixed-size windows with overlap
    ///   2. Separators — splits on headings and paragraph boundaries
    ///   3. Context    — separator-based chunks enriched with LLM-generated context
    ///   4. Topics     — LLM identifies logical topic boundaries
    ///
    /// Source: 02_02_chunking/app.js (i-am-alice/4th-devs)
    /// </summary>
    internal static class Program
    {
        private static readonly string WorkspaceDir =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "workspace");

        private const string InputFile = "example.md";

        static void Main(string[] args)
        {
            MainAsync().GetAwaiter().GetResult();
        }

        static async Task MainAsync()
        {
            PrintBanner();

            if (!ConfirmRun()) return;

            string inputPath = Path.Combine(WorkspaceDir, InputFile);

            if (!File.Exists(inputPath))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("ERROR: Input file not found: " + inputPath);
                Console.ResetColor();
                return;
            }

            string text = File.ReadAllText(inputPath, Encoding.UTF8);
            Console.WriteLine(string.Format(
                "Source: {0} ({1} chars)\n", InputFile, text.Length));

            // 1. Characters
            Console.WriteLine("1. Characters...");
            await Save("characters", Characters.ChunkByCharacters(text));

            // 2. Separators
            Console.WriteLine("2. Separators...");
            await Save("separators", Separators.ChunkBySeparators(text, source: InputFile));

            // 3. Context (LLM-enriched)
            Console.WriteLine("3. Context (LLM-enriched)...");
            await Save("context", await Context.ChunkWithContext(text, source: InputFile));

            // 4. Topics (AI-driven)
            Console.WriteLine("4. Topics (AI-driven)...");
            await Save("topics", await Topics.ChunkByTopics(text, source: InputFile));

            Console.WriteLine("\nDone.");
        }

        // ----------------------------------------------------------------
        // Save JSONL
        // ----------------------------------------------------------------

        static Task Save(string name, List<Chunk> chunks)
        {
            string path = Path.Combine(WorkspaceDir,
                string.Format("example-{0}.jsonl", name));

            var sb = new StringBuilder();
            foreach (var chunk in chunks)
                sb.AppendLine(JsonConvert.SerializeObject(chunk));

            File.WriteAllText(path, sb.ToString().TrimEnd(), Encoding.UTF8);
            Console.WriteLine(string.Format(
                "  ✓ workspace/example-{0}.jsonl ({1} chunks)", name, chunks.Count));

            return Task.FromResult(0);
        }

        // ----------------------------------------------------------------
        // Startup confirmation
        // ----------------------------------------------------------------

        static bool ConfirmRun()
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(
                "\n⚠  NOTE: Running this example will consume tokens " +
                "(context and topics strategies call the LLM).");
            Console.WriteLine(
                "   Pre-generated outputs are already present in workspace/ " +
                "if you want to skip the LLM calls.");
            Console.ResetColor();
            Console.WriteLine();
            Console.Write("Continue? (yes/y): ");
            string answer = Console.ReadLine()?.Trim().ToLowerInvariant() ?? string.Empty;

            if (answer != "yes" && answer != "y")
            {
                Console.WriteLine("Aborted.");
                return false;
            }

            Console.WriteLine();
            return true;
        }

        // ----------------------------------------------------------------
        // Banner
        // ----------------------------------------------------------------

        static void PrintBanner()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("╔══════════════════════════════════════════╗");
            Console.WriteLine("║     Chunking Strategies  (Lesson 07)     ║");
            Console.WriteLine("╚══════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine();
        }
    }
}
