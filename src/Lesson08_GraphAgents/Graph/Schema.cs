using System.Threading.Tasks;
using Neo4j.Driver;
using FourthDevs.Lesson08_GraphAgents.Helpers;

namespace FourthDevs.Lesson08_GraphAgents.Graph
{
    /// <summary>
    /// Graph schema: constraints, indexes, and label/relationship constants.
    ///
    /// Node labels:   Document, Chunk, Entity
    /// Relationships: HAS_CHUNK, MENTIONS, RELATED_TO
    ///
    /// Mirrors 02_03_graph_agents/src/graph/schema.js (i-am-alice/4th-devs)
    /// </summary>
    internal static class Schema
    {
        internal const int EmbeddingDim = 1536; // text-embedding-3-small

        private static readonly string[] SetupStatements =
        {
            // Uniqueness constraints
            "CREATE CONSTRAINT doc_source IF NOT EXISTS " +
            "FOR (d:Document) REQUIRE d.source IS UNIQUE",

            "CREATE CONSTRAINT entity_name_type IF NOT EXISTS " +
            "FOR (e:Entity) REQUIRE (e.name, e.type) IS UNIQUE",

            // Full-text indexes
            "CREATE FULLTEXT INDEX chunk_content_ft IF NOT EXISTS " +
            "FOR (c:Chunk) ON EACH [c.content]",

            "CREATE FULLTEXT INDEX entity_name_ft IF NOT EXISTS " +
            "FOR (e:Entity) ON EACH [e.name, e.aliases_text]",

            // Vector indexes
            "CREATE VECTOR INDEX chunk_embedding_vec IF NOT EXISTS " +
            "FOR (c:Chunk) ON (c.embedding) " +
            "OPTIONS {indexConfig: {`vector.dimensions`: " + EmbeddingDim + ", " +
            "`vector.similarity_function`: 'cosine'}}",

            "CREATE VECTOR INDEX entity_embedding_vec IF NOT EXISTS " +
            "FOR (e:Entity) ON (e.embedding) " +
            "OPTIONS {indexConfig: {`vector.dimensions`: " + EmbeddingDim + ", " +
            "`vector.similarity_function`: 'cosine'}}",
        };

        internal static async Task EnsureSchemaAsync(IDriver driver)
        {
            foreach (var stmt in SetupStatements)
            {
                try
                {
                    await Neo4jDriver.WriteQueryAsync(driver, stmt);
                }
                catch (Neo4jException ex)
                {
                    if (ex.Message.Contains("equivalent index already exists")) continue;
                    Logger.Warn("Schema statement skipped: " + ex.Message.Split('\n')[0]);
                }
            }
            Logger.Success("Graph schema ready");
        }
    }

    internal static class Labels
    {
        internal const string Document = "Document";
        internal const string Chunk    = "Chunk";
        internal const string Entity   = "Entity";
    }

    internal static class Rels
    {
        internal const string HasChunk  = "HAS_CHUNK";
        internal const string Mentions  = "MENTIONS";
        internal const string RelatedTo = "RELATED_TO";
    }
}
