using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace FourthDevs.Review.Core
{
    internal static class MarkdownParser
    {
        /// <summary>
        /// Parse markdown content into blocks.
        /// </summary>
        public static List<Models.MarkdownBlock> Parse(string content)
        {
            var blocks = new List<Models.MarkdownBlock>();
            if (string.IsNullOrEmpty(content)) return blocks;

            string[] lines = content.Replace("\r\n", "\n").Split('\n');
            int order = 0;
            bool inCode = false;
            List<string> codeLines = null;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];

                // Code fence
                if (line.TrimStart().StartsWith("```"))
                {
                    if (!inCode)
                    {
                        inCode = true;
                        codeLines = new List<string> { line };
                        continue;
                    }
                    else
                    {
                        codeLines.Add(line);
                        inCode = false;
                        order++;
                        blocks.Add(new Models.MarkdownBlock
                        {
                            Id = "b" + order,
                            Order = order,
                            Type = "code",
                            Text = string.Join("\n", codeLines),
                            Reviewable = false
                        });
                        codeLines = null;
                        continue;
                    }
                }

                if (inCode)
                {
                    codeLines.Add(line);
                    continue;
                }

                // Skip blank lines
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                // Thematic break
                if (IsThematicBreak(line))
                {
                    order++;
                    blocks.Add(new Models.MarkdownBlock
                    {
                        Id = "b" + order,
                        Order = order,
                        Type = "thematic_break",
                        Text = line,
                        Reviewable = false
                    });
                    continue;
                }

                // Heading
                var headingMatch = Regex.Match(line, @"^(#{1,6})\s+(.*)$");
                if (headingMatch.Success)
                {
                    int level = headingMatch.Groups[1].Value.Length;
                    string headingText = headingMatch.Groups[2].Value;
                    order++;
                    var block = new Models.MarkdownBlock
                    {
                        Id = "b" + order,
                        Order = order,
                        Type = "heading",
                        Text = headingText,
                        Reviewable = true
                    };
                    block.Meta["level"] = level;
                    blocks.Add(block);
                    continue;
                }

                // Blockquote
                if (line.TrimStart().StartsWith(">"))
                {
                    var quoteLines = new List<string>();
                    int j = i;
                    while (j < lines.Length && lines[j].TrimStart().StartsWith(">"))
                    {
                        string ql = Regex.Replace(lines[j], @"^>\s?", "");
                        quoteLines.Add(ql);
                        j++;
                    }
                    i = j - 1;
                    order++;
                    blocks.Add(new Models.MarkdownBlock
                    {
                        Id = "b" + order,
                        Order = order,
                        Type = "blockquote",
                        Text = string.Join("\n", quoteLines),
                        Reviewable = true
                    });
                    continue;
                }

                // HTML block (starts with <)
                if (line.TrimStart().StartsWith("<"))
                {
                    order++;
                    blocks.Add(new Models.MarkdownBlock
                    {
                        Id = "b" + order,
                        Order = order,
                        Type = "html_block",
                        Text = line,
                        Reviewable = false
                    });
                    continue;
                }

                // Table (line contains | and next line is separator)
                if (line.Contains("|") && i + 1 < lines.Length && IsTableSeparator(lines[i + 1]))
                {
                    var tableLines = new List<string>();
                    int j = i;
                    while (j < lines.Length && !string.IsNullOrWhiteSpace(lines[j]) && lines[j].Contains("|"))
                    {
                        tableLines.Add(lines[j]);
                        j++;
                    }
                    i = j - 1;
                    order++;
                    blocks.Add(new Models.MarkdownBlock
                    {
                        Id = "b" + order,
                        Order = order,
                        Type = "table",
                        Text = string.Join("\n", tableLines),
                        Reviewable = false
                    });
                    continue;
                }

                // List item
                var listMatch = Regex.Match(line, @"^(\s*)([-*]|\d+[.)]) (.*)$");
                if (listMatch.Success)
                {
                    string indent = listMatch.Groups[1].Value;
                    string marker = listMatch.Groups[2].Value;
                    string itemText = listMatch.Groups[3].Value;
                    int depth = indent.Length / 2;
                    bool ordered = Regex.IsMatch(marker, @"^\d+");
                    string listType = ordered ? "ordered" : "bullet";

                    // Check for checkbox
                    bool? checkedState = null;
                    var cbMatch = Regex.Match(itemText, @"^\[([ xX])\]\s?(.*)$");
                    if (cbMatch.Success)
                    {
                        checkedState = cbMatch.Groups[1].Value.Trim().ToUpperInvariant() == "X";
                        itemText = cbMatch.Groups[2].Value;
                    }

                    order++;
                    var block = new Models.MarkdownBlock
                    {
                        Id = "b" + order,
                        Order = order,
                        Type = "list_item",
                        Text = itemText,
                        Reviewable = true
                    };
                    block.Meta["listType"] = listType;
                    block.Meta["depth"] = depth;
                    block.Meta["marker"] = marker;
                    if (checkedState.HasValue)
                        block.Meta["checked"] = checkedState.Value;
                    blocks.Add(block);
                    continue;
                }

                // Paragraph — collect consecutive non-blank, non-special lines
                {
                    var paraLines = new List<string> { line };
                    int j = i + 1;
                    while (j < lines.Length &&
                           !string.IsNullOrWhiteSpace(lines[j]) &&
                           !lines[j].TrimStart().StartsWith("#") &&
                           !lines[j].TrimStart().StartsWith(">") &&
                           !lines[j].TrimStart().StartsWith("```") &&
                           !lines[j].TrimStart().StartsWith("<") &&
                           !IsThematicBreak(lines[j]) &&
                           !Regex.IsMatch(lines[j], @"^(\s*)([-*]|\d+[.)]) "))
                    {
                        paraLines.Add(lines[j]);
                        j++;
                    }
                    i = j - 1;
                    order++;
                    blocks.Add(new Models.MarkdownBlock
                    {
                        Id = "b" + order,
                        Order = order,
                        Type = "paragraph",
                        Text = string.Join("\n", paraLines),
                        Reviewable = true
                    });
                }
            }

            // Handle unclosed code block
            if (inCode && codeLines != null)
            {
                order++;
                blocks.Add(new Models.MarkdownBlock
                {
                    Id = "b" + order,
                    Order = order,
                    Type = "code",
                    Text = string.Join("\n", codeLines),
                    Reviewable = false
                });
            }

            return blocks;
        }

        /// <summary>
        /// Serialize blocks back to markdown.
        /// </summary>
        public static string Serialize(List<Models.MarkdownBlock> blocks)
        {
            var parts = new List<string>();
            foreach (var block in blocks)
            {
                switch (block.Type)
                {
                    case "heading":
                        int level = 1;
                        if (block.Meta.ContainsKey("level"))
                        {
                            if (block.Meta["level"] is int li) level = li;
                            else if (block.Meta["level"] is long ll) level = (int)ll;
                            else int.TryParse(block.Meta["level"].ToString(), out level);
                        }
                        parts.Add(new string('#', level) + " " + block.Text);
                        break;

                    case "list_item":
                        string marker = "- ";
                        if (block.Meta.ContainsKey("marker"))
                            marker = block.Meta["marker"].ToString() + " ";
                        int depth = 0;
                        if (block.Meta.ContainsKey("depth"))
                        {
                            if (block.Meta["depth"] is int di) depth = di;
                            else if (block.Meta["depth"] is long dl) depth = (int)dl;
                            else int.TryParse(block.Meta["depth"].ToString(), out depth);
                        }
                        string indent = new string(' ', depth * 2);
                        string itemText = block.Text;
                        if (block.Meta.ContainsKey("checked"))
                        {
                            bool chk = false;
                            if (block.Meta["checked"] is bool b) chk = b;
                            itemText = (chk ? "[x] " : "[ ] ") + itemText;
                        }
                        parts.Add(indent + marker + itemText);
                        break;

                    case "blockquote":
                        string[] qLines = block.Text.Split('\n');
                        var quoteParts = new List<string>();
                        foreach (string ql in qLines)
                            quoteParts.Add("> " + ql);
                        parts.Add(string.Join("\n", quoteParts));
                        break;

                    case "code":
                    case "table":
                    case "html_block":
                    case "thematic_break":
                        parts.Add(block.Text);
                        break;

                    default: // paragraph
                        parts.Add(block.Text);
                        break;
                }
            }
            return string.Join("\n\n", parts) + "\n";
        }

        /// <summary>
        /// Find exact quote position in text. Returns range if found uniquely.
        /// </summary>
        public static Models.QuoteRange FindQuoteRange(string text, string quote)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(quote))
                return new Models.QuoteRange { Found = false };

            int idx = text.IndexOf(quote, StringComparison.Ordinal);
            if (idx < 0)
                return new Models.QuoteRange { Found = false };

            // Check for uniqueness
            int secondIdx = text.IndexOf(quote, idx + 1, StringComparison.Ordinal);
            if (secondIdx >= 0)
                return new Models.QuoteRange { Found = false };

            return new Models.QuoteRange
            {
                Start = idx,
                End = idx + quote.Length,
                Found = true
            };
        }

        /// <summary>
        /// Resolve comment range from stored start/end or by finding quote.
        /// </summary>
        public static Models.QuoteRange ResolveCommentRange(string text, Models.ReviewComment comment)
        {
            if (comment.Start >= 0 && comment.End > comment.Start &&
                comment.End <= text.Length)
            {
                return new Models.QuoteRange
                {
                    Start = comment.Start,
                    End = comment.End,
                    Found = true
                };
            }

            return FindQuoteRange(text, comment.Quote);
        }

        private static bool IsThematicBreak(string line)
        {
            string trimmed = line.Trim();
            if (trimmed.Length < 3) return false;
            // Must be only -, *, or _ (with optional spaces)
            string stripped = trimmed.Replace(" ", "");
            if (stripped.Length < 3) return false;
            char first = stripped[0];
            if (first != '-' && first != '*' && first != '_') return false;
            foreach (char c in stripped)
            {
                if (c != first) return false;
            }
            return true;
        }

        private static bool IsTableSeparator(string line)
        {
            return line.Contains("|") && Regex.IsMatch(line, @"^[\s|:-]+$");
        }
    }
}
