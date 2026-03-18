using System.Collections.Generic;

namespace FourthDevs.Lesson05_Agent
{
    internal class SessionData
    {
        public string       Id      { get; set; }
        public List<object> History { get; set; } = new List<object>();
    }

    internal class AgentRunData
    {
        public string                Id           { get; set; }
        public string                SessionId    { get; set; }
        public string                Status       { get; set; }
        public string                Model        { get; set; }
        public List<object>          Conversation { get; set; }
        public List<WaitingForEntry> WaitingFor   { get; set; }
    }

    internal class WaitingForEntry
    {
        public string CallId   { get; set; }
        public string Type     { get; set; }
        public string Question { get; set; }
    }

    internal class WaitingForHumanException : System.Exception
    {
        public string CallId   { get; }
        public string Question { get; }

        public WaitingForHumanException(string callId, string question)
            : base("Agent is waiting for human input.")
        {
            CallId   = callId;
            Question = question;
        }
    }
}
