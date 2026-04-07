using System;
using System.Collections.Generic;
using FourthDevs.ChatUi.Data;
using FourthDevs.ChatUi.Models;

namespace FourthDevs.ChatUi.Mock
{
    /// <summary>
    /// Creates seed conversations and empty conversations for the mock mode.
    /// </summary>
    internal static class MockConversation
    {
        public static List<ConversationMessage> CreateSeedMessages()
        {
            var messages = new List<ConversationMessage>();

            // Add 4 seed prompt/response pairs
            for (int i = 0; i < MockData.SeedPrompts.Length; i++)
            {
                string userMsgId = "seed_u_" + i;
                string assistantMsgId = "seed_a_" + i;

                messages.Add(new ConversationMessage
                {
                    Id = userMsgId,
                    Role = MessageRole.user,
                    Status = MessageStatus.complete,
                    CreatedAt = DateTime.UtcNow.AddMinutes(-(MockData.SeedPrompts.Length - i) * 2).ToString("o"),
                    Text = MockData.SeedPrompts[i]
                });

                // Build completed events for the seed response
                var events = MockScenarios.GetScenario(i, assistantMsgId);
                var completedEvents = new List<BaseStreamEvent>();
                foreach (var de in events)
                {
                    completedEvents.Add(de.Event);
                }

                messages.Add(new ConversationMessage
                {
                    Id = assistantMsgId,
                    Role = MessageRole.assistant,
                    Status = MessageStatus.complete,
                    CreatedAt = DateTime.UtcNow.AddMinutes(-(MockData.SeedPrompts.Length - i) * 2 + 1).ToString("o"),
                    Events = completedEvents
                });
            }

            return messages;
        }

        public static List<ConversationMessage> CreateEmpty()
        {
            return new List<ConversationMessage>();
        }
    }
}
