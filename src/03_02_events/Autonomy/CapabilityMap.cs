using System.Collections.Generic;
using FourthDevs.Events.Helpers;

namespace FourthDevs.Events.Autonomy
{
    /// <summary>
    /// Builds a capability map from agent templates.
    /// </summary>
    internal static class CapabilityMap
    {
        public static Dictionary<string, List<string>> Build(string[] agentNames)
        {
            var result = new Dictionary<string, List<string>>();
            foreach (string name in agentNames)
            {
                try
                {
                    var template = AgentTemplateHelper.LoadFromWorkspace(name);
                    result[name] = template.Capabilities ?? new List<string>();
                }
                catch
                {
                    result[name] = new List<string>();
                }
            }
            return result;
        }
    }
}
