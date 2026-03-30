using System.Collections.Generic;

namespace FourthDevs.Apps.Models
{
    public class ListItem
    {
        public string Id { get; set; }
        public string Text { get; set; }
        public bool Done { get; set; }
    }

    public class ListsState
    {
        public List<ListItem> Todo { get; set; } = new List<ListItem>();
        public List<ListItem> Shopping { get; set; } = new List<ListItem>();
        public string UpdatedAt { get; set; }
    }

    public class AgentTurnResult
    {
        public string Kind { get; set; }   // "chat" or "open_manager"
        public string Text { get; set; }
        public string Focus { get; set; }  // "todo" or "shopping" (only when Kind == "open_manager")
    }
}
