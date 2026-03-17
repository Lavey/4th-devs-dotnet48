using System;
using System.Data.SQLite;
using System.IO;
using System.Threading.Tasks;
using FourthDevs.Lesson07_HybridRag.Db;

namespace FourthDevs.Lesson07_HybridRag
{
    /// <summary>
    /// Lesson 07 – Hybrid RAG
    ///
    /// Indexes workspace text files into SQLite (FTS5 + in-memory vector search),
    /// then runs an interactive agent that searches via hybrid retrieval.
    ///
    /// The agent has a single 'search' tool that combines:
    ///   - FTS5 full-text BM25 keyword search
    ///   - Cosine vector similarity (text-embedding-3-small)
    ///   - Reciprocal Rank Fusion (RRF) merging
    ///
    /// REPL commands: 'exit' | 'clear' | 'reindex'
    ///
    /// Source: 02_02_hybrid_rag/app.js (i-am-alice/4th-devs)
    /// </summary>
    internal static class Program
    {
        private static readonly string WorkspaceRoot =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "workspace");

        static void Main(string[] args)
        {
            MainAsync().GetAwaiter().GetResult();
        }

        static async Task MainAsync()
        {
            PrintBanner();

            Directory.CreateDirectory(WorkspaceRoot);

            // Initialize database
            Console.Write("Initializing database... ");
            SQLiteConnection db = Database.Open();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("ready");
            Console.ResetColor();

            try
            {
                // Index workspace
                Console.Write("Indexing workspace... ");
                Console.WriteLine();
                await Indexer.IndexWorkspaceAsync(db, WorkspaceRoot);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Indexing complete\n");
                Console.ResetColor();

                // Run REPL
                await Repl.RunAsync(db, WorkspaceRoot);
            }
            finally
            {
                db.Close();
                db.Dispose();
            }
        }

        // ----------------------------------------------------------------
        // Banner
        // ----------------------------------------------------------------

        static void PrintBanner()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("╔══════════════════════════════════════════╗");
            Console.WriteLine("║       Hybrid RAG Agent  (Lesson 07)      ║");
            Console.WriteLine("║  Commands: 'exit' | 'clear' | 'reindex'  ║");
            Console.WriteLine("╚══════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("Workspace: " + WorkspaceRoot);
            Console.WriteLine(
                "Place .md or .txt documents in workspace/ to make them searchable.\n");
        }
    }
}
