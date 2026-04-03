using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FourthDevs.Common;
using FourthDevs.Review.Models;
using FourthDevs.Review.Tools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Review.Core
{
    internal static class ReviewEngine
    {
        /// <summary>
        /// Run a full review of a document with the given prompt.
        /// Emits ReviewEvent objects via onEvent for NDJSON streaming.
        /// </summary>
        public static async Task RunReview(
            string documentPath,
            string promptPath,
            string mode,
            Action<ReviewEvent> onEvent)
        {
            try
            {
                var document = Store.LoadDocument(documentPath);
                var prompt = Store.LoadPrompt(promptPath);
                var agent = Store.LoadAgent("reviewer");

                // Create review
                string reviewId = "rev_" + Guid.NewGuid().ToString("N").Substring(0, 12);
                var review = new ReviewData
                {
                    Id = reviewId,
                    DocumentPath = documentPath,
                    PromptPath = promptPath,
                    Mode = mode,
                    CreatedAt = DateTime.UtcNow.ToString("o"),
                    Comments = new List<ReviewComment>()
                };
                Store.SaveReview(review);

                onEvent(new ReviewEvent { Type = "started", ReviewId = reviewId });

                if (mode == "paragraph")
                {
                    await RunParagraphMode(document, prompt, agent, review, onEvent);
                }
                else
                {
                    await RunAtOnceMode(document, prompt, agent, review, onEvent);
                }

                // Generate summary
                onEvent(new ReviewEvent { Type = "summary_start", ReviewId = reviewId });
                string summary = await GenerateSummary(review, document);
                review.Summary = summary;
                review.CompletedAt = DateTime.UtcNow.ToString("o");
                Store.SaveReview(review);

                onEvent(new ReviewEvent
                {
                    Type = "complete",
                    ReviewId = reviewId,
                    Summary = summary,
                    Review = review
                });
            }
            catch (Exception ex)
            {
                onEvent(new ReviewEvent { Type = "error", Error = ex.Message });
            }
        }

        private static async Task RunParagraphMode(
            DocumentData document,
            PromptData prompt,
            AgentProfile agent,
            ReviewData review,
            Action<ReviewEvent> onEvent)
        {
            var reviewable = document.Blocks.Where(b => b.Reviewable).ToList();
            var semaphore = new SemaphoreSlim(4);
            var tasks = new List<Task>();

            foreach (var block in reviewable)
            {
                var blockCapture = block;
                tasks.Add(Task.Run(async () =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        onEvent(new ReviewEvent
                        {
                            Type = "block_start",
                            ReviewId = review.Id,
                            BlockId = blockCapture.Id
                        });

                        await ReviewSingleBlock(
                            blockCapture, document.Blocks, prompt, agent, review, onEvent);

                        onEvent(new ReviewEvent
                        {
                            Type = "block_done",
                            ReviewId = review.Id,
                            BlockId = blockCapture.Id
                        });
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }));
            }

            await Task.WhenAll(tasks);
        }

        private static async Task RunAtOnceMode(
            DocumentData document,
            PromptData prompt,
            AgentProfile agent,
            ReviewData review,
            Action<ReviewEvent> onEvent)
        {
            onEvent(new ReviewEvent
            {
                Type = "block_start",
                ReviewId = review.Id,
                BlockId = "all"
            });

            string fullText = BuildFullDocumentContext(document, prompt);
            string instructions = BuildInstructions(agent, prompt, null);

            var comments = review.Comments;
            Action<ReviewComment> commentCallback = (c) =>
            {
                Store.SaveReview(review);
                onEvent(new ReviewEvent
                {
                    Type = "comment_added",
                    ReviewId = review.Id,
                    BlockId = c.BlockId,
                    Comment = c
                });
            };

            var handler = ReviewTools.CreateHandler(document.Blocks, comments, commentCallback);
            var tools = new List<ToolSpec>
            {
                new ToolSpec
                {
                    Definition = ReviewTools.GetAddCommentDefinition(),
                    Handler = handler
                }
            };

            await Agent.AgentRunner.RunAsync(fullText, instructions, agent.Model, tools);

            onEvent(new ReviewEvent
            {
                Type = "block_done",
                ReviewId = review.Id,
                BlockId = "all"
            });
        }

        private static async Task ReviewSingleBlock(
            MarkdownBlock block,
            List<MarkdownBlock> allBlocks,
            PromptData prompt,
            AgentProfile agent,
            ReviewData review,
            Action<ReviewEvent> onEvent)
        {
            string input = BuildBlockContext(block, prompt);
            string instructions = BuildInstructions(agent, prompt, block);

            var comments = review.Comments;
            Action<ReviewComment> commentCallback = (c) =>
            {
                lock (review)
                {
                    Store.SaveReview(review);
                }
                onEvent(new ReviewEvent
                {
                    Type = "comment_added",
                    ReviewId = review.Id,
                    BlockId = c.BlockId,
                    Comment = c
                });
            };

            var handler = ReviewTools.CreateHandler(allBlocks, comments, commentCallback);
            var tools = new List<ToolSpec>
            {
                new ToolSpec
                {
                    Definition = ReviewTools.GetAddCommentDefinition(),
                    Handler = handler
                }
            };

            await Agent.AgentRunner.RunAsync(input, instructions, agent.Model, tools);
        }

        private static string BuildInstructions(AgentProfile agent, PromptData prompt, MarkdownBlock currentBlock)
        {
            var sb = new StringBuilder();
            sb.AppendLine(agent.Content);
            sb.AppendLine();
            sb.AppendLine("--- Review Prompt ---");
            sb.AppendLine(prompt.Content);

            if (!string.IsNullOrEmpty(prompt.ContextContent))
            {
                sb.AppendLine();
                sb.AppendLine("--- Context ---");
                sb.AppendLine(prompt.ContextContent);
            }

            if (currentBlock != null)
            {
                sb.AppendLine();
                sb.AppendLine("You are reviewing block " + currentBlock.Id +
                              " (type: " + currentBlock.Type + "). " +
                              "Add at most two comments for this block.");
            }

            return sb.ToString();
        }

        private static string BuildBlockContext(MarkdownBlock block, PromptData prompt)
        {
            return "Review this block (id: " + block.Id + ", type: " + block.Type + "):\n\n" + block.Text;
        }

        private static string BuildFullDocumentContext(DocumentData document, PromptData prompt)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Review the following document. Each reviewable block is labeled with its id.");
            sb.AppendLine();
            foreach (var block in document.Blocks)
            {
                if (block.Reviewable)
                    sb.AppendLine("[" + block.Id + " | " + block.Type + "]: " + block.Text);
                else
                    sb.AppendLine("[" + block.Id + " | " + block.Type + " | not reviewable]");
                sb.AppendLine();
            }
            return sb.ToString();
        }

        private static async Task<string> GenerateSummary(ReviewData review, DocumentData document)
        {
            if (review.Comments.Count == 0)
                return "No comments were added during this review.";

            var sb = new StringBuilder();
            sb.AppendLine("Summarize the following review comments in 2-3 sentences:");
            foreach (var c in review.Comments)
            {
                sb.AppendLine("- [" + c.Severity + "] " + c.Title + ": " + c.Body);
            }

            try
            {
                string model = AiConfig.ResolveModel("gpt-4.1-mini");
                var body = new JObject
                {
                    ["model"] = model,
                    ["input"] = new JArray
                    {
                        new JObject
                        {
                            ["type"] = "message",
                            ["role"] = "user",
                            ["content"] = sb.ToString()
                        }
                    }
                };

                using (var http = new HttpClient())
                {
                    http.Timeout = TimeSpan.FromMinutes(2);
                    http.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Bearer", AiConfig.ApiKey);

                    if (AiConfig.Provider == "openrouter")
                    {
                        if (!string.IsNullOrWhiteSpace(AiConfig.HttpReferer))
                            http.DefaultRequestHeaders.TryAddWithoutValidation("HTTP-Referer", AiConfig.HttpReferer);
                        if (!string.IsNullOrWhiteSpace(AiConfig.AppName))
                            http.DefaultRequestHeaders.TryAddWithoutValidation("X-Title", AiConfig.AppName);
                    }

                    using (var content = new StringContent(body.ToString(Formatting.None), Encoding.UTF8, "application/json"))
                    using (var response = await http.PostAsync(AiConfig.ApiEndpoint, content))
                    {
                        string responseJson = await response.Content.ReadAsStringAsync();
                        var parsed = JObject.Parse(responseJson);
                        return parsed["output_text"]?.ToString() ?? "Review complete with " + review.Comments.Count + " comments.";
                    }
                }
            }
            catch
            {
                return "Review complete with " + review.Comments.Count + " comments.";
            }
        }

        // ---- Comment operations ----

        public static ReviewData AcceptReviewComment(string reviewId, string commentId)
        {
            var review = Store.LoadReview(reviewId);
            if (review == null) return null;

            var comment = review.Comments.FirstOrDefault(c => c.Id == commentId);
            if (comment == null) return review;

            if (comment.Kind == "suggestion" && !string.IsNullOrEmpty(comment.Suggestion))
            {
                // Find the document path from the review
                var document = Store.LoadDocument(review.DocumentPath);
                var block = document.Blocks.FirstOrDefault(b => b.Id == comment.BlockId);
                if (block != null)
                {
                    // Save previous text for revert
                    comment.PreviousText = block.Text;

                    string oldQuote = block.Text.Substring(comment.Start,
                        Math.Min(comment.End - comment.Start, block.Text.Length - comment.Start));
                    string newText = block.Text.Substring(0, comment.Start) +
                                     comment.Suggestion +
                                     block.Text.Substring(Math.Min(comment.End, block.Text.Length));
                    block.Text = newText;

                    // Shift sibling comment ranges
                    int delta = comment.Suggestion.Length - (comment.End - comment.Start);
                    foreach (var sibling in review.Comments)
                    {
                        if (sibling.Id == commentId || sibling.BlockId != comment.BlockId)
                            continue;

                        if (sibling.Start >= comment.End)
                        {
                            sibling.Start += delta;
                            sibling.End += delta;
                        }
                        else if (sibling.Start < comment.End && sibling.End > comment.Start)
                        {
                            // Overlapping — mark stale
                            sibling.Status = "stale";
                        }
                    }

                    Store.SaveDocument(document);
                }
            }

            comment.Status = "accepted";
            Store.SaveReview(review);
            return review;
        }

        public static ReviewData RejectReviewComment(string reviewId, string commentId)
        {
            var review = Store.LoadReview(reviewId);
            if (review == null) return null;

            var comment = review.Comments.FirstOrDefault(c => c.Id == commentId);
            if (comment != null)
                comment.Status = "rejected";

            Store.SaveReview(review);
            return review;
        }

        public static ReviewData ResolveReviewComment(string reviewId, string commentId)
        {
            var review = Store.LoadReview(reviewId);
            if (review == null) return null;

            var comment = review.Comments.FirstOrDefault(c => c.Id == commentId);
            if (comment != null)
                comment.Status = "resolved";

            Store.SaveReview(review);
            return review;
        }

        public static ReviewData ConvertToSuggestion(string reviewId, string commentId, string suggestion)
        {
            var review = Store.LoadReview(reviewId);
            if (review == null) return null;

            var comment = review.Comments.FirstOrDefault(c => c.Id == commentId);
            if (comment != null)
            {
                comment.Kind = "suggestion";
                comment.Suggestion = suggestion;
            }

            Store.SaveReview(review);
            return review;
        }

        public static ReviewData RevertReviewComment(string reviewId, string commentId)
        {
            var review = Store.LoadReview(reviewId);
            if (review == null) return null;

            var comment = review.Comments.FirstOrDefault(c => c.Id == commentId);
            if (comment != null && comment.Status == "accepted" && !string.IsNullOrEmpty(comment.PreviousText))
            {
                var document = Store.LoadDocument(review.DocumentPath);
                var block = document.Blocks.FirstOrDefault(b => b.Id == comment.BlockId);
                if (block != null)
                {
                    block.Text = comment.PreviousText;
                    Store.SaveDocument(document);
                }
                comment.Status = "open";
                comment.PreviousText = null;
            }

            Store.SaveReview(review);
            return review;
        }

        public static ReviewData BatchAcceptComments(string reviewId, List<string> commentIds)
        {
            ReviewData review = null;
            foreach (string id in commentIds)
                review = AcceptReviewComment(reviewId, id);
            return review;
        }

        public static ReviewData BatchRejectComments(string reviewId, List<string> commentIds)
        {
            var review = Store.LoadReview(reviewId);
            if (review == null) return null;

            foreach (string id in commentIds)
            {
                var comment = review.Comments.FirstOrDefault(c => c.Id == id);
                if (comment != null)
                    comment.Status = "rejected";
            }

            Store.SaveReview(review);
            return review;
        }

        /// <summary>
        /// Rerun review for a single block.
        /// </summary>
        public static async Task<ReviewData> RerunReviewBlock(
            string reviewId,
            string blockId,
            Action<ReviewEvent> onEvent)
        {
            var review = Store.LoadReview(reviewId);
            if (review == null) return null;

            var document = Store.LoadDocument(review.DocumentPath);
            var prompt = Store.LoadPrompt(review.PromptPath);
            var agent = Store.LoadAgent("reviewer");

            var block = document.Blocks.FirstOrDefault(b => b.Id == blockId);
            if (block == null) return review;

            // Remove existing open comments for this block
            review.Comments.RemoveAll(c => c.BlockId == blockId && c.Status == "open");

            onEvent(new ReviewEvent
            {
                Type = "block_start",
                ReviewId = review.Id,
                BlockId = blockId
            });

            string input = "Review this block (id: " + block.Id + ", type: " + block.Type + "):\n\n" + block.Text;
            string instructions = BuildInstructions(agent, prompt, block);

            Action<ReviewComment> commentCallback = (c) =>
            {
                Store.SaveReview(review);
                onEvent(new ReviewEvent
                {
                    Type = "comment_added",
                    ReviewId = review.Id,
                    BlockId = c.BlockId,
                    Comment = c
                });
            };

            var handler = ReviewTools.CreateHandler(document.Blocks, review.Comments, commentCallback);
            var tools = new List<ToolSpec>
            {
                new ToolSpec
                {
                    Definition = ReviewTools.GetAddCommentDefinition(),
                    Handler = handler
                }
            };

            await Agent.AgentRunner.RunAsync(input, instructions, agent.Model, tools);

            onEvent(new ReviewEvent
            {
                Type = "block_done",
                ReviewId = review.Id,
                BlockId = blockId
            });

            Store.SaveReview(review);
            return review;
        }

        public static ReviewData UpdateBlock(string reviewId, string blockId, string newText)
        {
            var review = Store.LoadReview(reviewId);
            if (review == null) return null;

            var document = Store.LoadDocument(review.DocumentPath);
            var block = document.Blocks.FirstOrDefault(b => b.Id == blockId);
            if (block != null)
            {
                block.Text = newText;
                Store.SaveDocument(document);

                // Mark all open comments on this block as stale
                foreach (var c in review.Comments)
                {
                    if (c.BlockId == blockId && c.Status == "open")
                        c.Status = "stale";
                }
                Store.SaveReview(review);
            }

            return review;
        }

        public static string GetDocumentMarkdown(string docPath)
        {
            var document = Store.LoadDocument(docPath);
            return MarkdownParser.Serialize(document.Blocks);
        }
    }
}
