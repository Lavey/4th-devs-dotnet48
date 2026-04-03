using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using FourthDevs.Review.Models;

namespace FourthDevs.Review.Core
{
    internal static class Store
    {
        private static string _workspacePath;

        public static void Init(string workspacePath)
        {
            _workspacePath = workspacePath;
        }

        /// <summary>
        /// Resolve a relative path safely within the workspace, preventing directory traversal.
        /// </summary>
        private static string SafeResolvePath(string basePath, string relPath)
        {
            string combined = Path.Combine(basePath, relPath.Replace("/", Path.DirectorySeparatorChar.ToString()));
            string full = Path.GetFullPath(combined);
            string baseFullPath = Path.GetFullPath(basePath);
            if (!full.StartsWith(baseFullPath))
                throw new UnauthorizedAccessException("Path escapes workspace: " + relPath);
            return full;
        }

        // ---- Frontmatter parser ----

        public static void ParseFrontmatter(string content, out Dictionary<string, object> frontmatter, out string body)
        {
            frontmatter = new Dictionary<string, object>();
            body = content ?? string.Empty;

            if (content == null || !content.TrimStart().StartsWith("---"))
                return;

            // Normalise line endings
            string normalised = content.Replace("\r\n", "\n");
            int firstFence = normalised.IndexOf("---", StringComparison.Ordinal);
            if (firstFence < 0) return;

            int afterFirst = firstFence + 3;
            // Skip newline after first fence
            if (afterFirst < normalised.Length && normalised[afterFirst] == '\n')
                afterFirst++;

            int secondFence = normalised.IndexOf("\n---", afterFirst, StringComparison.Ordinal);
            if (secondFence < 0) return;

            string yamlBlock = normalised.Substring(afterFirst, secondFence - afterFirst);
            int bodyStart = secondFence + 4; // skip \n---
            if (bodyStart < normalised.Length && normalised[bodyStart] == '\n')
                bodyStart++;

            body = normalised.Substring(bodyStart);
            frontmatter = ParseSimpleYaml(yamlBlock);
        }

        private static Dictionary<string, object> ParseSimpleYaml(string yaml)
        {
            var result = new Dictionary<string, object>();
            string[] lines = yaml.Split('\n');
            string currentKey = null;
            List<string> currentList = null;
            bool inMultilineScalar = false;
            string multilineKey = null;
            List<string> multilineLines = null;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];

                // Continuation of array items (  - value)
                if (currentKey != null && currentList != null)
                {
                    var arrayItemMatch = Regex.Match(line, @"^\s+-\s+(.*)$");
                    if (arrayItemMatch.Success)
                    {
                        currentList.Add(arrayItemMatch.Groups[1].Value.Trim());
                        continue;
                    }
                    else
                    {
                        // End of array
                        result[currentKey] = currentList;
                        currentKey = null;
                        currentList = null;
                    }
                }

                // Multiline scalar (>- or |)
                if (inMultilineScalar)
                {
                    if (line.StartsWith("  ") || string.IsNullOrWhiteSpace(line))
                    {
                        multilineLines.Add(line.TrimStart());
                        continue;
                    }
                    else
                    {
                        result[multilineKey] = string.Join(" ", multilineLines).Trim();
                        inMultilineScalar = false;
                        multilineKey = null;
                        multilineLines = null;
                    }
                }

                // Skip empty lines
                if (string.IsNullOrWhiteSpace(line)) continue;

                // key: value
                var kvMatch = Regex.Match(line, @"^([a-zA-Z_][a-zA-Z0-9_-]*)\s*:\s*(.*)$");
                if (!kvMatch.Success) continue;

                string key = kvMatch.Groups[1].Value;
                string value = kvMatch.Groups[2].Value.Trim();

                // Array starting on same line [a, b, c]
                if (value.StartsWith("[") && value.EndsWith("]"))
                {
                    string inner = value.Substring(1, value.Length - 2);
                    var items = inner.Split(',')
                        .Select(s => s.Trim().Trim('"').Trim('\''))
                        .Where(s => s.Length > 0)
                        .ToList();
                    result[key] = items;
                    continue;
                }

                // Multiline scalar indicator
                if (value == ">-" || value == ">")
                {
                    inMultilineScalar = true;
                    multilineKey = key;
                    multilineLines = new List<string>();
                    continue;
                }

                // Empty value — might be array starting next line
                if (string.IsNullOrEmpty(value))
                {
                    // Peek to see if next line is an array item
                    if (i + 1 < lines.Length && Regex.IsMatch(lines[i + 1], @"^\s+-\s+"))
                    {
                        currentKey = key;
                        currentList = new List<string>();
                        continue;
                    }
                    result[key] = string.Empty;
                    continue;
                }

                // Trim quotes
                if ((value.StartsWith("\"") && value.EndsWith("\"")) ||
                    (value.StartsWith("'") && value.EndsWith("'")))
                {
                    value = value.Substring(1, value.Length - 2);
                }

                result[key] = value;
            }

            // Flush pending
            if (currentKey != null && currentList != null)
                result[currentKey] = currentList;
            if (inMultilineScalar && multilineKey != null && multilineLines != null)
                result[multilineKey] = string.Join(" ", multilineLines).Trim();

            return result;
        }

        // ---- Serialize frontmatter ----

        private static string SerializeFrontmatter(Dictionary<string, object> fm, string body)
        {
            if (fm == null || fm.Count == 0)
                return body;

            var parts = new List<string> { "---" };
            foreach (var kvp in fm)
            {
                if (kvp.Value is List<string> list)
                {
                    if (list.All(s => !s.Contains(",") && !s.Contains("\n") && s.Length < 60))
                    {
                        parts.Add(kvp.Key + ": [" + string.Join(", ", list) + "]");
                    }
                    else
                    {
                        parts.Add(kvp.Key + ":");
                        foreach (string item in list)
                            parts.Add("  - " + item);
                    }
                }
                else
                {
                    string val = kvp.Value?.ToString() ?? string.Empty;
                    if (val.Contains(":") || val.Contains("#") || val.Contains("\""))
                        parts.Add(kvp.Key + ": \"" + val.Replace("\"", "\\\"") + "\"");
                    else
                        parts.Add(kvp.Key + ": " + val);
                }
            }
            parts.Add("---");
            return string.Join("\n", parts) + "\n" + body;
        }

        // ---- File listing ----

        public static List<DocumentListItem> ListDocuments()
        {
            string docsDir = Path.Combine(_workspacePath, "documents");
            if (!Directory.Exists(docsDir))
                return new List<DocumentListItem>();

            var items = new List<DocumentListItem>();
            foreach (string file in Directory.GetFiles(docsDir, "*.md"))
            {
                string text = File.ReadAllText(file);
                ParseFrontmatter(text, out var fm, out _);
                string relPath = "documents/" + Path.GetFileName(file);
                items.Add(new DocumentListItem
                {
                    Path = relPath,
                    Title = fm.ContainsKey("title") ? fm["title"].ToString() : Path.GetFileNameWithoutExtension(file),
                    Summary = fm.ContainsKey("summary") ? fm["summary"].ToString() : string.Empty
                });
            }
            return items;
        }

        public static List<PromptListItem> ListPrompts()
        {
            string promptsDir = Path.Combine(_workspacePath, "prompts");
            if (!Directory.Exists(promptsDir))
                return new List<PromptListItem>();

            var items = new List<PromptListItem>();
            foreach (string file in Directory.GetFiles(promptsDir, "*.md"))
            {
                string text = File.ReadAllText(file);
                ParseFrontmatter(text, out var fm, out _);
                string relPath = "prompts/" + Path.GetFileName(file);

                var modes = new List<string>();
                if (fm.ContainsKey("modes"))
                {
                    if (fm["modes"] is List<string> ml)
                        modes = ml;
                }

                items.Add(new PromptListItem
                {
                    Path = relPath,
                    Title = fm.ContainsKey("title") ? fm["title"].ToString() : Path.GetFileNameWithoutExtension(file),
                    Description = fm.ContainsKey("description") ? fm["description"].ToString() : string.Empty,
                    Modes = modes
                });
            }
            return items;
        }

        // ---- Document operations ----

        public static DocumentData LoadDocument(string relPath)
        {
            string fullPath = SafeResolvePath(_workspacePath, relPath);
            if (!File.Exists(fullPath))
                throw new FileNotFoundException("Document not found: " + relPath);

            string text = File.ReadAllText(fullPath);
            ParseFrontmatter(text, out var fm, out string body);
            var blocks = MarkdownParser.Parse(body);

            return new DocumentData
            {
                Path = relPath,
                Frontmatter = fm,
                Blocks = blocks
            };
        }

        public static void SaveDocument(DocumentData doc)
        {
            string markdown = MarkdownParser.Serialize(doc.Blocks);
            string full = SerializeFrontmatter(doc.Frontmatter, markdown);
            string fullPath = SafeResolvePath(_workspacePath, doc.Path);
            File.WriteAllText(fullPath, full);
        }

        // ---- Prompt operations ----

        public static PromptData LoadPrompt(string relPath)
        {
            string fullPath = SafeResolvePath(_workspacePath, relPath);
            if (!File.Exists(fullPath))
                throw new FileNotFoundException("Prompt not found: " + relPath);

            string text = File.ReadAllText(fullPath);
            ParseFrontmatter(text, out var fm, out string body);

            // Load contextFiles if present
            string contextContent = string.Empty;
            if (fm.ContainsKey("contextFiles"))
            {
                var contextFiles = new List<string>();
                if (fm["contextFiles"] is List<string> cfl)
                    contextFiles = cfl;

                var contextParts = new List<string>();
                foreach (string cf in contextFiles)
                {
                    string cfPath = SafeResolvePath(_workspacePath, cf);
                    if (File.Exists(cfPath))
                        contextParts.Add(File.ReadAllText(cfPath));
                }
                contextContent = string.Join("\n\n", contextParts);
            }

            return new PromptData
            {
                Path = relPath,
                Frontmatter = fm,
                Content = body.Trim(),
                ContextContent = contextContent
            };
        }

        // ---- Agent operations ----

        public static AgentProfile LoadAgent(string name)
        {
            string fullPath = Path.Combine(_workspacePath, "system", "agents", name + ".md");
            if (!File.Exists(fullPath))
                throw new FileNotFoundException("Agent not found: " + name);

            string text = File.ReadAllText(fullPath);
            ParseFrontmatter(text, out var fm, out string body);

            string model = "gpt-4.1";
            if (fm.ContainsKey("model"))
                model = fm["model"].ToString();

            return new AgentProfile
            {
                Name = name,
                Model = model,
                Content = body.Trim(),
                Frontmatter = fm
            };
        }

        // ---- Review persistence ----

        public static void SaveReview(ReviewData review)
        {
            string reviewsDir = Path.Combine(_workspacePath, "reviews");
            if (!Directory.Exists(reviewsDir))
                Directory.CreateDirectory(reviewsDir);

            string filePath = SafeResolvePath(reviewsDir, review.Id + ".json");
            string json = JsonConvert.SerializeObject(review, Formatting.Indented);
            File.WriteAllText(filePath, json);
        }

        public static ReviewData LoadReview(string reviewId)
        {
            string filePath = SafeResolvePath(Path.Combine(_workspacePath, "reviews"), reviewId + ".json");
            if (!File.Exists(filePath))
                return null;

            string json = File.ReadAllText(filePath);
            return JsonConvert.DeserializeObject<ReviewData>(json);
        }

        public static ReviewData LoadLatestReviewForDocument(string docPath)
        {
            string reviewsDir = Path.Combine(_workspacePath, "reviews");
            if (!Directory.Exists(reviewsDir))
                return null;

            ReviewData latest = null;
            DateTime latestTime = DateTime.MinValue;

            foreach (string file in Directory.GetFiles(reviewsDir, "*.json"))
            {
                try
                {
                    string json = File.ReadAllText(file);
                    var review = JsonConvert.DeserializeObject<ReviewData>(json);
                    if (review.DocumentPath == docPath && !string.IsNullOrEmpty(review.CreatedAt))
                    {
                        DateTime created;
                        if (DateTime.TryParse(review.CreatedAt, out created) && created > latestTime)
                        {
                            latestTime = created;
                            latest = review;
                        }
                    }
                }
                catch { /* skip corrupt files */ }
            }

            return latest;
        }

        /// <summary>
        /// Hydrate a review by overlaying comment data onto document blocks.
        /// Returns the review with comments referencing valid blocks.
        /// </summary>
        public static ReviewData HydrateReviewForDocument(DocumentData document, ReviewData review)
        {
            if (review == null) return null;

            var blockMap = new Dictionary<string, MarkdownBlock>();
            foreach (var block in document.Blocks)
                blockMap[block.Id] = block;

            // Filter comments to only those referencing existing blocks
            var valid = new List<ReviewComment>();
            foreach (var comment in review.Comments)
            {
                if (blockMap.ContainsKey(comment.BlockId))
                    valid.Add(comment);
            }
            review.Comments = valid;
            return review;
        }
    }
}
