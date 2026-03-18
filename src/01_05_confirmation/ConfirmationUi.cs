using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Lesson05_Confirmation
{
    /// <summary>
    /// Handles the human-in-the-loop (HITL) confirmation UI.
    /// Mirrors the confirmation flow in 01_05_confirmation/src/repl.js
    /// in the source repo.
    /// </summary>
    internal static class ConfirmationUi
    {
        // Tools that pause for user confirmation before execution.
        private static readonly HashSet<string> ConfirmationRequired =
            new HashSet<string> { "send_email" };

        // Tools trusted for the current session (skip confirmation).
        private static readonly HashSet<string> TrustedTools =
            new HashSet<string>();

        // ----------------------------------------------------------------
        // Public API
        // ----------------------------------------------------------------

        /// <summary>
        /// Clears all trusted tools (called by the 'untrust' REPL command).
        /// </summary>
        internal static void ClearTrustedTools()
        {
            TrustedTools.Clear();
        }

        /// <summary>
        /// Decides whether to run a tool, potentially asking the user for
        /// confirmation when the tool is in the confirmation-required set.
        /// </summary>
        internal static Task<bool> ShouldRunTool(string toolName, JObject args)
        {
            if (!ConfirmationRequired.Contains(toolName))
                return Task.FromResult(true);

            if (TrustedTools.Contains(toolName))
            {
                ColorLine(string.Format("  ⚡ Auto-approved (trusted): {0}", toolName),
                    ConsoleColor.Blue);
                return Task.FromResult(true);
            }

            if (toolName == "send_email")
                PrintEmailConfirmation(args);
            else
                PrintGenericConfirmation(toolName, args);

            Console.Write("  Your choice (Y/N/T=trust): ");
            string answer = (Console.ReadLine() ?? string.Empty).Trim().ToLowerInvariant();
            Console.WriteLine();

            if (answer == "t" || answer == "trust")
            {
                TrustedTools.Add(toolName);
                ColorLine(string.Format("  ✓ Trusted \"{0}\" for this session", toolName),
                    ConsoleColor.Blue);
                return Task.FromResult(true);
            }

            if (answer == "y" || answer == "yes")
                return Task.FromResult(true);

            ColorLine("  ✗ Action cancelled", ConsoleColor.Red);
            return Task.FromResult(false);
        }

        // ----------------------------------------------------------------
        // Confirmation display helpers
        // ----------------------------------------------------------------

        private static void PrintEmailConfirmation(JObject args)
        {
            string to      = FormatTo(args["to"]);
            string subject = args["subject"]?.ToString() ?? "(no subject)";
            string body    = args["body"]?.ToString()    ?? "(empty)";
            string format  = args["format"]?.ToString()  ?? "text";
            string replyTo = args["reply_to"]?.ToString();

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("  ┌──────────────────────────────────────────────────────────────┐");
            Console.WriteLine("  │  📧 EMAIL CONFIRMATION REQUIRED                              │");
            Console.WriteLine("  ├──────────────────────────────────────────────────────────────┤");
            Console.ResetColor();
            Console.WriteLine(string.Format("  │  To:      {0}", to.Length > 50 ? to.Substring(0, 50) : to));
            Console.WriteLine(string.Format("  │  Subject: {0}", subject.Length > 50 ? subject.Substring(0, 50) : subject));
            Console.WriteLine(string.Format("  │  Format:  {0}", format));
            if (!string.IsNullOrEmpty(replyTo))
                Console.WriteLine(string.Format("  │  Reply-To:{0}", replyTo));
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("  ├──────────────────────────────────────────────────────────────┤");
            Console.ResetColor();
            Console.WriteLine("  │  Body:");
            foreach (string line in body.Split('\n'))
                Console.WriteLine("  │    " + line);
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("  └──────────────────────────────────────────────────────────────┘");
            Console.ResetColor();
            Console.WriteLine();
        }

        private static void PrintGenericConfirmation(string toolName, JObject args)
        {
            Console.WriteLine();
            ColorLine("  ⚠  Action requires confirmation", ConsoleColor.Yellow);
            Console.WriteLine("     Tool: " + toolName);
            Console.WriteLine("     Args: " + args.ToString(Formatting.Indented));
            Console.WriteLine();
        }

        private static string FormatTo(JToken token)
        {
            if (token == null) return "(none)";
            if (token.Type == JTokenType.Array)
                return string.Join(", ", token.Select(t => t.ToString()));
            return token.ToString();
        }

        // ----------------------------------------------------------------
        // Console helper
        // ----------------------------------------------------------------

        private static void ColorLine(string text, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(text);
            Console.ResetColor();
        }
    }
}
