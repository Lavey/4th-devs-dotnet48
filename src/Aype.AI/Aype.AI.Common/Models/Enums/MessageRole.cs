using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Aype.AI.Common.Models.Enums
{
    /// <summary>
    /// Role of a message in the conversation.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum MessageRole
    {
        [EnumMember(Value = "user")]
        User,

        [EnumMember(Value = "assistant")]
        Assistant,

        [EnumMember(Value = "system")]
        System
    }
}
