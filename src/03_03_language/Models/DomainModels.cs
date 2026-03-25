using System.Collections.Generic;
using Newtonsoft.Json;

namespace FourthDevs.Language.Models
{
    public class ListenIssue
    {
        [JsonProperty("trait_id")] public string TraitId { get; set; }
        [JsonProperty("evidence")] public string Evidence { get; set; }
        [JsonProperty("fix")] public string Fix { get; set; }
        [JsonProperty("severity")] public string Severity { get; set; }
    }

    public class ListenSegment
    {
        [JsonProperty("start_sec")] public double StartSec { get; set; }
        [JsonProperty("end_sec")] public double EndSec { get; set; }
        [JsonProperty("text")] public string Text { get; set; }
        [JsonProperty("confidence")] public double Confidence { get; set; }
    }

    public class ListenMetadata
    {
        [JsonProperty("word_count")] public int WordCount { get; set; }
        [JsonProperty("unique_word_count")] public int UniqueWordCount { get; set; }
        [JsonProperty("filler_counts")] public Dictionary<string, int> FillerCounts { get; set; }
        [JsonProperty("duration_sec")] public double DurationSec { get; set; }
        [JsonProperty("estimated_wpm")] public double? EstimatedWpm { get; set; }
    }

    public class ListenResult
    {
        [JsonProperty("transcript")] public string Transcript { get; set; }
        [JsonProperty("confidence")] public double Confidence { get; set; }
        [JsonProperty("strengths")] public List<string> Strengths { get; set; }
        [JsonProperty("issues")] public List<ListenIssue> Issues { get; set; }
        [JsonProperty("segments")] public List<ListenSegment> Segments { get; set; }
        [JsonProperty("metadata")] public ListenMetadata Metadata { get; set; }
    }

    public class LearnerProfile
    {
        [JsonProperty("role")] public string Role { get; set; }
        [JsonProperty("goals")] public List<string> Goals { get; set; }
        [JsonProperty("weakAreas")] public List<string> WeakAreas { get; set; }
    }
}
