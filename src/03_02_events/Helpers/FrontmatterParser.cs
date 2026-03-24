using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FourthDevs.Events.Helpers
{
    /// <summary>
    /// Simple YAML frontmatter parser for markdown files.
    /// Parses --- delimited YAML blocks with key-value pairs and lists.
    /// </summary>
    internal static class FrontmatterParser
    {
        public class ParseResult
        {
            public Dictionary<string, string> Fields { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            public string Body { get; set; } = string.Empty;
        }

        public static ParseResult Parse(string content)
        {
            var result = new ParseResult();
            if (string.IsNullOrEmpty(content))
                return result;

            string trimmed = content.TrimStart();
            if (!trimmed.StartsWith("---"))
            {
                result.Body = content;
                return result;
            }

            int firstSep = trimmed.IndexOf("---", StringComparison.Ordinal);
            int secondSep = trimmed.IndexOf("---", firstSep + 3, StringComparison.Ordinal);

            if (secondSep < 0)
            {
                result.Body = content;
                return result;
            }

            string yamlBlock = trimmed.Substring(firstSep + 3, secondSep - firstSep - 3).Trim();
            result.Body = trimmed.Substring(secondSep + 3);

            // Parse YAML key-value pairs (simple flat + list support)
            string currentKey = null;
            var listBuilder = new List<string>();

            foreach (string rawLine in yamlBlock.Split(new[] { '\n' }, StringSplitOptions.None))
            {
                string line = rawLine.TrimEnd('\r');

                // List item (  - value)
                if (line.TrimStart().StartsWith("- ") && currentKey != null)
                {
                    string item = line.TrimStart().Substring(2).Trim();
                    listBuilder.Add(item);
                    continue;
                }

                // Flush previous list
                if (currentKey != null && listBuilder.Count > 0)
                {
                    result.Fields[currentKey] = string.Join(",", listBuilder);
                    listBuilder.Clear();
                }

                int colonIdx = line.IndexOf(':');
                if (colonIdx < 0)
                {
                    currentKey = null;
                    continue;
                }

                string key = line.Substring(0, colonIdx).Trim();
                string value = line.Substring(colonIdx + 1).Trim();
                currentKey = key;

                if (!string.IsNullOrEmpty(value))
                {
                    result.Fields[key] = value;
                    currentKey = null; // not expecting list
                }
                // else: might be a list following
            }

            // Flush final list
            if (currentKey != null && listBuilder.Count > 0)
            {
                result.Fields[currentKey] = string.Join(",", listBuilder);
            }

            return result;
        }

        public static List<string> ParseList(string commaOrNewlineDelimited)
        {
            if (string.IsNullOrWhiteSpace(commaOrNewlineDelimited))
                return new List<string>();

            var items = new List<string>();
            foreach (string s in commaOrNewlineDelimited.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string trimItem = s.Trim();
                if (!string.IsNullOrEmpty(trimItem))
                    items.Add(trimItem);
            }
            return items;
        }

        /// <summary>
        /// Serialize fields to YAML frontmatter string.
        /// </summary>
        public static string Serialize(Dictionary<string, string> fields)
        {
            var sb = new StringBuilder();
            sb.AppendLine("---");
            foreach (var kv in fields)
            {
                if (kv.Value != null && kv.Value.Contains(","))
                {
                    sb.AppendLine(kv.Key + ":");
                    foreach (string item in ParseList(kv.Value))
                    {
                        sb.AppendLine("  - " + item);
                    }
                }
                else
                {
                    sb.AppendLine(kv.Key + ": " + (kv.Value ?? ""));
                }
            }
            sb.AppendLine("---");
            return sb.ToString();
        }

        /// <summary>
        /// Serialize TaskFrontmatter to a frontmatter+body markdown string.
        /// </summary>
        public static string SerializeTask(Models.TaskFrontmatter fm, string body)
        {
            var fields = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(fm.Id)) fields["id"] = fm.Id;
            if (!string.IsNullOrEmpty(fm.Title)) fields["title"] = fm.Title;
            fields["status"] = fm.Status ?? Models.TaskStatus.Open;
            fields["priority"] = fm.Priority ?? Models.TaskPriority.Medium;
            if (!string.IsNullOrEmpty(fm.AssignedTo)) fields["assigned_to"] = fm.AssignedTo;
            if (!string.IsNullOrEmpty(fm.RunId)) fields["run_id"] = fm.RunId;
            if (fm.DependsOn != null && fm.DependsOn.Count > 0)
                fields["depends_on"] = string.Join(",", fm.DependsOn);
            if (fm.Capabilities != null && fm.Capabilities.Count > 0)
                fields["capabilities"] = string.Join(",", fm.Capabilities);
            if (!string.IsNullOrEmpty(fm.CreatedAt)) fields["created_at"] = fm.CreatedAt;
            if (!string.IsNullOrEmpty(fm.UpdatedAt)) fields["updated_at"] = fm.UpdatedAt;
            if (!string.IsNullOrEmpty(fm.StartedAt)) fields["started_at"] = fm.StartedAt;
            if (!string.IsNullOrEmpty(fm.CompletedAt)) fields["completed_at"] = fm.CompletedAt;
            if (!string.IsNullOrEmpty(fm.BlockedReason)) fields["blocked_reason"] = fm.BlockedReason;
            if (!string.IsNullOrEmpty(fm.WaitId)) fields["wait_id"] = fm.WaitId;
            if (!string.IsNullOrEmpty(fm.WaitQuestion)) fields["wait_question"] = fm.WaitQuestion;
            if (!string.IsNullOrEmpty(fm.WaitAnswer)) fields["wait_answer"] = fm.WaitAnswer;
            if (!string.IsNullOrEmpty(fm.OutputFile)) fields["output_file"] = fm.OutputFile;
            if (!string.IsNullOrEmpty(fm.OutputType)) fields["output_type"] = fm.OutputType;
            if (fm.Attempts > 0) fields["attempts"] = fm.Attempts.ToString();
            fields["max_attempts"] = fm.MaxAttempts.ToString();
            if (!string.IsNullOrEmpty(fm.Phase)) fields["phase"] = fm.Phase;
            fields["order"] = fm.Order.ToString();
            if (!string.IsNullOrEmpty(fm.WorkflowId)) fields["workflow_id"] = fm.WorkflowId;
            if (!string.IsNullOrEmpty(fm.Agent)) fields["agent"] = fm.Agent;
            if (fm.Tags != null && fm.Tags.Count > 0)
                fields["tags"] = string.Join(",", fm.Tags);
            if (!string.IsNullOrEmpty(fm.ErrorMessage)) fields["error_message"] = fm.ErrorMessage;

            return Serialize(fields) + "\n" + (body ?? "");
        }

        /// <summary>
        /// Parse a TaskFrontmatter from a parsed frontmatter result.
        /// </summary>
        public static Models.TaskFrontmatter ToTaskFrontmatter(Dictionary<string, string> fields)
        {
            var fm = new Models.TaskFrontmatter();
            string val;
            if (fields.TryGetValue("id", out val)) fm.Id = val;
            if (fields.TryGetValue("title", out val)) fm.Title = val;
            if (fields.TryGetValue("status", out val)) fm.Status = val;
            if (fields.TryGetValue("priority", out val)) fm.Priority = val;
            if (fields.TryGetValue("assigned_to", out val)) fm.AssignedTo = val;
            if (fields.TryGetValue("run_id", out val)) fm.RunId = val;
            if (fields.TryGetValue("depends_on", out val)) fm.DependsOn = ParseList(val);
            if (fields.TryGetValue("capabilities", out val)) fm.Capabilities = ParseList(val);
            if (fields.TryGetValue("created_at", out val)) fm.CreatedAt = val;
            if (fields.TryGetValue("updated_at", out val)) fm.UpdatedAt = val;
            if (fields.TryGetValue("started_at", out val)) fm.StartedAt = val;
            if (fields.TryGetValue("completed_at", out val)) fm.CompletedAt = val;
            if (fields.TryGetValue("blocked_reason", out val)) fm.BlockedReason = val;
            if (fields.TryGetValue("wait_id", out val)) fm.WaitId = val;
            if (fields.TryGetValue("wait_question", out val)) fm.WaitQuestion = val;
            if (fields.TryGetValue("wait_answer", out val)) fm.WaitAnswer = val;
            if (fields.TryGetValue("output_file", out val)) fm.OutputFile = val;
            if (fields.TryGetValue("output_type", out val)) fm.OutputType = val;
            if (fields.TryGetValue("attempts", out val))
            {
                int a;
                if (int.TryParse(val, out a)) fm.Attempts = a;
            }
            if (fields.TryGetValue("max_attempts", out val))
            {
                int m;
                if (int.TryParse(val, out m)) fm.MaxAttempts = m;
            }
            if (fields.TryGetValue("phase", out val)) fm.Phase = val;
            if (fields.TryGetValue("order", out val))
            {
                int o;
                if (int.TryParse(val, out o)) fm.Order = o;
            }
            if (fields.TryGetValue("workflow_id", out val)) fm.WorkflowId = val;
            if (fields.TryGetValue("agent", out val)) fm.Agent = val;
            if (fields.TryGetValue("tags", out val)) fm.Tags = ParseList(val);
            if (fields.TryGetValue("error_message", out val)) fm.ErrorMessage = val;
            return fm;
        }
    }
}
