using Newtonsoft.Json;

namespace FourthDevs.AxClassifier.Models
{
    public class LabeledEmail
    {
        [JsonProperty("emailFrom")]
        public string EmailFrom { get; set; }

        [JsonProperty("emailSubject")]
        public string EmailSubject { get; set; }

        [JsonProperty("emailBody")]
        public string EmailBody { get; set; }

        [JsonProperty("labels")]
        public string[] Labels { get; set; }

        [JsonProperty("priority")]
        public string Priority { get; set; }

        [JsonProperty("needsReply")]
        public bool NeedsReply { get; set; }

        [JsonProperty("summary")]
        public string Summary { get; set; }
    }
}
