using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using FourthDevs.Events.Models;

namespace FourthDevs.Events.Core
{
    /// <summary>
    /// JSON structured logger for agent events.
    /// </summary>
    internal static class Logger
    {
        private static readonly object _lock = new object();

        public static void Info(string source, string message, object data = null)
        {
            Log("info", source, message, data);
        }

        public static void Warn(string source, string message, object data = null)
        {
            Log("warn", source, message, data);
        }

        public static void Error(string source, string message, object data = null)
        {
            Log("error", source, message, data);
        }

        public static void Debug(string source, string message, object data = null)
        {
            Log("debug", source, message, data);
        }

        public static void Event(HeartbeatEvent evt)
        {
            string json = JsonConvert.SerializeObject(evt, Formatting.None,
                new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            WriteColored(ConsoleColor.DarkCyan, "[event] " + json);
        }

        private static void Log(string level, string source, string message, object data)
        {
            ConsoleColor color;
            switch (level)
            {
                case "warn": color = ConsoleColor.Yellow; break;
                case "error": color = ConsoleColor.Red; break;
                case "debug": color = ConsoleColor.DarkGray; break;
                default: color = ConsoleColor.Gray; break;
            }

            string prefix = "[" + source + "] ";
            string line = prefix + message;

            if (data != null)
            {
                line += " " + JsonConvert.SerializeObject(data, Formatting.None,
                    new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            }

            WriteColored(color, line);
        }

        private static void WriteColored(ConsoleColor color, string text)
        {
            lock (_lock)
            {
                ConsoleColor prev = Console.ForegroundColor;
                Console.ForegroundColor = color;
                Console.WriteLine(text);
                Console.ForegroundColor = prev;
            }
        }
    }
}
