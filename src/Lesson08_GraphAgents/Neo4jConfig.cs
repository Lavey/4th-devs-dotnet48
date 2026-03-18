using System.Configuration;

namespace FourthDevs.Lesson08_GraphAgents
{
    /// <summary>
    /// Neo4j connection settings, read from App.config.
    /// Mirrors 02_03_graph_agents/src/config.js (i-am-alice/4th-devs)
    /// </summary>
    internal static class Neo4jConfig
    {
        internal static readonly string Uri      = Get("NEO4J_URI")      ?? "bolt://localhost:7687";
        internal static readonly string Username = Get("NEO4J_USERNAME") ?? "neo4j";
        internal static readonly string Password = Get("NEO4J_PASSWORD") ?? "password";

        private static string Get(string key) =>
            ConfigurationManager.AppSettings[key]?.Trim();
    }
}
