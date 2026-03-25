using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using FourthDevs.Language.Agent;
using Newtonsoft.Json;

namespace FourthDevs.Language
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            MainAsync().GetAwaiter().GetResult();
        }

        private static async Task MainAsync()
        {
            string workspaceDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "workspace");

            // Ensure workspace directories exist
            Directory.CreateDirectory(Path.Combine(workspaceDir, "input"));
            Directory.CreateDirectory(Path.Combine(workspaceDir, "output"));
            Directory.CreateDirectory(Path.Combine(workspaceDir, "sessions"));

            // Create default profile if not present
            string profilePath = Path.Combine(workspaceDir, "profile.json");
            if (!File.Exists(profilePath))
            {
                var defaultProfile = new
                {
                    role = "software engineer",
                    goals = new[]
                    {
                        "Speak clearly in daily standups",
                        "Explain technical decisions with confidence",
                        "Reduce filler words and hesitation"
                    },
                    weakAreas = new string[0]
                };
                File.WriteAllText(profilePath,
                    JsonConvert.SerializeObject(defaultProfile, Formatting.Indented),
                    Encoding.UTF8);
            }

            PrintBanner();

            string lastResponseId = null;

            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("You: ");
                Console.ResetColor();

                string userInput = Console.ReadLine();
                if (userInput == null) break;

                userInput = userInput.Trim();
                if (userInput.Length == 0) continue;

                if (string.Equals(userInput, "quit", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(userInput, "exit", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Goodbye!");
                    break;
                }

                Console.WriteLine();

                try
                {
                    AgentRunResult result = await AgentRunner.RunAsync(userInput, workspaceDir, lastResponseId);
                    lastResponseId = result.ResponseId;

                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine("Coach:");
                    Console.ResetColor();
                    Console.WriteLine(result.Text);
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Error: {ex.Message}");
                    Console.ResetColor();
                }

                Console.WriteLine();
            }
        }

        private static void PrintBanner()
        {
            Console.WriteLine("────────────────────────────────────────────────────────");
            Console.WriteLine("English Coach — analiza wymowy i gramatyki");
            Console.WriteLine("────────────────────────────────────────────────────────");
            Console.WriteLine();
            Console.WriteLine("Agent odsłuchuje nagranie (Gemini ASR), analizuje wymowę");
            Console.WriteLine("i gramatykę, a następnie generuje feedback w formie");
            Console.WriteLine("tekstu i audio (Gemini TTS). Postępy są zapisywane");
            Console.WriteLine("w profilu (workspace/profile.json) i sesjach.");
            Console.WriteLine();
            Console.WriteLine("Przykładowe pliki audio są w workspace/input/.");
            Console.WriteLine();
            Console.WriteLine("Przykłady:");
            Console.WriteLine("  Please give me feedback on input/example-day-1.wav");
            Console.WriteLine("  Listen to input/example-day-1.wav and review my pronunciation");
            Console.WriteLine();
            Console.WriteLine("Wpisz \"quit\" aby zakończyć.");
            Console.WriteLine("────────────────────────────────────────────────────────");
            Console.WriteLine();
        }
    }
}
