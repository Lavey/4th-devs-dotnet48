using System;
using System.Collections.Generic;
using System.Linq;
using FourthDevs.AxClassifier.Models;

namespace FourthDevs.AxClassifier.Core
{
    public static class Metric
    {
        /// <summary>
        /// Scores a prediction against an expected labeled email.
        /// Replicates the TypeScript metric exactly:
        ///   labelScore      = jaccard(pred.labels, expected.labels) * 0.5
        ///   priorityScore   = exact match * 0.25
        ///   replyScore      = exact match * 0.15
        ///   consistencyBonus = needsReply matches presence of "needs-reply" label * 0.1
        /// </summary>
        public static double Score(ClassificationResult prediction, LabeledEmail expected)
        {
            var predLabels = prediction.Labels ?? new List<string>();
            var expectedLabels = expected.Labels ?? new string[0];

            double labelScore = Jaccard(predLabels, expectedLabels);

            double priorityScore =
                string.Equals(prediction.Priority, expected.Priority,
                    StringComparison.OrdinalIgnoreCase) ? 1.0 : 0.0;

            double replyScore =
                prediction.NeedsReply == expected.NeedsReply ? 1.0 : 0.0;

            bool needsReplyLabelConsistent =
                prediction.NeedsReply == predLabels.Contains("needs-reply");
            double consistencyBonus = needsReplyLabelConsistent ? 0.1 : 0.0;

            return Math.Min(1.0,
                labelScore * 0.5 +
                priorityScore * 0.25 +
                replyScore * 0.15 +
                consistencyBonus);
        }

        private static double Jaccard(IList<string> a, IList<string> b)
        {
            var setA = new HashSet<string>(a, StringComparer.OrdinalIgnoreCase);
            var setB = new HashSet<string>(b, StringComparer.OrdinalIgnoreCase);

            int intersection = setA.Count(x => setB.Contains(x));
            var union = new HashSet<string>(setA, StringComparer.OrdinalIgnoreCase);
            foreach (var item in setB) union.Add(item);

            return union.Count == 0 ? 1.0 : (double)intersection / union.Count;
        }
    }
}
