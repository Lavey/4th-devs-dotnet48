using System;
using System.Threading.Tasks;
using FourthDevs.Garden.Agent;
using FourthDevs.Garden.Core;
using FourthDevs.Garden.Models;

namespace FourthDevs.Garden
{
    /// <summary>
    /// Lesson 15 – Digital Garden Agent (04_01_garden)
    ///
    /// A CLI chat agent that manages a markdown-based vault / digital garden.
    /// It can create, edit, and organise notes; run JavaScript skill scripts
    /// via Jint; and push changes to Git.
    ///
    /// Source: 04_01_garden (i-am-alice/4th-devs)
    /// </summary>
    internal static class Program
    {
        static void Main(string[] args)
        {
            MainAsync().GetAwaiter().GetResult();
        }

        static async Task MainAsync()
        {
            PrintWelcome();

            while (true)
            {
                Console.WriteLine();
                Console.Write("  You: ");
                string input = Console.ReadLine();

                if (input == null)
                    break;

                string trimmed = input.Trim();
                if (trimmed.Length == 0)
                    continue;

                string lower = trimmed.ToLowerInvariant();
                if (lower == "exit" || lower == "quit")
                    break;

                try
                {
                    AgentResult result = await AgentRunner.RunAsync(trimmed);
                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine("  Agent: " + result.Text);
                    Console.ResetColor();
                    Console.WriteLine();
                    Console.WriteLine("  [tokens: " + result.TotalTokens + "]");
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine();
                    Console.WriteLine("  Error: " + ex.Message);
                    Console.ResetColor();
                }
            }
        }

        private static void PrintWelcome()
        {
            string[] lines = new string[]
            {
                "",
                "  Cyfrowy Ogr\u00f3d",
                "",
                "  Ten przyklad laczy trzy elementy:",
                "  1. publiczna baze wiedzy w Markdownie w `vault/**`",
                "  2. agenta, ktory moze tworzyc, edytowac i wzbogacac notatki",
                "  3. generator statycznej strony w `grove/`, ktory zamienia vault na HTML",
                "",
                "  Proponowane pierwsze kroki:",
                "  - Popros agenta o dodanie 3-4 ulubionych ksiazek do shelf.",
                "  - Utworz krotka notatke w lab o czyms, czego sie uczysz.",
                "  - Zapisz notatki researchowe w `vault/research/<temat>/`.",
                "",
                "  Wazne:",
                "  - Traktuj ten przyklad jako public-by-design.",
                "  - `vault/system/**` zawiera instrukcje agenta i skille.",
                "  - Wpisz `exit`, aby zamknac CLI.",
                "",
            };

            Console.WriteLine(string.Join(Environment.NewLine, lines));
        }
    }
}
