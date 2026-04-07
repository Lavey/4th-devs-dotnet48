using FourthDevs.ChatUi.Data;
using Newtonsoft.Json.Linq;

namespace FourthDevs.ChatUi.Tools
{
    internal static class NotesTool
    {
        public static JObject SearchNotesDef()
        {
            return new JObject
            {
                ["type"] = "function",
                ["name"] = "search_notes",
                ["description"] = "Search through the user's notes by keyword or topic.",
                ["parameters"] = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["query"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "Search query"
                        }
                    },
                    ["required"] = new JArray("query")
                }
            };
        }

        public static ToolResult SearchNotes(JObject args)
        {
            string query = args["query"]?.ToString() ?? "";
            return new ToolResult
            {
                Ok = true,
                Output = new JObject
                {
                    ["query"] = query,
                    ["results"] = MockData.NoteSnippets
                }
            };
        }
    }
}
