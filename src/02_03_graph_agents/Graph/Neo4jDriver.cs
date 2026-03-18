using System.Collections.Generic;
using System.Threading.Tasks;
using Neo4j.Driver;
using FourthDevs.Lesson08_GraphAgents.Helpers;

namespace FourthDevs.Lesson08_GraphAgents.Graph
{
    /// <summary>
    /// Neo4j driver wrapper: creates the driver, verifies connectivity,
    /// and provides read/write transaction helpers.
    /// Mirrors 02_03_graph_agents/src/graph/driver.js (i-am-alice/4th-devs)
    /// </summary>
    internal static class Neo4jDriver
    {
        internal static IDriver CreateDriver(string uri, string username, string password)
        {
            return GraphDatabase.Driver(uri, AuthTokens.Basic(username, password));
        }

        internal static async Task<IServerInfo> VerifyConnectionAsync(IDriver driver)
        {
            var serverInfo = await driver.GetServerInfoAsync();
            Logger.Info(string.Format("Neo4j {0} at {1}",
                serverInfo.ProtocolVersion, serverInfo.Address));
            return serverInfo;
        }

        /// <summary>
        /// Runs a read transaction and returns all records.
        /// </summary>
        internal static async Task<IList<IRecord>> ReadQueryAsync(
            IDriver driver, string cypher, object parameters = null)
        {
            var session = driver.AsyncSession();
            try
            {
                return await session.ExecuteReadAsync(async tx =>
                {
                    var cursor = await tx.RunAsync(cypher, parameters ?? new { });
                    return await cursor.ToListAsync();
                });
            }
            finally
            {
                await session.CloseAsync();
            }
        }

        /// <summary>
        /// Runs a write transaction and returns all records.
        /// </summary>
        internal static async Task<IList<IRecord>> WriteQueryAsync(
            IDriver driver, string cypher, object parameters = null)
        {
            var session = driver.AsyncSession();
            try
            {
                return await session.ExecuteWriteAsync(async tx =>
                {
                    var cursor = await tx.RunAsync(cypher, parameters ?? new { });
                    return await cursor.ToListAsync();
                });
            }
            finally
            {
                await session.CloseAsync();
            }
        }

        /// <summary>
        /// Runs a write transaction that makes multiple Cypher calls.
        /// </summary>
        internal static async Task WriteTransactionAsync(
            IDriver driver,
            System.Func<IAsyncQueryRunner, Task> work)
        {
            var session = driver.AsyncSession();
            try
            {
                await session.ExecuteWriteAsync<int>(async tx =>
                {
                    await work(tx);
                    return 0;
                });
            }
            finally
            {
                await session.CloseAsync();
            }
        }
    }
}
