using System;
using System.Configuration;

namespace FourthDevs.Mcp.Upload.Config
{
    internal class EnvironmentConfig
    {
        public string UploadThingToken { get; set; }
        public int Port { get; set; }
        public string Host { get; set; }

        private EnvironmentConfig() { }

        public static EnvironmentConfig Load()
        {
            var config = new EnvironmentConfig();

            config.UploadThingToken = GetSetting("UPLOADTHING_TOKEN") ?? string.Empty;

            string portStr = GetSetting("PORT");
            config.Port = 3000;
            if (!string.IsNullOrWhiteSpace(portStr) && int.TryParse(portStr, out int port))
                config.Port = port;

            config.Host = GetSetting("HOST") ?? "127.0.0.1";

            return config;
        }

        private static string GetSetting(string key)
        {
            string value = Environment.GetEnvironmentVariable(key);
            if (!string.IsNullOrWhiteSpace(value)) return value;
            try
            {
                value = ConfigurationManager.AppSettings[key];
            }
            catch
            {
                // Ignore configuration errors
            }
            return value;
        }
    }
}
