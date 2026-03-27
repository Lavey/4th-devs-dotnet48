using System.Collections.Generic;

namespace FourthDevs.Artifacts.Models
{
    public class ArtifactDocument
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Prompt { get; set; }
        public string Html { get; set; }
        public string Model { get; set; }
        public List<string> Packs { get; set; }
        public string CreatedAt { get; set; }
    }

    public class SearchReplaceOperation
    {
        public string Search { get; set; }
        public string Replace { get; set; }
        public bool ReplaceAll { get; set; }
        public bool UseRegex { get; set; }
        public bool? CaseSensitive { get; set; }
        public string RegexFlags { get; set; }
    }

    public class AgentTurnResult
    {
        /// <summary>"chat" or "artifact"</summary>
        public string Kind { get; set; }

        /// <summary>"created" or "edited"</summary>
        public string Action { get; set; }

        public string Text { get; set; }
        public ArtifactDocument Artifact { get; set; }
    }
}
