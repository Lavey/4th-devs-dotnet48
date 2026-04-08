namespace FourthDevs.AutoPrompt.Config
{
    public static class Defaults
    {
        public const string MODEL = "gpt-5.4";
        public const int MAX_ITERATIONS = 10;
        public const int EVAL_RUNS = 1;
        public const int CANDIDATE_COUNT = 3;
        public const string RUNS_DIR = "runs";

        public static readonly ModelProfile DefaultExecution = new ModelProfile { Model = MODEL };
        public static readonly ModelProfile DefaultJudge = new ModelProfile { Model = MODEL };
        public static readonly ModelProfile DefaultImprover = new ModelProfile { Model = MODEL };
    }

    public class ModelProfile
    {
        public string Model { get; set; }
        public ReasoningConfig Reasoning { get; set; }
    }

    public class ReasoningConfig
    {
        public string Effort { get; set; }
    }
}
