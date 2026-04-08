using System.Collections.Generic;

namespace FourthDevs.AutoPrompt.Models
{
    public class IterationResult
    {
        public int Iteration { get; set; }
        public double Score { get; set; }
        public string Status { get; set; }
        public string Operation { get; set; }
        public string Reasoning { get; set; }
        public string CandidateLabel { get; set; }
        public string Prompt { get; set; }
        public string Diff { get; set; }
        public string DiffSummary { get; set; }
        public EvalResult Result { get; set; }
        public double Delta { get; set; }
        public double NoiseFloor { get; set; }
        public Dictionary<string, double> SectionScores { get; set; }
        public Dictionary<string, double> SectionDeltas { get; set; }
        public string SectionSummary { get; set; }
        public string SectionDeltaSummary { get; set; }
        public List<CandidateSummary> Candidates { get; set; }
    }

    public class CandidateSummary
    {
        public string CandidateLabel { get; set; }
        public string Operation { get; set; }
        public string Status { get; set; }
        public double Score { get; set; }
        public double Delta { get; set; }
        public double NoiseFloor { get; set; }
        public string DiffSummary { get; set; }
        public string SectionSummary { get; set; }
        public string SectionDeltaSummary { get; set; }
    }

    public class HistoryEntry
    {
        public string Status { get; set; }
        public string Score { get; set; }
        public string Operation { get; set; }
        public string Reasoning { get; set; }
        public string CandidateLabel { get; set; }
        public string SectionSummary { get; set; }
        public string SectionDeltaSummary { get; set; }
        public string Diff { get; set; }
    }

    public class ImproverSuggestion
    {
        public string Reasoning { get; set; }
        public string Operation { get; set; }
        public string Prompt { get; set; }
        public string CandidateLabel { get; set; }
        public string CandidateHint { get; set; }
    }

    public class OptimizeRun
    {
        public string ProjectName { get; set; }
        public EvalResult Baseline { get; set; }
        public string BestPrompt { get; set; }
        public double BestScore { get; set; }
        public ResolvedModels Models { get; set; }
        public List<IterationResult> Iterations { get; set; }
        public int EvalRuns { get; set; }
        public int MaxIterations { get; set; }
    }

    public class CandidateStrategy
    {
        public string Label { get; set; }
        public string Hint { get; set; }
    }
}
