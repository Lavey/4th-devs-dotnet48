using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FourthDevs.Language.Core;
using FourthDevs.Language.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Language.Tools
{
    public class LocalToolDef
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public object Parameters { get; set; }
        public Func<JObject, Task<string>> Handler { get; set; }
    }

    public static class AgentTools
    {
        private static readonly string[] FillerWords = { "um", "uh", "like", "actually", "basically" };

        public static List<LocalToolDef> CreateTools(string workspaceDir)
        {
            return new List<LocalToolDef>
            {
                CreateListenTool(workspaceDir),
                CreateSpeakTool(workspaceDir),
                CreateFeedbackTool(workspaceDir),
                CreateFsReadTool(workspaceDir),
                CreateFsWriteTool(workspaceDir)
            };
        }

        // ----------------------------------------------------------------
        // listen tool
        // ----------------------------------------------------------------

        private static LocalToolDef CreateListenTool(string workspaceDir)
        {
            return new LocalToolDef
            {
                Name = "listen",
                Description = "Analyze audio input and return structured JSON with transcript, issues, strengths, segments, and confidence.",
                Parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        path = new { type = "string", description = "Audio path in workspace, e.g. input/day.wav" }
                    },
                    required = new[] { "path" },
                    additionalProperties = false
                },
                Handler = async args =>
                {
                    string rawPath = args["path"]?.Value<string>() ?? string.Empty;

                    // Strip "workspace/" prefix if agent includes it
                    if (rawPath.StartsWith("workspace/", StringComparison.OrdinalIgnoreCase))
                        rawPath = rawPath.Substring("workspace/".Length);
                    if (rawPath.StartsWith("workspace\\", StringComparison.OrdinalIgnoreCase))
                        rawPath = rawPath.Substring("workspace\\".Length);

                    string fullPath = Path.GetFullPath(Path.Combine(workspaceDir, rawPath));
                    if (!fullPath.StartsWith(workspaceDir, StringComparison.OrdinalIgnoreCase))
                        return JsonConvert.SerializeObject(new { error = "Path traversal detected" });

                    if (!File.Exists(fullPath))
                        return JsonConvert.SerializeObject(new { error = $"File not found: {rawPath}" });

                    byte[] audioBytes = File.ReadAllBytes(fullPath);
                    string audioBase64 = Convert.ToBase64String(audioBytes);
                    string mime = GeminiClient.MimeFor(fullPath);

                    string model = GetModel();
                    string analysisPrompt =
                        "You are an English pronunciation and language coach. Analyze the audio carefully.\n\n" +
                        "Return ONLY a JSON object (no markdown) with this exact schema:\n" +
                        "{\n" +
                        "  \"transcript\": \"<full verbatim transcript>\",\n" +
                        "  \"confidence\": <0.0-1.0>,\n" +
                        "  \"strengths\": [\"<what speaker does well>\"],\n" +
                        "  \"issues\": [\n" +
                        "    {\n" +
                        "      \"trait_id\": \"<snake_case_id>\",\n" +
                        "      \"evidence\": \"<exact quote or description>\",\n" +
                        "      \"fix\": \"<actionable advice>\",\n" +
                        "      \"severity\": \"low|medium|high\"\n" +
                        "    }\n" +
                        "  ],\n" +
                        "  \"segments\": [\n" +
                        "    { \"start_sec\": 0.0, \"end_sec\": 5.0, \"text\": \"...\", \"confidence\": 0.9 }\n" +
                        "  ],\n" +
                        "  \"metadata\": {\n" +
                        "    \"word_count\": <int>,\n" +
                        "    \"unique_word_count\": <int>,\n" +
                        "    \"filler_counts\": { \"um\": 0, \"uh\": 0, \"like\": 0, \"actually\": 0, \"basically\": 0 },\n" +
                        "    \"duration_sec\": <float>,\n" +
                        "    \"estimated_wpm\": <float or null>\n" +
                        "  }\n" +
                        "}\n\n" +
                        "Focus on: pronunciation clarity, grammar, vocabulary, filler words, pace, and intonation.";

                    var request = new GeminiInteractionRequest
                    {
                        Model = model,
                        Input = new object[]
                        {
                            new GeminiTextInput { Text = analysisPrompt },
                            new GeminiAudioInput { MimeType = mime, Data = audioBase64 }
                        },
                        GenerationConfig = new Dictionary<string, object> { ["temperature"] = 0.1 }
                    };

                    GeminiInteraction interaction = await GeminiClient.CallInteractionAsync(request);
                    string rawText = GeminiClient.ExtractText(interaction.Outputs);
                    return NormalizeListenResult(rawText);
                }
            };
        }

        private static string NormalizeListenResult(string rawText)
        {
            ListenResult result = GeminiClient.ParseJsonLoose<ListenResult>(rawText);

            if (result == null)
                result = new ListenResult { Transcript = rawText };

            if (result.Strengths == null) result.Strengths = new List<string>();
            if (result.Issues == null) result.Issues = new List<ListenIssue>();
            if (result.Segments == null) result.Segments = new List<ListenSegment>();

            string transcript = result.Transcript ?? string.Empty;
            string[] words = transcript.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            if (result.Metadata == null)
            {
                result.Metadata = new ListenMetadata();
            }

            if (result.Metadata.WordCount == 0)
                result.Metadata.WordCount = words.Length;

            if (result.Metadata.UniqueWordCount == 0)
            {
                var unique = new System.Collections.Generic.HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);
                foreach (var w in words)
                    unique.Add(w.Trim('.', ',', '!', '?', ';', ':'));
                result.Metadata.UniqueWordCount = unique.Count;
            }

            if (result.Metadata.FillerCounts == null)
            {
                result.Metadata.FillerCounts = new Dictionary<string, int>();
                string lowerTranscript = transcript.ToLowerInvariant();
                foreach (string filler in FillerWords)
                {
                    int count = CountOccurrences(lowerTranscript, filler);
                    result.Metadata.FillerCounts[filler] = count;
                }
            }

            return JsonConvert.SerializeObject(result, Formatting.None);
        }

        private static int CountOccurrences(string text, string word)
        {
            int count = 0;
            int index = 0;
            while ((index = text.IndexOf(word, index, StringComparison.Ordinal)) >= 0)
            {
                // Simple whole-word check
                bool beforeOk = index == 0 || !char.IsLetterOrDigit(text[index - 1]);
                bool afterOk = index + word.Length >= text.Length || !char.IsLetterOrDigit(text[index + word.Length]);
                if (beforeOk && afterOk) count++;
                index += word.Length;
            }
            return count;
        }

        // ----------------------------------------------------------------
        // speak tool
        // ----------------------------------------------------------------

        private static LocalToolDef CreateSpeakTool(string workspaceDir)
        {
            return new LocalToolDef
            {
                Name = "speak",
                Description = "Generate spoken feedback audio from text and save it in workspace.",
                Parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        text = new { type = "string", description = "Text to speak" },
                        output_path = new { type = "string", description = "Output path within workspace, e.g. output/feedback.wav" },
                        style = new { type = "string", description = "Speaking style: slow or normal", @enum = new[] { "slow", "normal" } }
                    },
                    required = new[] { "text", "output_path" },
                    additionalProperties = false
                },
                Handler = args => SpeakHandler(args, workspaceDir)
            };
        }

        internal static async Task<string> SpeakHandler(JObject args, string workspaceDir)
        {
            string text = args["text"]?.Value<string>() ?? string.Empty;
            string outputPath = args["output_path"]?.Value<string>() ?? "output/feedback.wav";
            string style = args["style"]?.Value<string>() ?? "normal";

            string ttsModel = GetTtsModel();
            string stylePrompt = style == "slow"
                ? "Speak slowly and clearly, pausing between key phrases. This is for a language learner."
                : "Speak at a natural, conversational pace.";

            var request = new GeminiInteractionRequest
            {
                Model = ttsModel,
                Input = new object[]
                {
                    new GeminiTextInput { Text = $"{stylePrompt}\n\n{text}" }
                },
                ResponseModalities = new List<string> { "AUDIO" },
                GenerationConfig = new Dictionary<string, object>
                {
                    ["speech_config"] = new
                    {
                        language = "en-us",
                        voice = "kore"
                    }
                }
            };

            GeminiInteraction interaction = await GeminiClient.CallInteractionAsync(request);
            var (audioData, audioMime) = GeminiClient.ExtractAudio(interaction.Outputs);

            if (string.IsNullOrEmpty(audioData))
                return JsonConvert.SerializeObject(new { error = "No audio returned from TTS" });

            byte[] audioBytes = Convert.FromBase64String(audioData);

            // Convert PCM to WAV if needed
            string safeMime = audioMime ?? string.Empty;
            bool isWav = safeMime.Contains("wav");
            bool isPcm = safeMime.Contains("pcm") || (!isWav && !safeMime.Contains("mpeg") && !safeMime.Contains("mp4") && !safeMime.Contains("ogg"));
            byte[] outputBytes = isPcm ? GeminiClient.ToWav(audioBytes, 24000) : audioBytes;

            // Strip workspace/ prefix if present
            string relativePath = outputPath;
            if (relativePath.StartsWith("workspace/", StringComparison.OrdinalIgnoreCase))
                relativePath = relativePath.Substring("workspace/".Length);
            if (relativePath.StartsWith("workspace\\", StringComparison.OrdinalIgnoreCase))
                relativePath = relativePath.Substring("workspace\\".Length);

            string fullPath = Path.GetFullPath(Path.Combine(workspaceDir, relativePath));
            if (!fullPath.StartsWith(workspaceDir, StringComparison.OrdinalIgnoreCase))
                return JsonConvert.SerializeObject(new { error = "Path traversal detected" });

            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllBytes(fullPath, outputBytes);

            return JsonConvert.SerializeObject(new
            {
                output_path = relativePath,
                bytes = outputBytes.Length,
                mime = "audio/wav"
            });
        }

        // ----------------------------------------------------------------
        // feedback tool
        // ----------------------------------------------------------------

        private static LocalToolDef CreateFeedbackTool(string workspaceDir)
        {
            return new LocalToolDef
            {
                Name = "feedback",
                Description = "Generate personalized coaching text and spoken feedback from listen output.",
                Parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        listen_result_json = new { type = "string", description = "JSON output from the listen tool" },
                        profile_json = new { type = "string", description = "Optional learner profile JSON" },
                        output_path = new { type = "string", description = "Output audio path, default output/feedback.wav" },
                        style = new { type = "string", description = "Speaking style: slow or normal", @enum = new[] { "slow", "normal" } }
                    },
                    required = new[] { "listen_result_json" },
                    additionalProperties = false
                },
                Handler = async args =>
                {
                    string listenJson = args["listen_result_json"]?.Value<string>() ?? "{}";
                    string profileJson = args["profile_json"]?.Value<string>();
                    string outputPath = args["output_path"]?.Value<string>() ?? "output/feedback.wav";
                    string styleArg = args["style"]?.Value<string>();

                    ListenResult listenResult = null;
                    try { listenResult = JsonConvert.DeserializeObject<ListenResult>(listenJson); }
                    catch { }

                    LearnerProfile profile = null;
                    if (!string.IsNullOrWhiteSpace(profileJson))
                    {
                        try { profile = JsonConvert.DeserializeObject<LearnerProfile>(profileJson); }
                        catch { }
                    }

                    // Determine style
                    string style = styleArg;
                    if (string.IsNullOrEmpty(style))
                    {
                        bool hasPronunciationIssues = listenResult?.Issues != null &&
                            listenResult.Issues.Exists(i =>
                                i.TraitId != null && i.TraitId.ToLowerInvariant().Contains("pronun"));
                        style = hasPronunciationIssues ? "slow" : "normal";
                    }

                    string profileContext = profile != null
                        ? $"\nLearner profile: role={profile.Role}, goals={string.Join(", ", profile.Goals ?? new List<string>())}, weakAreas={string.Join(", ", profile.WeakAreas ?? new List<string>())}"
                        : string.Empty;

                    string feedbackPrompt =
                        "You are an expert English coach. Based on the following analysis of a learner's speech, " +
                        "generate personalized coaching feedback." + profileContext + "\n\n" +
                        "Analysis:\n" + listenJson + "\n\n" +
                        "Return ONLY a JSON object with this schema:\n" +
                        "{\n" +
                        "  \"text_feedback\": \"<markdown coaching feedback with specific examples and tips>\",\n" +
                        "  \"speech_script\": \"<natural spoken version of the feedback, 2-4 sentences, no markdown>\",\n" +
                        "  \"issues_used\": [\"<trait_id1>\", \"<trait_id2>\"]\n" +
                        "}";

                    string model = GetModel();
                    var feedbackRequest = new GeminiInteractionRequest
                    {
                        Model = model,
                        Input = new object[] { new GeminiTextInput { Text = feedbackPrompt } },
                        GenerationConfig = new Dictionary<string, object> { ["temperature"] = 0.2 }
                    };

                    GeminiInteraction feedbackInteraction = await GeminiClient.CallInteractionAsync(feedbackRequest);
                    string rawText = GeminiClient.ExtractText(feedbackInteraction.Outputs);

                    string textFeedback = rawText;
                    string speechScript = rawText;
                    List<string> issuesUsed = new List<string>();

                    var parsed = GeminiClient.ParseJsonLoose<JObject>(rawText);
                    if (parsed != null)
                    {
                        textFeedback = parsed["text_feedback"]?.Value<string>() ?? rawText;
                        speechScript = parsed["speech_script"]?.Value<string>() ?? rawText;
                        var issuesArr = parsed["issues_used"] as JArray;
                        if (issuesArr != null)
                            foreach (var item in issuesArr)
                                issuesUsed.Add(item.Value<string>());
                    }

                    // Generate audio via speak tool
                    var speakArgs = new JObject
                    {
                        ["text"] = speechScript,
                        ["output_path"] = outputPath,
                        ["style"] = style
                    };

                    string speakResult = await SpeakHandler(speakArgs, workspaceDir);
                    JObject speakJson = null;
                    try { speakJson = JObject.Parse(speakResult); }
                    catch { }

                    if (speakJson != null && speakJson["error"] == null)
                    {
                        return JsonConvert.SerializeObject(new
                        {
                            text_feedback = textFeedback,
                            speech_script = speechScript,
                            issues_used = issuesUsed,
                            style = style,
                            output_path = speakJson["output_path"]?.Value<string>(),
                            bytes = speakJson["bytes"]?.Value<int>(),
                            mime = speakJson["mime"]?.Value<string>()
                        });
                    }

                    return JsonConvert.SerializeObject(new
                    {
                        text_feedback = textFeedback,
                        speech_script = speechScript,
                        issues_used = issuesUsed,
                        style = style,
                        audio_error = speakJson?["error"]?.Value<string>() ?? speakResult
                    });
                }
            };
        }

        // ----------------------------------------------------------------
        // fs_read tool
        // ----------------------------------------------------------------

        private static LocalToolDef CreateFsReadTool(string workspaceDir)
        {
            return new LocalToolDef
            {
                Name = "fs_read",
                Description = "Read a text file from workspace.",
                Parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        path = new { type = "string", description = "File path within workspace" }
                    },
                    required = new[] { "path" },
                    additionalProperties = false
                },
                Handler = args =>
                {
                    string rawPath = args["path"]?.Value<string>() ?? string.Empty;
                    if (rawPath.StartsWith("workspace/", StringComparison.OrdinalIgnoreCase))
                        rawPath = rawPath.Substring("workspace/".Length);
                    if (rawPath.StartsWith("workspace\\", StringComparison.OrdinalIgnoreCase))
                        rawPath = rawPath.Substring("workspace\\".Length);

                    string fullPath = Path.GetFullPath(Path.Combine(workspaceDir, rawPath));
                    if (!fullPath.StartsWith(workspaceDir, StringComparison.OrdinalIgnoreCase))
                        return Task.FromResult(JsonConvert.SerializeObject(new { error = "Path traversal detected" }));

                    if (!File.Exists(fullPath))
                        return Task.FromResult(JsonConvert.SerializeObject(new { error = $"File not found: {rawPath}" }));

                    try
                    {
                        string content = File.ReadAllText(fullPath, Encoding.UTF8);
                        return Task.FromResult(content);
                    }
                    catch (Exception ex)
                    {
                        return Task.FromResult(JsonConvert.SerializeObject(new { error = ex.Message }));
                    }
                }
            };
        }

        // ----------------------------------------------------------------
        // fs_write tool
        // ----------------------------------------------------------------

        private static LocalToolDef CreateFsWriteTool(string workspaceDir)
        {
            return new LocalToolDef
            {
                Name = "fs_write",
                Description = "Write or append text to a file in workspace.",
                Parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        path = new { type = "string", description = "File path within workspace" },
                        content = new { type = "string", description = "Content to write" },
                        append = new { type = "boolean", description = "If true, append instead of overwrite" }
                    },
                    required = new[] { "path", "content" },
                    additionalProperties = false
                },
                Handler = args =>
                {
                    string rawPath = args["path"]?.Value<string>() ?? string.Empty;
                    if (rawPath.StartsWith("workspace/", StringComparison.OrdinalIgnoreCase))
                        rawPath = rawPath.Substring("workspace/".Length);
                    if (rawPath.StartsWith("workspace\\", StringComparison.OrdinalIgnoreCase))
                        rawPath = rawPath.Substring("workspace\\".Length);

                    string fullPath = Path.GetFullPath(Path.Combine(workspaceDir, rawPath));
                    if (!fullPath.StartsWith(workspaceDir, StringComparison.OrdinalIgnoreCase))
                        return Task.FromResult(JsonConvert.SerializeObject(new { error = "Path traversal detected" }));

                    string content = args["content"]?.Value<string>() ?? string.Empty;
                    bool append = args["append"]?.Value<bool>() ?? false;

                    try
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                        if (append)
                            File.AppendAllText(fullPath, content, Encoding.UTF8);
                        else
                            File.WriteAllText(fullPath, content, Encoding.UTF8);

                        return Task.FromResult(JsonConvert.SerializeObject(new
                        {
                            path = rawPath,
                            bytes = Encoding.UTF8.GetByteCount(content)
                        }));
                    }
                    catch (Exception ex)
                    {
                        return Task.FromResult(JsonConvert.SerializeObject(new { error = ex.Message }));
                    }
                }
            };
        }

        // ----------------------------------------------------------------
        // Config helpers
        // ----------------------------------------------------------------

        private static string GetModel()
        {
            string model = System.Configuration.ConfigurationManager.AppSettings["GEMINI_MODEL"]?.Trim();
            return string.IsNullOrEmpty(model) ? "gemini-3-flash-preview" : model;
        }

        private static string GetTtsModel()
        {
            string model = System.Configuration.ConfigurationManager.AppSettings["TTS_MODEL"]?.Trim();
            return string.IsNullOrEmpty(model) ? "gemini-2.5-flash-preview-tts" : model;
        }
    }
}
