using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FourthDevs.Common;
using FourthDevs.Common.Models;

namespace FourthDevs.Lesson01_Interaction
{
    /// <summary>
    /// Lesson 01 – Interaction
    /// Demonstrates multi-turn conversation by passing the full message history
    /// back to the Responses API on each subsequent call.
    ///
    /// Source: 01_01_interaction/app.js (i-am-alice/4th-devs)
    /// </summary>
    internal static class Program
    {
        private const string Model = "gpt-4.1-mini";

        static void Main(string[] args)
        {
            MainAsync().GetAwaiter().GetResult();
        }

        static async Task MainAsync()
        {
            using (var client = new ResponsesApiClient())
            {
                // ----------------------------------------------------------------
                // Turn 1
                // ----------------------------------------------------------------
                string firstQuestion = "What is 25 * 48?";
                var firstResponse = await Chat(client, firstQuestion);

                // ----------------------------------------------------------------
                // Turn 2 – include the previous exchange as history
                // ----------------------------------------------------------------
                string secondQuestion = "Divide that by 4.";
                var history = new List<InputMessage>
                {
                    new InputMessage { Role = "user",      Content = firstQuestion },
                    new InputMessage { Role = "assistant", Content = firstResponse.Text }
                };
                var secondResponse = await Chat(client, secondQuestion, history);

                // ----------------------------------------------------------------
                // Print results
                // ----------------------------------------------------------------
                Console.WriteLine($"Q: {firstQuestion}");
                Console.WriteLine($"A: {firstResponse.Text}  ({firstResponse.ReasoningTokens} reasoning tokens)");
                Console.WriteLine();
                Console.WriteLine($"Q: {secondQuestion}");
                Console.WriteLine($"A: {secondResponse.Text}  ({secondResponse.ReasoningTokens} reasoning tokens)");
            }
        }

        private static async Task<ChatResult> Chat(
            ResponsesApiClient client,
            string userInput,
            List<InputMessage> history = null)
        {
            var input = new List<InputMessage>();
            if (history != null) input.AddRange(history);
            input.Add(new InputMessage { Role = "user", Content = userInput });

            var request = new ResponsesRequest
            {
                Model     = AiConfig.ResolveModel(Model),
                Input     = input,
                Reasoning = new ReasoningOptions { Effort = "medium" }
            };

            var response = await client.SendAsync(request);

            return new ChatResult
            {
                Text            = ResponsesApiClient.ExtractText(response),
                ReasoningTokens = response.Usage?.OutputTokensDetails?.ReasoningTokens ?? 0
            };
        }

        private sealed class ChatResult
        {
            public string Text            { get; set; }
            public int    ReasoningTokens { get; set; }
        }
    }
}
