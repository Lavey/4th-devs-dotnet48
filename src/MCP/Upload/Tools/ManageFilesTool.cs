using System;
using FourthDevs.Mcp.Upload.Adapters;
using FourthDevs.Mcp.Upload.Models;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Mcp.Upload.Tools
{
    internal class ManageFilesTool
    {
        private readonly UploadThingClient _client;

        public ManageFilesTool(UploadThingClient client)
        {
            _client = client;
        }

        public JObject GetToolDefinition()
        {
            return JObject.Parse(@"{
                ""name"": ""manage_files"",
                ""description"": ""Rename or delete files in UploadThing"",
                ""inputSchema"": {
                    ""type"": ""object"",
                    ""properties"": {
                        ""action"": {
                            ""type"": ""string"",
                            ""enum"": [""rename"", ""delete""],
                            ""description"": ""Action to perform""
                        },
                        ""fileKeys"": {
                            ""type"": ""array"",
                            ""items"": {""type"": ""string""},
                            ""description"": ""File keys to operate on""
                        },
                        ""newName"": {
                            ""type"": ""string"",
                            ""description"": ""New name (for rename, single file only)""
                        }
                    },
                    ""required"": [""action"", ""fileKeys""]
                }
            }");
        }

        public string Execute(JObject args)
        {
            string action = (string)args["action"];
            var fileKeysArr = args["fileKeys"] as JArray;

            if (string.IsNullOrWhiteSpace(action))
                return "Error: 'action' parameter is required.";
            if (fileKeysArr == null || fileKeysArr.Count == 0)
                return "Error: 'fileKeys' parameter is required and must not be empty.";

            var fileKeys = new string[fileKeysArr.Count];
            for (int i = 0; i < fileKeysArr.Count; i++)
                fileKeys[i] = (string)fileKeysArr[i];

            try
            {
                switch (action)
                {
                    case "rename":
                    {
                        string newName = (string)args["newName"];
                        if (string.IsNullOrWhiteSpace(newName))
                            return "Error: 'newName' is required for rename action.";
                        if (fileKeys.Length != 1)
                            return "Error: rename only supports a single file at a time.";

                        var renames = new[] { new RenameRequest { FileKey = fileKeys[0], NewName = newName } };
                        bool ok = _client.RenameFilesAsync(renames).GetAwaiter().GetResult();
                        return ok
                            ? $"File '{fileKeys[0]}' renamed to '{newName}'."
                            : "Rename failed.";
                    }
                    case "delete":
                    {
                        bool ok = _client.DeleteFilesAsync(fileKeys).GetAwaiter().GetResult();
                        return ok
                            ? $"Deleted {fileKeys.Length} file(s): {string.Join(", ", fileKeys)}"
                            : "Delete failed.";
                    }
                    default:
                        return $"Error: Unknown action '{action}'. Use 'rename' or 'delete'.";
                }
            }
            catch (Exception ex)
            {
                return "Error managing files: " + ex.Message;
            }
        }
    }
}
