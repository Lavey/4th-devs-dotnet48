using System.Collections.Generic;
using System.Linq;
using FourthDevs.Email.Models;

namespace FourthDevs.Email.Tools
{
    /// <summary>
    /// Aggregates all tool definitions into a single list and lookup map.
    /// </summary>
    public static class ToolRegistry
    {
        private static readonly List<ToolDef> _allTools;
        private static readonly Dictionary<string, ToolDef> _toolMap;

        static ToolRegistry()
        {
            _allTools = new List<ToolDef>();
            _allTools.AddRange(EmailTools.GetTools());
            _allTools.AddRange(LabelTools.GetTools());
            _allTools.AddRange(KnowledgeTools.GetTools());

            _toolMap = _allTools.ToDictionary(t => t.Name, t => t);
        }

        public static List<ToolDef> AllTools => _allTools;

        public static Dictionary<string, ToolDef> ToolMap => _toolMap;
    }
}
