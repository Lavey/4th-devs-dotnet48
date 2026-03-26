using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace FourthDevs.ContextAgent.Memory
{
    /// <summary>
    /// Builds the context (systemPrompt + trimmed messages) for the agent,
    /// injecting active observations into the system prompt when available.
    /// </summary>
    internal static class MemoryContextBuilder
    {
        private const string ObservationAppendix =
            "\n\nThe following observations are your memory of past conversations with this user.\n\n" +
            "<observations>\n{0}\n</observations>\n\n" +
            "IMPORTANT: Reference specific details from these observations when relevant.\n" +
            "When observations conflict, prefer the most recent one.";

        private const string ContinuationHint =
            "<system-reminder>\n" +
            "Conversation history was compressed into memory observations.\n" +
            "Continue naturally. Do not mention memory mechanics.\n" +
            "</system-reminder>";

        public static MemoryContext Build(string baseSystemPrompt, Session session)
        {
            var memory = session.Memory;
            var allMessages = session.Messages;

            string systemPrompt = baseSystemPrompt;
            if (!string.IsNullOrEmpty(memory.ActiveObservations))
            {
                systemPrompt += string.Format(ObservationAppendix, memory.ActiveObservations);
            }

            // Messages from lastObservedIndex onward
            var activeMessages = new List<JObject>();

            // If all messages are sealed (lastObservedIndex == count), inject hint
            if (memory.LastObservedIndex > 0 && memory.LastObservedIndex >= allMessages.Count)
            {
                var hint = new JObject
                {
                    ["type"] = "message",
                    ["role"] = "user",
                    ["content"] = ContinuationHint
                };
                activeMessages.Add(hint);
            }
            else
            {
                for (int i = memory.LastObservedIndex; i < allMessages.Count; i++)
                    activeMessages.Add(allMessages[i]);
            }

            return new MemoryContext
            {
                SystemPrompt = systemPrompt,
                Messages = activeMessages
            };
        }
    }
}
