using System;

namespace FourthDevs.Events.Helpers
{
    /// <summary>
    /// Rough token estimation (chars/4 heuristic) with calibration support.
    /// </summary>
    internal static class TokenEstimator
    {
        private const double CharsPerToken = 4.0;
        private static double _calibrationRatio = 1.0;

        public static int Estimate(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            return (int)Math.Ceiling(text.Length / CharsPerToken * _calibrationRatio);
        }

        public static void Calibrate(int estimatedTokens, int actualTokens)
        {
            if (estimatedTokens <= 0 || actualTokens <= 0) return;
            double ratio = (double)actualTokens / estimatedTokens;
            // Exponential moving average
            _calibrationRatio = _calibrationRatio * 0.8 + ratio * 0.2;
        }

        public static double CalibrationRatio
        {
            get { return _calibrationRatio; }
        }
    }
}
