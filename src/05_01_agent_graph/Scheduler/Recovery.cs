using System;
using System.Collections.Generic;
using System.Globalization;
using FourthDevs.AgentGraph.Models;

namespace FourthDevs.AgentGraph.Scheduler
{
    public class RecoverableActorError : Exception
    {
        public int RetryAfterMs { get; private set; }
        public RecoverableActorError(string message, int retryAfterMs) : base(message)
        {
            RetryAfterMs = retryAfterMs;
        }
    }

    public static class Recovery
    {
        public const int MaxLlmCallAttempts = 3;
        public const int MaxAutoRetryAttempts = 3;
        private const int BaseRetryDelayMs = 1500;
        private const int MaxRetryDelayMs = 15000;

        public static int ComputeRetryDelayMs(int attempt)
        {
            return Math.Min(MaxRetryDelayMs, BaseRetryDelayMs * (1 << Math.Max(0, attempt - 1)));
        }

        public static string FormatError(Exception error)
        {
            if (error == null) return "Unknown error";
            return error.Message ?? error.ToString() ?? "Unknown error";
        }

        public static bool IsTransientLlmError(Exception error)
        {
            var msg = (error != null && error.Message != null ? error.Message : "").ToLowerInvariant();
            return msg.Contains("timeout") || msg.Contains("temporarily unavailable")
                || msg.Contains("connection reset") || msg.Contains("network")
                || msg.Contains("rate limit") || msg.Contains("overloaded")
                || msg.Contains("429") || msg.Contains("500") || msg.Contains("502")
                || msg.Contains("503") || msg.Contains("504");
        }

        public static bool ShouldAutoRetryTask(AgentTask task, long referenceTimeMs = 0)
        {
            if (referenceTimeMs == 0) referenceTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (task.Status != "blocked") return false;
            var recovery = task.Recovery;
            if (recovery == null || !recovery.AutoRetry) return false;
            if (recovery.Attempts > MaxAutoRetryAttempts) return false;
            if (string.IsNullOrEmpty(recovery.NextRetryAt)) return true;

            DateTime nextRetry;
            if (!DateTime.TryParse(recovery.NextRetryAt, null, DateTimeStyles.RoundtripKind, out nextRetry))
                return true;
            return new DateTimeOffset(nextRetry).ToUnixTimeMilliseconds() <= referenceTimeMs;
        }
    }
}
