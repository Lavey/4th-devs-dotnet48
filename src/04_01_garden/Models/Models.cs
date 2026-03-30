using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Garden.Models
{
    internal class AgentResult
    {
        public string Text { get; set; }
        public int Turns { get; set; }
        public int TotalTokens { get; set; }
    }

    internal class AgentTemplate
    {
        public string Name { get; set; }
        public string Model { get; set; } = "gpt-4.1";
        public List<string> Tools { get; set; } = new List<string>();
        public string Instructions { get; set; } = string.Empty;
        public List<SkillTemplate> Skills { get; set; } = new List<SkillTemplate>();
    }

    internal class SkillTemplate
    {
        public string Name { get; set; }
        public string Description { get; set; } = string.Empty;
        public string RelativePath { get; set; } = string.Empty;
        public List<string> RuntimeScripts { get; set; } = new List<string>();
        public bool DisableModelInvocation { get; set; }
        public bool UserInvocable { get; set; } = true;
        public string ArgumentHint { get; set; }
        public List<string> AllowedTools { get; set; } = new List<string>();
        public string Instructions { get; set; } = string.Empty;
    }

    internal class ToolExecutionResult
    {
        public bool Ok { get; set; }
        public string Output { get; set; }

        public ToolExecutionResult(bool ok, string output)
        {
            Ok = ok;
            Output = output;
        }
    }

    internal class LocalToolDefinition
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public JObject Parameters { get; set; }
        public ToolHandlerDelegate Handler { get; set; }
    }

    internal delegate System.Threading.Tasks.Task<ToolExecutionResult> ToolHandlerDelegate(JObject args);
}
