using System;
using FourthDevs.UploadThingMcp.Config;

namespace FourthDevs.UploadThingMcp
{
    internal static class Program
    {
        static void Main(string[] args)
        {
            var config = EnvironmentConfig.Load();

            Console.WriteLine("UploadThing MCP Server");
            Console.WriteLine("Listening on http://{0}:{1}/", config.Host, config.Port);
            Console.WriteLine("Press Ctrl+C to stop.");

            using (var server = new Http.HttpServerHost(config))
            {
                server.Start();
                Console.CancelKeyPress += (s, e) => { e.Cancel = true; server.Stop(); };
                server.WaitForStop();
            }
        }
    }
}
