using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FourthDevs.AxClassifier.Models;
using FourthDevs.Common;
using FourthDevs.Common.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.AxClassifier.Core
{
    public sealed class Classifier
    {
        private const string Model = "gpt-4.1-mini";

        private static readonly string LabelsEnum =
            string.Join(", ", Labels.All);

        private static readonly string Description =
            "Classify developer inbox emails. Assign ALL matching labels from the allowed set.\n" +
            "\"urgent\" = requires immediate action or time-sensitive.\n" +
            "\"needs-reply\" = sender explicitly expects a human response.\n" +
            "\"spam\" = unsolicited recruiter outreach, cold sales, or unwanted marketing.\n" +
            "\"automated\" = machine-generated notifications (CI, alerts, billing).\n" +
            "\"github\" = GitHub notifications (PRs, issues, security).\n" +
            "\"client\" = from an actual business client or partner.\n" +
            "\"internal\" = from a teammate or coworker.\n" +
            "\"newsletter\" = periodic digest or subscription.\n" +
            "\"billing\" = invoices, payments, subscription renewals.\n" +
            "\"security\" = security alerts or vulnerability reports.";

        private readonly List<LabeledEmail> _examples = new List<LabeledEmail>();
        private readonly ResponsesApiClient _client;

        public Classifier(ResponsesApiClient client)
        {
            _client = client;
        }

        public void SetExamples(List<LabeledEmail> examples)
        {
            _examples.Clear();
            _examples.AddRange(examples);
        }

        public async Task<ClassificationResult> ClassifyAsync(Email email)
        {
            return await ClassifyRawAsync(email.From, email.Subject, email.Body);
        }

        public async Task<ClassificationResult> ClassifyRawAsync(
            string emailFrom, string emailSubject, string emailBody)
        {
            var systemPrompt = BuildSystemPrompt();
            var userPrompt = BuildUserPrompt(emailFrom, emailSubject, emailBody);

            var request = new ResponsesRequest
            {
                Model = AiConfig.ResolveModel(Model),
                Input = new List<InputMessage>
                {
                    new InputMessage { Role = "system", Content = systemPrompt },
                    new InputMessage { Role = "user", Content = userPrompt }
                },
                Text = new TextOptions
                {
                    Format = new JObject
                    {
                        ["type"] = "json_schema",
                        ["name"] = "classification",
                        ["strict"] = true,
                        ["schema"] = BuildJsonSchema()
                    }
                }
            };

            var response = await _client.SendAsync(request);
            var text = ResponsesApiClient.ExtractText(response);
            var result = JsonConvert.DeserializeObject<ClassificationResult>(text);
            return result;
        }

        private string BuildSystemPrompt()
        {
            var sb = new StringBuilder();
            sb.AppendLine(Description);
            sb.AppendLine();
            sb.AppendLine(string.Format(
                "Allowed labels (pick ALL that match): {0}", LabelsEnum));
            sb.AppendLine("Priority must be one of: high, medium, low");
            sb.AppendLine("needsReply: true if the sender explicitly expects a human response.");
            sb.AppendLine("summary: one-sentence summary of the email.");

            if (_examples.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("=== Examples ===");
                foreach (var ex in _examples)
                {
                    sb.AppendLine();
                    sb.AppendLine(string.Format("From: {0}", ex.EmailFrom));
                    sb.AppendLine(string.Format("Subject: {0}", ex.EmailSubject));
                    sb.AppendLine(string.Format("Body: {0}", ex.EmailBody));
                    sb.AppendLine(string.Format("-> labels: [{0}]", string.Join(", ", ex.Labels)));
                    sb.AppendLine(string.Format("-> priority: {0}", ex.Priority));
                    sb.AppendLine(string.Format("-> needsReply: {0}",
                        ex.NeedsReply ? "true" : "false"));
                    if (!string.IsNullOrEmpty(ex.Summary))
                        sb.AppendLine(string.Format("-> summary: {0}", ex.Summary));
                }
                sb.AppendLine();
                sb.AppendLine("=== End Examples ===");
            }

            return sb.ToString();
        }

        private static string BuildUserPrompt(
            string emailFrom, string emailSubject, string emailBody)
        {
            return string.Format(
                "Classify this email:\n\nFrom: {0}\nSubject: {1}\nBody:\n{2}",
                emailFrom, emailSubject, emailBody);
        }

        private static JObject BuildJsonSchema()
        {
            var labelItems = new JArray();
            foreach (var l in Labels.All)
                labelItems.Add(l);

            return new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["labels"] = new JObject
                    {
                        ["type"] = "array",
                        ["items"] = new JObject
                        {
                            ["type"] = "string",
                            ["enum"] = labelItems
                        }
                    },
                    ["priority"] = new JObject
                    {
                        ["type"] = "string",
                        ["enum"] = new JArray("high", "medium", "low")
                    },
                    ["needsReply"] = new JObject
                    {
                        ["type"] = "boolean"
                    },
                    ["summary"] = new JObject
                    {
                        ["type"] = "string"
                    }
                },
                ["required"] = new JArray("labels", "priority", "needsReply", "summary"),
                ["additionalProperties"] = false
            };
        }
    }
}
