using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace FourthDevs.CodingAgent.Models
{
    /// <summary>
    /// Conversation items that match the Responses API input format.
    /// Mirrors memory.ts types from the TypeScript original.
    /// </summary>
    internal abstract class ConversationItem
    {
        [JsonProperty("type", NullValueHandling = NullValueHandling.Ignore)]
        public virtual string Type { get { return null; } }
    }

    internal sealed class TextMessage : ConversationItem
    {
        [JsonProperty("role")]
        public string Role { get; set; }

        [JsonProperty("content")]
        public string Content { get; set; }

        public override string ToString()
        {
            return string.Format("{0}: {1}", (Role ?? "").ToUpperInvariant(), Content);
        }
    }

    internal sealed class FunctionCallItem : ConversationItem
    {
        [JsonProperty("type")]
        public override string Type { get { return "function_call"; } }

        [JsonProperty("call_id")]
        public string CallId { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("arguments")]
        public string Arguments { get; set; }

        public override string ToString()
        {
            return string.Format("TOOL CALL {0}: {1}", Name, Arguments);
        }
    }

    internal sealed class FunctionCallOutputItem : ConversationItem
    {
        [JsonProperty("type")]
        public override string Type { get { return "function_call_output"; } }

        [JsonProperty("call_id")]
        public string CallId { get; set; }

        [JsonProperty("output")]
        public string Output { get; set; }

        public override string ToString()
        {
            return string.Format("TOOL RESULT {0}: {1}", CallId, Output);
        }
    }

    /// <summary>
    /// Holds state for a single agent conversation session.
    /// </summary>
    internal sealed class Session
    {
        public string Id { get; set; }
        public string Summary { get; set; }
        public List<ConversationItem> Messages { get; set; }

        public Session(string id)
        {
            Id = id;
            Summary = string.Empty;
            Messages = new List<ConversationItem>();
        }

        public void AddUserMessage(string content)
        {
            Messages.Add(new TextMessage { Role = "user", Content = content });
        }

        public void AddAssistantMessage(string content)
        {
            Messages.Add(new TextMessage { Role = "assistant", Content = content });
        }

        public void AddToolCall(string callId, string name, string arguments)
        {
            Messages.Add(new FunctionCallItem
            {
                CallId = callId,
                Name = name,
                Arguments = arguments
            });
        }

        public void AddToolResult(string callId, string output)
        {
            Messages.Add(new FunctionCallOutputItem
            {
                CallId = callId,
                Output = output
            });
        }

        /// <summary>
        /// Serializes a list of messages into a human-readable string for summarization.
        /// </summary>
        public static string SerializeMessages(IEnumerable<ConversationItem> messages)
        {
            int index = 0;
            var parts = new List<string>();
            foreach (var msg in messages)
            {
                index++;
                parts.Add(string.Format("{0}. {1}", index, msg.ToString()));
            }
            return string.Join("\n\n", parts.ToArray());
        }
    }
}
