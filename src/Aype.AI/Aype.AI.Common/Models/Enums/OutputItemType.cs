using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Aype.AI.Common.Models.Enums
{
    /// <summary>
    /// Type of an item in the model's output.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum OutputItemType
    {
        [EnumMember(Value = "message")]
        Message,

        [EnumMember(Value = "function_call")]
        FunctionCall
    }
}
