using Aype.AI.Common.Models.Enums;
using Newtonsoft.Json;

namespace Aype.AI.Common.Models
{
    // -------------------------------------------------------------------------
    // InputMessage hierarchy
    //
    // Base class and concrete child classes for each role/type combination.
    // Child classes pre-fill Type and Role so callers never touch raw strings.
    // -------------------------------------------------------------------------

    /// <summary>
    /// Base class for all messages sent in the <c>input</c> array of a Responses API request.
    /// </summary>
    public abstract class InputMessage
    {
        [JsonProperty("type")]
        public MessageType Type { get; protected set; }

        [JsonProperty("role", NullValueHandling = NullValueHandling.Ignore)]
        public MessageRole? Role { get; protected set; }

        [JsonProperty("content", NullValueHandling = NullValueHandling.Ignore)]
        public object Content { get; set; }
    }

    // ---- Standard conversation messages ------------------------------------

    /// <summary>
    /// A message from the user (<c>type=message, role=user</c>).
    /// </summary>
    public sealed class UserMessage : InputMessage
    {
        public UserMessage()
        {
            Type = MessageType.Message;
            Role = MessageRole.User;
        }

        public UserMessage(object content) : this()
        {
            Content = content;
        }
    }

    /// <summary>
    /// A message from the assistant (<c>type=message, role=assistant</c>).
    /// </summary>
    public sealed class AssistantMessage : InputMessage
    {
        public AssistantMessage()
        {
            Type = MessageType.Message;
            Role = MessageRole.Assistant;
        }

        public AssistantMessage(object content) : this()
        {
            Content = content;
        }
    }

    /// <summary>
    /// A system-level instruction (<c>type=message, role=system</c>).
    /// </summary>
    public sealed class SystemMessage : InputMessage
    {
        public SystemMessage()
        {
            Type = MessageType.Message;
            Role = MessageRole.System;
        }

        public SystemMessage(object content) : this()
        {
            Content = content;
        }
    }

    // ---- Tool-related messages ---------------------------------------------

    /// <summary>
    /// A function-call item appended back into the conversation after the model
    /// requests a tool call (<c>type=function_call</c>).
    /// </summary>
    public sealed class FunctionCallMessage : InputMessage
    {
        [JsonProperty("call_id")]
        public string CallId { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("arguments")]
        public string Arguments { get; set; }

        public FunctionCallMessage()
        {
            Type = MessageType.FunctionCall;
        }

        public FunctionCallMessage(string callId, string name, string arguments) : this()
        {
            CallId    = callId;
            Name      = name;
            Arguments = arguments;
        }
    }

    /// <summary>
    /// The result of executing a tool, sent back to the model
    /// (<c>type=function_call_output</c>).
    /// </summary>
    public sealed class ToolMessage : InputMessage
    {
        [JsonProperty("call_id")]
        public string CallId { get; set; }

        [JsonProperty("output")]
        public string Output { get; set; }

        public ToolMessage()
        {
            Type = MessageType.FunctionCallOutput;
        }

        public ToolMessage(string callId, string output) : this()
        {
            CallId = callId;
            Output = output;
        }
    }
}
