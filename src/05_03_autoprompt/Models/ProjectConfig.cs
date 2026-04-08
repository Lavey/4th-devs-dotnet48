using System.Collections.Generic;
using Newtonsoft.Json;

namespace FourthDevs.AutoPrompt.Models
{
    public class AutoPromptConfigFile
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("prompt")]
        public string Prompt { get; set; }

        [JsonProperty("schema")]
        public string Schema { get; set; }

        [JsonProperty("testsDir")]
        public string TestsDir { get; set; }

        [JsonProperty("models")]
        public ModelsConfig Models { get; set; }

        [JsonProperty("optimization")]
        public OptimizationConfig Optimization { get; set; }

        [JsonProperty("evaluation")]
        public EvaluationConfig Evaluation { get; set; }
    }

    public class ModelsConfig
    {
        [JsonProperty("execution")]
        public ModelProfileConfig Execution { get; set; }

        [JsonProperty("judge")]
        public ModelProfileConfig Judge { get; set; }

        [JsonProperty("improver")]
        public ModelProfileConfig Improver { get; set; }
    }

    public class ModelProfileConfig
    {
        [JsonProperty("model")]
        public string Model { get; set; }

        [JsonProperty("reasoning")]
        public ReasoningConfigJson Reasoning { get; set; }
    }

    public class ReasoningConfigJson
    {
        [JsonProperty("effort")]
        public string Effort { get; set; }
    }

    public class OptimizationConfig
    {
        [JsonProperty("candidates")]
        public int? Candidates { get; set; }

        [JsonProperty("cases")]
        public List<string> Cases { get; set; }

        [JsonProperty("verifyCases")]
        public List<string> VerifyCases { get; set; }
    }

    public class EvaluationConfig
    {
        [JsonProperty("sections")]
        public List<EvaluationSection> Sections { get; set; }
    }

    public class EvaluationSection
    {
        [JsonProperty("key")]
        public string Key { get; set; }

        [JsonProperty("weight")]
        public double Weight { get; set; }

        [JsonProperty("matchBy")]
        public List<string> MatchBy { get; set; }

        [JsonProperty("fields")]
        public Dictionary<string, string> Fields { get; set; }
    }

    public class ExtractionSchema
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("schema")]
        public Newtonsoft.Json.Linq.JObject Schema { get; set; }
    }

    public class TestCase
    {
        public string Id { get; set; }
        public string Input { get; set; }
        public Newtonsoft.Json.Linq.JObject Expected { get; set; }
        public Newtonsoft.Json.Linq.JObject Context { get; set; }
    }

    public class LoadedProject
    {
        public string Name { get; set; }
        public string Dir { get; set; }
        public string ConfigPath { get; set; }
        public string PromptPath { get; set; }
        public string InitialPrompt { get; set; }
        public ExtractionSchema ExtractionSchema { get; set; }
        public EvaluationConfig Evaluation { get; set; }
        public ResolvedModels Models { get; set; }
        public OptimizationConfig Optimization { get; set; }
        public List<TestCase> TestCases { get; set; }
        public List<TestCase> VerifyCases { get; set; }
    }

    public class ResolvedModels
    {
        public Config.ModelProfile Execution { get; set; }
        public Config.ModelProfile Judge { get; set; }
        public Config.ModelProfile Improver { get; set; }
    }
}
