using System.Configuration;
using System.Threading.Tasks;

namespace FourthDevs.Observability.Core.Tracing
{
    /// <summary>
    /// Manages Langfuse tracing initialisation and lifecycle.
    /// On .NET 4.8 the actual Langfuse OpenTelemetry SDK is not available,
    /// so we log trace events to the structured logger instead.
    /// </summary>
    internal static class TracingManager
    {
        private static bool _initialized;
        private static bool _active;
        private static Logger _logger;

        public static bool IsActive
        {
            get { return _initialized && _active; }
        }

        /// <summary>
        /// Initialise tracing. If LANGFUSE_PUBLIC_KEY and LANGFUSE_SECRET_KEY
        /// are present in App.config, tracing is enabled.
        /// </summary>
        public static void Init(Logger logger, string serviceName = "03_01_observability")
        {
            _logger = logger;

            string publicKey = ConfigurationManager.AppSettings["LANGFUSE_PUBLIC_KEY"] ?? string.Empty;
            string secretKey = ConfigurationManager.AppSettings["LANGFUSE_SECRET_KEY"] ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(publicKey) && !string.IsNullOrWhiteSpace(secretKey))
            {
                _active = true;
                _logger.Info("Langfuse tracing enabled (structured log mode)", new System.Collections.Generic.Dictionary<string, object>
                {
                    { "service", serviceName }
                });
            }
            else
            {
                _active = false;
                _logger.Warn("Langfuse keys not configured – tracing disabled");
            }

            _initialized = true;
        }

        public static Task FlushAsync()
        {
            return Task.CompletedTask;
        }

        public static Task ShutdownAsync()
        {
            _active = false;
            return Task.CompletedTask;
        }

        public static void LogTrace(string level, string message, System.Collections.Generic.Dictionary<string, object> data = null)
        {
            if (_logger == null) return;

            switch (level)
            {
                case "debug": _logger.Debug(message, data); break;
                case "warn":  _logger.Warn(message, data);  break;
                case "error": _logger.Error(message, data);  break;
                default:      _logger.Info(message, data);   break;
            }
        }
    }
}
