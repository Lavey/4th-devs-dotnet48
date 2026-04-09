using System;
using System.Collections.Concurrent;
using System.Configuration;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FourthDevs.Common;
using FourthDevs.MultiAgentApi.Agent;
using FourthDevs.MultiAgentApi.Auth;
using FourthDevs.MultiAgentApi.Db;
using FourthDevs.MultiAgentApi.Models;
using FourthDevs.MultiAgentApi.Server;

namespace FourthDevs.MultiAgentApi
{
    /// <summary>
    /// 05_04_api — Multi-agent API server with SQLite, multiple AI providers,
    /// threads, sessions, runs, memory, event streaming, and authentication.
    ///
    /// Port of: i-am-alice/4th-devs/05_04_api (TypeScript/Bun → .NET 4.8 + HttpListener)
    /// </summary>
    internal static class Program
    {
        static void Main(string[] args)
        {
            MainAsync().GetAwaiter().GetResult();
        }

        static async Task MainAsync()
        {
            // Read configuration
            string host = Cfg("HOST") ?? "127.0.0.1";
            int port = 3000;
            int parsedPort;
            if (int.TryParse(Cfg("PORT"), out parsedPort))
                port = parsedPort;

            string dbPath = Cfg("DATABASE_PATH") ?? "var/05_04_api.sqlite";
            string authMode = Cfg("AUTH_MODE") ?? "dev_headers";
            string corsOrigins = Cfg("CORS_ALLOW_ORIGINS") ?? "*";

            // Resolve database path relative to exe
            if (!Path.IsPathRooted(dbPath))
                dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, dbPath);

            Console.WriteLine("[05_04_api] Initializing database at {0}", dbPath);

            using (var db = new DatabaseManager(dbPath))
            {
                db.SeedIfEmpty();

                var authManager = new AuthManager(db, authMode);
                var memory = new MemoryManager(db);
                var eventQueue = new ConcurrentQueue<DomainEvent>();

                using (var apiClient = new ResponsesApiClient())
                {
                    var runner = new AgentRunner(db, memory, apiClient, evt => eventQueue.Enqueue(evt));
                    var routes = new RouteHandler(db, authManager, runner, memory, eventQueue);

                    using (var server = new ApiServer(host, port, routes, corsOrigins))
                    {
                        server.Start();

                        string url = string.Format("http://{0}:{1}", host, port);
                        Console.WriteLine("[05_04_api] Multi-agent API server running at {0}", url);
                        Console.WriteLine("[05_04_api] Auth mode: {0}", authMode);
                        Console.WriteLine("[05_04_api] AI provider: {0}", AiConfig.Provider);
                        Console.WriteLine("[05_04_api] Press Ctrl+C to stop");

                        var exit = new ManualResetEventSlim(false);
                        Console.CancelKeyPress += delegate(object s, ConsoleCancelEventArgs e)
                        {
                            e.Cancel = true;
                            exit.Set();
                        };

                        exit.Wait();
                        Console.WriteLine("[05_04_api] Shutting down...");
                    }
                }
            }
        }

        private static string Cfg(string key)
        {
            return ConfigurationManager.AppSettings[key]?.Trim();
        }
    }
}
