using System.Collections.Generic;
using System.Linq;
using FourthDevs.Events.Models;

namespace FourthDevs.Events.Tools
{
    public static class ToolRegistry
    {
        private static readonly List<Tool> AllTools = new List<Tool>
        {
            WebSearchTool.Create(),
            HumanTool.Create(),
            RenderHtmlTool.Create()
        };

        public static IReadOnlyList<Tool> GetAllTools()
        {
            return AllTools;
        }

        public static Tool FindTool(string name)
        {
            return AllTools.FirstOrDefault(t => t.Definition.Name == name);
        }
    }
}
