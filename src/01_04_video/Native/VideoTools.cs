using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FourthDevs.Video.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Video.Native
{
    /// <summary>
    /// Defines the four video tool functions exposed to the OpenAI model,
    /// and implements their execution via GeminiVideoClient.
    /// </summary>
    internal static class VideoTools
    {
        private const string OutputDir = "workspace/output";

        public static List<VideoToolDefinition> CreateTools()
        {
            return new List<VideoToolDefinition>
            {
                BuildAnalyzeTool(),
                BuildTranscribeTool(),
                BuildExtractTool(),
                BuildQueryTool()
            };
        }

        // ----------------------------------------------------------------
        // analyze_video
        // ----------------------------------------------------------------

        private static VideoToolDefinition BuildAnalyzeTool()
        {
            return new VideoToolDefinition
            {
                Name        = "analyze_video",
                Description = "Analyze video content. Choose analysis_type for focus: 'general' (overview), " +
                              "'visual' (scenes, colors, objects), 'audio' (sounds, music, speech), " +
                              "'action' (activities, movements, events). Supports YouTube URLs and local files.",
                Parameters  = JObject.Parse(@"{
  ""type"": ""object"",
  ""properties"": {
    ""video_path"": {
      ""type"": ""string"",
      ""description"": ""YouTube URL or path to a local video file (e.g. workspace/input/video.mp4)""
    },
    ""analysis_type"": {
      ""type"": ""string"",
      ""enum"": [""general"", ""visual"", ""audio"", ""action""],
      ""description"": ""Type of analysis to perform. Defaults to 'general'.""
    },
    ""custom_prompt"": {
      ""type"": ""string"",
      ""description"": ""Optional custom analysis prompt that overrides the default.""
    },
    ""start_time"": {
      ""type"": ""string"",
      ""description"": ""Start offset for clipping, e.g. '30s' or '1m30s'.""
    },
    ""end_time"": {
      ""type"": ""string"",
      ""description"": ""End offset for clipping, e.g. '90s' or '2m'.""
    },
    ""fps"": {
      ""type"": ""number"",
      ""description"": ""Frames per second to sample (e.g. 1.0 for one frame per second).""
    },
    ""output_name"": {
      ""type"": ""string"",
      ""description"": ""Optional filename (without extension) to save the result in workspace/output/.""
    }
  },
  ""required"": [""video_path""],
  ""additionalProperties"": false
}"),
                Handler = async (args) =>
                {
                    string videoPath     = args["video_path"]?.ToString() ?? string.Empty;
                    string analysisType  = args["analysis_type"]?.ToString() ?? "general";
                    string customPrompt  = args["custom_prompt"]?.ToString();
                    string startTime     = args["start_time"]?.ToString();
                    string endTime       = args["end_time"]?.ToString();
                    double? fps          = args["fps"]?.Type == JTokenType.Null ? (double?)null : args["fps"]?.Value<double>();
                    string outputName    = args["output_name"]?.ToString();

                    string prompt = !string.IsNullOrWhiteSpace(customPrompt)
                        ? customPrompt
                        : BuildAnalysisPrompt(analysisType);

                    LogTool("analyze_video", videoPath, analysisType);

                    string result = await GeminiVideoClient.ProcessVideoAsync(
                        videoPath, prompt, startTime, endTime, fps);

                    if (!string.IsNullOrWhiteSpace(outputName))
                        SaveOutput(outputName + "_analysis.txt", result);

                    return new { analysis = result, video = videoPath, type = analysisType };
                }
            };
        }

        // ----------------------------------------------------------------
        // transcribe_video
        // ----------------------------------------------------------------

        private static VideoToolDefinition BuildTranscribeTool()
        {
            return new VideoToolDefinition
            {
                Name        = "transcribe_video",
                Description = "Transcribe speech from a video with optional timestamps and speaker detection. " +
                              "Returns structured JSON. Supports YouTube URLs and local files.",
                Parameters  = JObject.Parse(@"{
  ""type"": ""object"",
  ""properties"": {
    ""video_path"": {
      ""type"": ""string"",
      ""description"": ""YouTube URL or path to a local video file.""
    },
    ""include_timestamps"": {
      ""type"": ""boolean"",
      ""description"": ""Whether to include MM:SS timestamps for each segment. Defaults to true.""
    },
    ""detect_speakers"": {
      ""type"": ""boolean"",
      ""description"": ""Whether to identify different speakers. Defaults to false.""
    },
    ""translate_to"": {
      ""type"": ""string"",
      ""description"": ""Optional language to translate the transcription to (e.g. 'English', 'Polish').""
    },
    ""start_time"": {
      ""type"": ""string"",
      ""description"": ""Start offset for clipping, e.g. '30s'.""
    },
    ""end_time"": {
      ""type"": ""string"",
      ""description"": ""End offset for clipping, e.g. '90s'.""
    },
    ""output_name"": {
      ""type"": ""string"",
      ""description"": ""Optional filename (without extension) to save the result in workspace/output/.""
    }
  },
  ""required"": [""video_path""],
  ""additionalProperties"": false
}"),
                Handler = async (args) =>
                {
                    string videoPath         = args["video_path"]?.ToString() ?? string.Empty;
                    bool   includeTimestamps = args["include_timestamps"]?.Value<bool>() ?? true;
                    bool   detectSpeakers    = args["detect_speakers"]?.Value<bool>() ?? false;
                    string translateTo       = args["translate_to"]?.ToString();
                    string startTime         = args["start_time"]?.ToString();
                    string endTime           = args["end_time"]?.ToString();
                    string outputName        = args["output_name"]?.ToString();

                    string prompt = BuildTranscriptionPrompt(includeTimestamps, detectSpeakers, translateTo);
                    LogTool("transcribe_video", videoPath);

                    string rawResult = await GeminiVideoClient.ProcessVideoAsync(
                        videoPath, prompt, startTime, endTime, wantJson: true);

                    JToken parsed = TryParseJson(rawResult);
                    if (!string.IsNullOrWhiteSpace(outputName))
                        SaveOutput(outputName + "_transcription.json", rawResult);

                    return new { transcription = parsed ?? (object)rawResult, video = videoPath };
                }
            };
        }

        // ----------------------------------------------------------------
        // extract_video
        // ----------------------------------------------------------------

        private static VideoToolDefinition BuildExtractTool()
        {
            return new VideoToolDefinition
            {
                Name        = "extract_video",
                Description = "Extract structured information from a video: 'scenes' (scene boundaries and descriptions), " +
                              "'keyframes' (important moments with timestamps), 'objects' (detected objects), " +
                              "'text' (on-screen text / captions). Returns JSON.",
                Parameters  = JObject.Parse(@"{
  ""type"": ""object"",
  ""properties"": {
    ""video_path"": {
      ""type"": ""string"",
      ""description"": ""YouTube URL or path to a local video file.""
    },
    ""extraction_type"": {
      ""type"": ""string"",
      ""enum"": [""scenes"", ""keyframes"", ""objects"", ""text""],
      ""description"": ""What to extract. Defaults to 'scenes'.""
    },
    ""start_time"": {
      ""type"": ""string"",
      ""description"": ""Start offset for clipping, e.g. '30s'.""
    },
    ""end_time"": {
      ""type"": ""string"",
      ""description"": ""End offset for clipping, e.g. '90s'.""
    },
    ""fps"": {
      ""type"": ""number"",
      ""description"": ""Frames per second to sample.""
    },
    ""output_name"": {
      ""type"": ""string"",
      ""description"": ""Optional filename (without extension) to save the result in workspace/output/.""
    }
  },
  ""required"": [""video_path""],
  ""additionalProperties"": false
}"),
                Handler = async (args) =>
                {
                    string videoPath      = args["video_path"]?.ToString() ?? string.Empty;
                    string extractionType = args["extraction_type"]?.ToString() ?? "scenes";
                    string startTime      = args["start_time"]?.ToString();
                    string endTime        = args["end_time"]?.ToString();
                    double? fps           = args["fps"]?.Type == JTokenType.Null ? (double?)null : args["fps"]?.Value<double>();
                    string outputName     = args["output_name"]?.ToString();

                    string prompt = BuildExtractionPrompt(extractionType);
                    LogTool("extract_video", videoPath, extractionType);

                    string rawResult = await GeminiVideoClient.ProcessVideoAsync(
                        videoPath, prompt, startTime, endTime, fps, wantJson: true);

                    JToken parsed = TryParseJson(rawResult);
                    if (!string.IsNullOrWhiteSpace(outputName))
                        SaveOutput(outputName + "_" + extractionType + ".json", rawResult);

                    return new { extraction = parsed ?? (object)rawResult, video = videoPath, type = extractionType };
                }
            };
        }

        // ----------------------------------------------------------------
        // query_video
        // ----------------------------------------------------------------

        private static VideoToolDefinition BuildQueryTool()
        {
            return new VideoToolDefinition
            {
                Name        = "query_video",
                Description = "Ask any custom question about video content. " +
                              "Supports YouTube URLs and local files. Returns a detailed text answer.",
                Parameters  = JObject.Parse(@"{
  ""type"": ""object"",
  ""properties"": {
    ""video_path"": {
      ""type"": ""string"",
      ""description"": ""YouTube URL or path to a local video file.""
    },
    ""question"": {
      ""type"": ""string"",
      ""description"": ""The question to ask about the video.""
    },
    ""start_time"": {
      ""type"": ""string"",
      ""description"": ""Start offset for clipping, e.g. '30s'.""
    },
    ""end_time"": {
      ""type"": ""string"",
      ""description"": ""End offset for clipping, e.g. '90s'.""
    }
  },
  ""required"": [""video_path"", ""question""],
  ""additionalProperties"": false
}"),
                Handler = async (args) =>
                {
                    string videoPath = args["video_path"]?.ToString() ?? string.Empty;
                    string question  = args["question"]?.ToString() ?? string.Empty;
                    string startTime = args["start_time"]?.ToString();
                    string endTime   = args["end_time"]?.ToString();

                    LogTool("query_video", videoPath, question);

                    string result = await GeminiVideoClient.ProcessVideoAsync(
                        videoPath, question, startTime, endTime);

                    return new { answer = result, video = videoPath, question = question };
                }
            };
        }

        // ----------------------------------------------------------------
        // Prompt builders
        // ----------------------------------------------------------------

        private static string BuildAnalysisPrompt(string analysisType)
        {
            switch (analysisType?.ToLowerInvariant())
            {
                case "visual":
                    return "Analyze the visual content of this video in detail. Describe the scenes, " +
                           "colors, lighting, visual style, and key objects or people visible. " +
                           "Reference specific timestamps in MM:SS format.";
                case "audio":
                    return "Analyze the audio content of this video. Describe the speech, music, " +
                           "sound effects, background noise, and overall audio quality. " +
                           "Reference specific timestamps in MM:SS format.";
                case "action":
                    return "Analyze the actions and events in this video. Describe what happens, " +
                           "the activities performed, movements, and key events in chronological order. " +
                           "Reference specific timestamps in MM:SS format.";
                default:
                    return "Provide a comprehensive analysis of this video. Describe the content, " +
                           "key themes, visual style, audio, main events, and any notable elements. " +
                           "Reference specific timestamps in MM:SS format.";
            }
        }

        private static string BuildTranscriptionPrompt(bool includeTimestamps, bool detectSpeakers, string translateTo)
        {
            string prompt = "Transcribe all speech in this video accurately.";

            if (includeTimestamps)
                prompt += " Include MM:SS timestamps at the start of each segment.";

            if (detectSpeakers)
                prompt += " Identify different speakers and label them (e.g. Speaker 1, Speaker 2).";

            if (!string.IsNullOrWhiteSpace(translateTo))
                prompt += " Translate the transcription to " + translateTo + ".";

            prompt += " Return a JSON object with a 'segments' array. Each segment should have: " +
                      "'timestamp' (MM:SS), 'speaker' (if applicable), and 'text' fields.";

            return prompt;
        }

        private static string BuildExtractionPrompt(string extractionType)
        {
            switch (extractionType?.ToLowerInvariant())
            {
                case "keyframes":
                    return "Identify the most important frames/moments in this video. " +
                           "Return a JSON object with a 'keyframes' array. Each item should have: " +
                           "'timestamp' (MM:SS), 'description' (what is shown), and 'importance' (why it matters).";
                case "objects":
                    return "Detect and list all significant objects, people, and items visible in this video. " +
                           "Return a JSON object with an 'objects' array. Each item should have: " +
                           "'name', 'description', and 'first_seen' (MM:SS timestamp).";
                case "text":
                    return "Extract all text visible on screen in this video (captions, titles, signs, overlays, etc.). " +
                           "Return a JSON object with a 'text_segments' array. Each item should have: " +
                           "'timestamp' (MM:SS), 'text' (exact text), and 'location' (where on screen).";
                default:
                    return "Identify all distinct scenes in this video. " +
                           "Return a JSON object with a 'scenes' array. Each scene should have: " +
                           "'start_time' (MM:SS), 'end_time' (MM:SS), 'description' (what happens), " +
                           "and 'location' (setting/environment).";
            }
        }

        // ----------------------------------------------------------------
        // Output helpers
        // ----------------------------------------------------------------

        private static void SaveOutput(string filename, string content)
        {
            try
            {
                Directory.CreateDirectory(OutputDir);
                string path = Path.Combine(OutputDir, filename);
                File.WriteAllText(path, content, System.Text.Encoding.UTF8);
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("[tool] Saved result to " + path);
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine("[tool] Could not save output: " + ex.Message);
                Console.ResetColor();
            }
        }

        private static JToken TryParseJson(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            try { return JToken.Parse(text); }
            catch { }

            // Strip markdown code fences
            int start = text.IndexOf('{');
            int end   = text.LastIndexOf('}');
            if (start >= 0 && end > start)
            {
                try { return JToken.Parse(text.Substring(start, end - start + 1)); }
                catch { }
            }
            return null;
        }

        private static void LogTool(string tool, string video, string detail = null)
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            if (detail != null)
                Console.WriteLine("[tool] " + tool + " | " + detail + " | " + video);
            else
                Console.WriteLine("[tool] " + tool + " | " + video);
            Console.ResetColor();
        }
    }
}
