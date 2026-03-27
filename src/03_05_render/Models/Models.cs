using System.Collections.Generic;

namespace FourthDevs.Render.Models
{
    public class RenderSpecElement
    {
        public string Type { get; set; }
        public Dictionary<string, object> Props { get; set; }
        public List<string> Children { get; set; }
    }

    public class RenderSpec
    {
        public string Root { get; set; }
        public Dictionary<string, RenderSpecElement> Elements { get; set; }
    }

    public class RenderDocument
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Prompt { get; set; }
        public string Summary { get; set; }
        public RenderSpec Spec { get; set; }
        public Dictionary<string, object> State { get; set; }
        public string Html { get; set; }
        public string Model { get; set; }
        public List<string> Packs { get; set; }
        public string CreatedAt { get; set; }
    }

    public class AgentTurnResult
    {
        /// <summary>"chat" or "render"</summary>
        public string Kind { get; set; }
        public string Text { get; set; }
        public RenderDocument Document { get; set; }
    }
}
