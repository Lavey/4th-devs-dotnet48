using System;
using System.IO;
using System.Threading.Tasks;
using Neo4j.Driver;
using FourthDevs.Lesson08_GraphAgents.Graph;
using FourthDevs.Lesson08_GraphAgents.Helpers;

namespace FourthDevs.Lesson08_GraphAgents
{
    /// <summary>
    /// Lesson 08 – Graph RAG Agent
    ///
    /// Indexes workspace text files into Neo4j (full-text + vector + entity graph),
    /// then runs an interactive agent that searches via hybrid retrieval and graph exploration.
    ///
    /// Tools: search | explore | connect | cypher | learn | forget | merge_entities | audit
    /// REPL commands: 'exit' | 'clear' | 'reindex' | 'reindex --force'
    ///
    /// Source: 02_03_graph_agents/app.js (i-am-alice/4th-devs)
    /// </summary>
    internal static class Program
    {
        private static readonly string WorkspaceRoot =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "workspace");

        static void Main(string[] args)
        {
            MainAsync().GetAwaiter().GetResult();
        }

        static async Task MainAsync()
        {
            Logger.Box("Graph RAG Agent  (Lesson 08)\nCommands: 'exit' | 'clear' | 'reindex' | 'reindex --force'");

            // 1. Neo4j connection
            Logger.Start("Connecting to Neo4j...");
            IDriver driver = Neo4jDriver.CreateDriver(
                Neo4jConfig.Uri, Neo4jConfig.Username, Neo4jConfig.Password);

            try
            {
                await Neo4jDriver.VerifyConnectionAsync(driver);
                Logger.Success("Neo4j connected");

                // 2. Schema (constraints + indexes)
                Logger.Start("Ensuring graph schema...");
                await Schema.EnsureSchemaAsync(driver);

                // 3. Index workspace
                Logger.Start("Indexing workspace...");
                Directory.CreateDirectory(WorkspaceRoot);
                await Indexer.IndexWorkspaceAsync(driver, WorkspaceRoot);
                Logger.Success("Indexing complete");

                // 4. Run REPL
                await Repl.RunAsync(driver, WorkspaceRoot);
            }
            finally
            {
                Stats.LogStats();
#pragma warning disable CS0618 // CloseAsync is obsolete but DisposeAsync not available in C# 7.2
                await driver.CloseAsync();
#pragma warning restore CS0618
            }
        }
    }
}
