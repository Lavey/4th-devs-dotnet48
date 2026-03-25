using System;
using System.Collections.Generic;
using FourthDevs.Language.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Language.Hooks
{
    public class AgentHooksManager
    {
        public string SessionId { get; }
        public string CurrentDate { get; }

        public bool ListenDone { get; private set; }
        public bool FeedbackDone { get; private set; }
        public bool SessionSaved { get; private set; }
        public bool ProfileUpdated { get; private set; }

        public string AudioInputPath { get; private set; }
        public string TextFeedback { get; private set; }
        public string SpokenFeedbackPath { get; private set; }
        public ListenResult ListenResult { get; private set; }

        public List<string> CompletedPhaseTexts { get; } = new List<string>();
        public List<string> PhaseErrors { get; } = new List<string>();

        public AgentHooksManager(string currentDate, string sessionId)
        {
            CurrentDate = currentDate;
            SessionId = sessionId;
        }

        public void BeforeToolCall(string toolName, JObject args)
        {
            if (string.Equals(toolName, "listen", StringComparison.OrdinalIgnoreCase))
            {
                string path = args?["path"]?.Value<string>();
                if (!string.IsNullOrEmpty(path))
                    AudioInputPath = path;
            }
        }

        public string AfterToolResult(string toolName, JObject args, string output)
        {
            if (string.Equals(toolName, "listen", StringComparison.OrdinalIgnoreCase))
            {
                ListenResult parsed = null;
                try { parsed = JsonConvert.DeserializeObject<ListenResult>(output); }
                catch { }

                if (parsed != null && !string.IsNullOrEmpty(parsed.Transcript))
                {
                    ListenResult = parsed;
                    ListenDone = true;
                    CompletedPhaseTexts.Add("listen");
                }
                else
                {
                    PhaseErrors.Add($"listen returned unexpected result");
                }
            }
            else if (string.Equals(toolName, "feedback", StringComparison.OrdinalIgnoreCase))
            {
                JObject parsed = null;
                try { parsed = JObject.Parse(output); }
                catch { }

                if (parsed != null && parsed["error"] == null)
                {
                    string textFb = parsed["text_feedback"]?.Value<string>();
                    string audioPath = parsed["output_path"]?.Value<string>();

                    if (!string.IsNullOrEmpty(textFb))
                        TextFeedback = textFb;
                    if (!string.IsNullOrEmpty(audioPath))
                        SpokenFeedbackPath = audioPath;

                    FeedbackDone = true;
                    CompletedPhaseTexts.Add("feedback");
                }
                else
                {
                    string errMsg = parsed?["error"]?.Value<string>() ?? output;
                    PhaseErrors.Add($"feedback error: {errMsg}");
                }
            }
            else if (string.Equals(toolName, "fs_write", StringComparison.OrdinalIgnoreCase))
            {
                string path = args?["path"]?.Value<string>() ?? string.Empty;
                if (path.StartsWith("sessions/", StringComparison.OrdinalIgnoreCase) ||
                    path.Contains("sessions\\") ||
                    path.Contains("/sessions/"))
                {
                    SessionSaved = true;
                    CompletedPhaseTexts.Add("session_saved");
                }
                else if (path.Equals("profile.json", StringComparison.OrdinalIgnoreCase) ||
                         path.EndsWith("/profile.json", StringComparison.OrdinalIgnoreCase) ||
                         path.EndsWith("\\profile.json", StringComparison.OrdinalIgnoreCase))
                {
                    ProfileUpdated = true;
                    CompletedPhaseTexts.Add("profile_updated");
                }
            }

            // Return null to use original output unchanged
            return null;
        }

        public (bool Allow, string InjectMessage) BeforeFinish(string finalText)
        {
            if (!ListenDone)
            {
                return (false,
                    "You haven't run the 'listen' tool yet. Please call listen with the audio file path first.");
            }

            if (!FeedbackDone)
            {
                string listenJson = ListenResult != null
                    ? JsonConvert.SerializeObject(ListenResult)
                    : "{}";
                return (false,
                    $"You haven't called 'feedback' yet. Please call feedback with listen_result_json={listenJson} to generate coaching feedback.");
            }

            if (!SessionSaved)
            {
                return (false,
                    $"Please save the session record to sessions/{SessionId}.json using fs_write.");
            }

            return (true, null);
        }

        public string BuildFallbackTextFeedback()
        {
            if (!string.IsNullOrEmpty(TextFeedback))
                return TextFeedback;

            if (ListenResult == null)
                return "The coaching session has completed. No listen results were captured.";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("## Coaching Feedback");

            if (!string.IsNullOrEmpty(ListenResult.Transcript))
                sb.AppendLine($"\n**Transcript:** {ListenResult.Transcript}");

            if (ListenResult.Strengths != null && ListenResult.Strengths.Count > 0)
            {
                sb.AppendLine("\n**Strengths:**");
                foreach (string s in ListenResult.Strengths)
                    sb.AppendLine($"- {s}");
            }

            if (ListenResult.Issues != null && ListenResult.Issues.Count > 0)
            {
                sb.AppendLine("\n**Areas to improve:**");
                foreach (var issue in ListenResult.Issues)
                {
                    sb.AppendLine($"- **{issue.TraitId}** ({issue.Severity}): {issue.Evidence}");
                    if (!string.IsNullOrEmpty(issue.Fix))
                        sb.AppendLine($"  → {issue.Fix}");
                }
            }

            if (!string.IsNullOrEmpty(SpokenFeedbackPath))
                sb.AppendLine($"\nAudio feedback saved to: {SpokenFeedbackPath}");

            return sb.ToString().Trim();
        }
    }
}
