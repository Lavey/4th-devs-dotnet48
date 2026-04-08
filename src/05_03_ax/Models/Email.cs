namespace FourthDevs.AxClassifier.Models
{
    public class Email
    {
        public string Id { get; set; }
        public string From { get; set; }
        public string Subject { get; set; }
        public string Body { get; set; }
    }

    public static class Labels
    {
        public static readonly string[] All = new[]
        {
            "urgent",
            "client",
            "internal",
            "newsletter",
            "billing",
            "github",
            "security",
            "spam",
            "automated",
            "needs-reply"
        };
    }
}
