using System;
using System.Collections.Generic;
using System.Linq;
using FourthDevs.Review.Core;
using FourthDevs.Review.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Review.Tools
{
    internal static class ReviewTools
    {
        public static JObject GetAddCommentDefinition()
        {
            return new JObject
            {
                ["type"] = "function",
                ["name"] = "add_comment",
                ["description"] = "Add a review comment anchored to an exact quote in a document block.",
                ["strict"] = true,
                ["parameters"] = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["block_id"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "Block id (e.g. b1, b2)."
                        },
                        ["quote"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "Exact text fragment from the block to anchor the comment."
                        },
                        ["kind"] = new JObject
                        {
                            ["type"] = "string",
                            ["enum"] = new JArray { "comment", "suggestion" },
                            ["description"] = "Whether this is a comment or a suggestion with replacement text."
                        },
                        ["severity"] = new JObject
                        {
                            ["type"] = "string",
                            ["enum"] = new JArray { "low", "medium", "high" },
                            ["description"] = "Severity level."
                        },
                        ["title"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "Short title for the comment."
                        },
                        ["comment"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "Detailed comment body."
                        },
                        ["suggestion"] = new JObject
                        {
                            ["type"] = "string",
                            ["description"] = "Replacement text for the quoted fragment. Required when kind=suggestion."
                        }
                    },
                    ["required"] = new JArray { "block_id", "quote", "kind", "severity", "title", "comment", "suggestion" },
                    ["additionalProperties"] = false
                }
            };
        }

        /// <summary>
        /// Create a handler that validates and creates a comment.
        /// Returns null if successful (comment added to list), or an error string.
        /// </summary>
        public static Func<string, string> CreateHandler(
            List<MarkdownBlock> blocks,
            List<ReviewComment> comments,
            Action<ReviewComment> onComment)
        {
            var blockMap = new Dictionary<string, MarkdownBlock>();
            foreach (var b in blocks)
                blockMap[b.Id] = b;

            return (string argsJson) =>
            {
                JObject args;
                try { args = JObject.Parse(argsJson); }
                catch { return "{\"error\": \"Invalid JSON arguments\"}"; }

                string blockId = args["block_id"]?.ToString();
                string quote = args["quote"]?.ToString();
                string kind = args["kind"]?.ToString() ?? "comment";
                string severity = args["severity"]?.ToString() ?? "low";
                string title = args["title"]?.ToString() ?? "";
                string comment = args["comment"]?.ToString() ?? "";
                string suggestion = args["suggestion"]?.ToString() ?? "";

                // Validate block exists
                if (string.IsNullOrEmpty(blockId) || !blockMap.ContainsKey(blockId))
                {
                    return JsonConvert.SerializeObject(new { error = "Block not found: " + blockId });
                }

                var block = blockMap[blockId];
                if (!block.Reviewable)
                {
                    return JsonConvert.SerializeObject(new { error = "Block is not reviewable: " + blockId });
                }

                // Find quote in block text
                if (string.IsNullOrEmpty(quote))
                {
                    return JsonConvert.SerializeObject(new { error = "Quote is required" });
                }

                var range = MarkdownParser.FindQuoteRange(block.Text, quote);
                if (!range.Found)
                {
                    return JsonConvert.SerializeObject(new
                    {
                        error = "Quote not found uniquely in block " + blockId + ". Make the quote longer or more specific.",
                        block_text = block.Text
                    });
                }

                // Check for overlapping comments
                foreach (var existing in comments)
                {
                    if (existing.BlockId != blockId || existing.Status != "open")
                        continue;

                    if (range.Start < existing.End && range.End > existing.Start)
                    {
                        return JsonConvert.SerializeObject(new
                        {
                            error = "Overlapping with existing comment '" + existing.Title + "' on block " + blockId
                        });
                    }
                }

                // Create comment
                var newComment = new ReviewComment
                {
                    Id = "c" + (comments.Count + 1),
                    BlockId = blockId,
                    Quote = quote,
                    Start = range.Start,
                    End = range.End,
                    Kind = kind,
                    Severity = severity,
                    Title = title,
                    Body = comment,
                    Suggestion = suggestion,
                    Status = "open",
                    CreatedAt = DateTime.UtcNow.ToString("o")
                };

                comments.Add(newComment);
                onComment?.Invoke(newComment);

                return JsonConvert.SerializeObject(new
                {
                    ok = true,
                    id = newComment.Id,
                    blockId = newComment.BlockId,
                    start = newComment.Start,
                    end = newComment.End
                });
            };
        }
    }
}
