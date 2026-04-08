using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace FourthDevs.AutoPrompt.Models
{
    public class EvalResult
    {
        public double Avg { get; set; }
        public List<CaseResult> Results { get; set; }
        public double Spread { get; set; }
        public List<SingleRunResult> Runs { get; set; }
    }

    public class SingleRunResult
    {
        public double Avg { get; set; }
        public List<CaseResult> Results { get; set; }
    }

    public class CaseResult
    {
        public string Id { get; set; }
        public double Score { get; set; }
        public Dictionary<string, SectionBreakdown> Breakdown { get; set; }
        public JObject Actual { get; set; }
        public JObject Expected { get; set; }
        public string Error { get; set; }
    }

    public class SectionBreakdown
    {
        public double Score { get; set; }
        public double Weight { get; set; }
        public List<string> Issues { get; set; }
    }
}
