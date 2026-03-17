using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Aype.AI.Common.Models.Enums
{
    /// <summary>
    /// Type of an input message sent to the model.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum MessageType
    {
        [EnumMember(Value = "message")]
        Message,

        [EnumMember(Value = "function_call")]
        FunctionCall,

        [EnumMember(Value = "function_call_output")]
        FunctionCallOutput
    }
}
