using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using FourthDevs.Common;
using FourthDevs.Common.Models;

namespace FourthDevs.Lesson03_McpTranslator
{
    /// <summary>
    /// Lesson 03 – MCP Translator
    /// Translation agent that watches workspace/translate/ for text files and
    /// produces English versions in workspace/translated/.
    ///
    /// In the original JS version (01_03_mcp_translator) the agent uses an MCP
    /// file-server (files-mcp) for all filesystem operations. In this .NET 4.8
    /// port the file tools are implemented natively while the LLM translation
    /// still goes through the Responses API, demonstrating the same concept.
    ///
    /// Source: 01_03_mcp_translator/app.js (i-am-alice/4th-devs)
    /// </summary>
    internal static class Program
    {
        private const string Model = "gpt-4.1-mini";

        // Supported file extensions for translation
        private static readonly HashSet<string> SupportedExtensions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".txt", ".md", ".html", ".json", ".csv" };

        static void Main(string[] args)
        {
            MainAsync(args).GetAwaiter().GetResult();
        }

        static async Task MainAsync(string[] args)
        {
            string exeDir        = AppDomain.CurrentDomain.BaseDirectory;
            string translateDir  = Path.Combine(exeDir, "workspace", "translate");
            string translatedDir = Path.Combine(exeDir, "workspace", "translated");

            Directory.CreateDirectory(translateDir);
            Directory.CreateDirectory(translatedDir);

            Console.WriteLine("=== MCP Translator Agent ===");
            Console.WriteLine("Accurate translations to English with tone, formatting & nuances\n");
            Console.WriteLine("Source:      " + translateDir);
            Console.WriteLine("Destination: " + translatedDir);
            Console.WriteLine();

            // ----------------------------------------------------------------
            // Place a sample file if none exist (so the demo is self-running)
            // ----------------------------------------------------------------
            EnsureSampleFile(translateDir);

            // ----------------------------------------------------------------
            // Translation loop: process every file in workspace/translate/
            // ----------------------------------------------------------------
            string[] files = Directory.GetFiles(translateDir);
            if (files.Length == 0)
            {
                Console.WriteLine("No files found in workspace/translate/ — nothing to do.");
                return;
            }

            int translated = 0;
            int skipped    = 0;

            foreach (string filePath in files)
            {
                string ext = Path.GetExtension(filePath);
                if (!SupportedExtensions.Contains(ext))
                {
                    Console.WriteLine("Skipping (unsupported extension): " + Path.GetFileName(filePath));
                    skipped++;
                    continue;
                }

                string outputPath = Path.Combine(translatedDir, Path.GetFileName(filePath));
                if (File.Exists(outputPath))
                {
                    Console.WriteLine("Already translated, skipping: " + Path.GetFileName(filePath));
                    skipped++;
                    continue;
                }

                Console.WriteLine("Translating: " + Path.GetFileName(filePath) + " ...");
                string source = File.ReadAllText(filePath, Encoding.UTF8);

                try
                {
                    string result = await TranslateToEnglish(source);
                    File.WriteAllText(outputPath, result, Encoding.UTF8);
                    Console.WriteLine("  → " + Path.GetFileName(outputPath));
                    translated++;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("  Error: " + ex.Message);
                }
            }

            Console.WriteLine();
            Console.WriteLine(string.Format(
                "Done. Translated: {0}  Skipped: {1}", translated, skipped));
        }

        static async Task<string> TranslateToEnglish(string sourceText)
        {
            using (var client = new ResponsesApiClient())
            {
                var request = new ResponsesRequest
                {
                    Model = AiConfig.ResolveModel(Model),
                    Input = new List<InputMessage>
                    {
                        new InputMessage
                        {
                            Role    = "system",
                            Content =
                                "You are a professional translator. " +
                                "Translate the provided text to English, preserving the original " +
                                "tone, formatting (Markdown, HTML, etc.), and any nuances. " +
                                "Return ONLY the translated content — no preamble or explanation."
                        },
                        new InputMessage
                        {
                            Role    = "user",
                            Content = sourceText
                        }
                    }
                };

                var response = await client.SendAsync(request);
                return ResponsesApiClient.ExtractText(response);
            }
        }

        static void EnsureSampleFile(string translateDir)
        {
            string samplePath = Path.Combine(translateDir, "przyklad.md");
            if (File.Exists(samplePath)) return;

            File.WriteAllText(samplePath, SamplePolishText, Encoding.UTF8);
            Console.WriteLine("Created sample file: workspace/translate/przyklad.md\n");
        }

        private const string SamplePolishText =
@"# Protokół MCP – Wprowadzenie

**Model Context Protocol (MCP)** to ustandaryzowany protokół umożliwiający
aplikacjom dostarczanie kontekstu dla dużych modeli językowych (LLM).

## Główne możliwości

- **Narzędzia** – funkcje, które model może wywoływać
- **Zasoby**    – dane tylko do odczytu udostępniane przez serwer
- **Szablony promptów** – wielokrotnie używane szablony wiadomości z parametrami

## Dlaczego MCP?

MCP oddziela dostarczanie kontekstu od faktycznej interakcji z modelem LLM,
dzięki czemu serwery mogą być ponownie używane przez różnych klientów.
";
    }
}
