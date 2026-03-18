using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using FourthDevs.Common;
using FourthDevs.Common.Models;

namespace FourthDevs.Lesson01_Grounding
{
    /// <summary>
    /// Lesson 01 – Grounding
    /// Reads a Markdown notes file, sends it to the model, and asks it to
    /// produce a fact-checked HTML report that embeds inline citations.
    ///
    /// Source: 01_01_grounding/app.js (i-am-alice/4th-devs)
    ///
    /// NOTE: The original pipeline used web-search grounding + concept deduplication.
    ///       This C# port implements a streamlined single-pass version:
    ///       the full notes content is sent to the model which is instructed to
    ///       annotate facts with [citation needed] markers and wrap the output in HTML.
    /// </summary>
    internal static class Program
    {
        private const string Model = "gpt-4.1-mini";

        static void Main(string[] args)
        {
            MainAsync(args).GetAwaiter().GetResult();
        }

        static async Task MainAsync(string[] args)
        {
            // Resolve the notes file (default: notes/notes.md next to the executable)
            string exeDir    = AppDomain.CurrentDomain.BaseDirectory;
            string notesFile = args.Length > 0
                ? args[0]
                : Path.Combine(exeDir, "notes", "notes.md");

            if (!File.Exists(notesFile))
            {
                Console.Error.WriteLine($"Notes file not found: {notesFile}");
                Console.Error.WriteLine("Usage: Lesson01_Grounding.exe [path/to/notes.md]");
                Environment.Exit(1);
            }

            string markdown = File.ReadAllText(notesFile, Encoding.UTF8);

            Console.WriteLine($"Source:  {notesFile}");
            Console.WriteLine("Generating grounded HTML report...");

            string html = await GenerateGroundedHtml(markdown);

            string outFile = Path.Combine(exeDir, "output", "grounded_report.html");
            Directory.CreateDirectory(Path.GetDirectoryName(outFile));
            File.WriteAllText(outFile, html, Encoding.UTF8);

            Console.WriteLine($"Output:  {outFile}");
            Console.WriteLine("Done.");
        }

        static async Task<string> GenerateGroundedHtml(string markdownContent)
        {
            using (var client = new ResponsesApiClient())
            {
                string prompt = BuildPrompt(markdownContent);

                var request = new ResponsesRequest
                {
                    Model = AiConfig.ResolveModel(Model),
                    Input = new List<InputMessage>
                    {
                        new InputMessage { Role = "user", Content = prompt }
                    }
                };

                var response = await client.SendAsync(request);
                return ResponsesApiClient.ExtractText(response);
            }
        }

        static string BuildPrompt(string markdown)
        {
            return
                "You are a fact-checking assistant. " +
                "Convert the following Markdown notes into a well-structured HTML document. " +
                "For every factual claim that can be independently verified, " +
                "add a small inline annotation like <span class=\"fact\" title=\"Verify: &lt;search query&gt;\">[?]</span>. " +
                "Use semantic HTML5 tags (article, section, h1-h3, p). " +
                "Return ONLY the complete HTML document, no Markdown fences.\n\n" +
                "---\n" +
                markdown;
        }
    }
}
