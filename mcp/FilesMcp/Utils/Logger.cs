using System;

namespace FourthDevs.FilesMcp.Utils
{
    internal enum LogLevel
    {
        Debug = 0,
        Info = 1,
        Warning = 2,
        Error = 3
    }

    internal static class Logger
    {
        private static LogLevel _minLevel = LogLevel.Info;

        public static void Initialize(string levelName)
        {
            switch (levelName?.ToLowerInvariant())
            {
                case "debug":   _minLevel = LogLevel.Debug;   break;
                case "info":    _minLevel = LogLevel.Info;    break;
                case "warning":
                case "warn":    _minLevel = LogLevel.Warning; break;
                case "error":   _minLevel = LogLevel.Error;   break;
                default:        _minLevel = LogLevel.Info;    break;
            }
        }

        public static void Debug(string message)   => Log(LogLevel.Debug,   message);
        public static void Info(string message)    => Log(LogLevel.Info,    message);
        public static void Warning(string message) => Log(LogLevel.Warning, message);
        public static void Error(string message)   => Log(LogLevel.Error,   message);

        private static void Log(LogLevel level, string message)
        {
            if (level < _minLevel) return;

            string label;
            switch (level)
            {
                case LogLevel.Debug:   label = "DEBUG";   break;
                case LogLevel.Info:    label = "INFO";    break;
                case LogLevel.Warning: label = "WARNING"; break;
                case LogLevel.Error:   label = "ERROR";   break;
                default:               label = "LOG";     break;
            }
            Console.Error.WriteLine($"[{label}] {message}");
        }
    }
}
