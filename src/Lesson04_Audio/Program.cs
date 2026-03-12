using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using FourthDevs.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.Lesson04_Audio
{
    /// <summary>
    /// Lesson 04 – Audio
    /// Demonstrates audio processing with OpenAI APIs:
    ///   • Transcription — Whisper API  (POST /audio/transcriptions)
    ///   • Text-to-Speech — TTS API     (POST /audio/speech)
    ///
    /// Workflow:
    ///   1. If workspace/input/ contains .mp3/.wav/.m4a/.ogg files, transcribes them.
    ///   2. Generates a sample TTS phrase and saves it to workspace/output/tts_demo.mp3.
    ///
    /// Note: Transcription uses OpenAI directly (Whisper endpoint).
    ///       OpenRouter does not proxy the /audio/* endpoints.
    ///
    /// Source: 01_04_audio/app.js (i-am-alice/4th-devs)
    /// </summary>
    internal static class Program
    {
        private const string WhisperEndpoint = "https://api.openai.com/v1/audio/transcriptions";
        private const string TtsEndpoint     = "https://api.openai.com/v1/audio/speech";
        private const string WhisperModel    = "whisper-1";
        private const string TtsModel        = "tts-1";
        private const string TtsVoice        = "alloy";

        // Audio file extensions supported by Whisper
        private static readonly HashSet<string> AudioExtensions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".mp3", ".mp4", ".mpeg", ".mpga", ".m4a", ".wav", ".webm", ".ogg" };

        static void Main(string[] args)
        {
            MainAsync().GetAwaiter().GetResult();
        }

        static async Task MainAsync()
        {
            string exeDir   = AppDomain.CurrentDomain.BaseDirectory;
            string inputDir = Path.Combine(exeDir, "workspace", "input");
            string outDir   = Path.Combine(exeDir, "workspace", "output");
            Directory.CreateDirectory(inputDir);
            Directory.CreateDirectory(outDir);

            Console.WriteLine("=== Audio Processing Demo ===\n");

            // ----------------------------------------------------------------
            // Step 1 – Transcription: process any audio files in workspace/input/
            // ----------------------------------------------------------------
            Console.WriteLine("--- Transcription (Whisper) ---");
            string[] audioFiles = Directory.GetFiles(inputDir);
            bool anyAudio = false;

            foreach (string filePath in audioFiles)
            {
                if (!AudioExtensions.Contains(Path.GetExtension(filePath))) continue;
                anyAudio = true;

                Console.WriteLine("Transcribing: " + Path.GetFileName(filePath) + " ...");
                try
                {
                    string transcript = await TranscribeAsync(filePath);
                    Console.WriteLine("Transcript:\n" + transcript);

                    string outPath = Path.Combine(outDir,
                        Path.GetFileNameWithoutExtension(filePath) + "_transcript.txt");
                    File.WriteAllText(outPath, transcript, Encoding.UTF8);
                    Console.WriteLine("Saved: " + outPath);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("  Error: " + ex.Message);
                }
                Console.WriteLine();
            }

            if (!anyAudio)
            {
                Console.WriteLine("No audio files found in workspace/input/");
                Console.WriteLine("(Supported: .mp3 .wav .m4a .ogg .mp4 .webm)");
                Console.WriteLine();
            }

            // ----------------------------------------------------------------
            // Step 2 – Text-to-Speech: generate a demo MP3
            // ----------------------------------------------------------------
            Console.WriteLine("--- Text-to-Speech (TTS) ---");
            const string ttsText = "Hello! This is a demonstration of the OpenAI text-to-speech API " +
                                   "from the 4th-devs .NET 4.8 migration of lesson four.";

            Console.WriteLine("Generating speech ...");
            try
            {
                string ttsOutPath = Path.Combine(outDir, "tts_demo.mp3");
                await GenerateSpeechAsync(ttsText, ttsOutPath);
                Console.WriteLine("Saved: " + ttsOutPath);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("  Error: " + ex.Message);
                Console.Error.WriteLine("  (TTS requires an OpenAI key — set OPENAI_API_KEY in App.config)");
            }

            Console.WriteLine("\nDone.");
        }

        // ----------------------------------------------------------------
        // Whisper transcription
        // ----------------------------------------------------------------

        static async Task<string> TranscribeAsync(string filePath)
        {
            using (var http = BuildHttpClient())
            using (var form = new MultipartFormDataContent())
            {
                byte[] audioBytes = File.ReadAllBytes(filePath);
                var fileContent   = new ByteArrayContent(audioBytes);
                fileContent.Headers.ContentType =
                    new MediaTypeHeaderValue(GetAudioMime(filePath));

                form.Add(fileContent, "file", Path.GetFileName(filePath));
                form.Add(new StringContent(WhisperModel), "model");

                using (var response = await http.PostAsync(WhisperEndpoint, form))
                {
                    string body = await response.Content.ReadAsStringAsync();
                    if (!response.IsSuccessStatusCode)
                        throw new InvalidOperationException(
                            string.Format("Whisper error ({0}): {1}", (int)response.StatusCode, body));

                    var json = JObject.Parse(body);
                    return json["text"]?.ToString() ?? string.Empty;
                }
            }
        }

        // ----------------------------------------------------------------
        // TTS generation
        // ----------------------------------------------------------------

        static async Task GenerateSpeechAsync(string text, string outputPath)
        {
            var requestBody = JsonConvert.SerializeObject(new
            {
                model  = TtsModel,
                input  = text,
                voice  = TtsVoice,
                format = "mp3"
            });

            using (var http = BuildHttpClient())
            using (var content = new StringContent(requestBody, Encoding.UTF8, "application/json"))
            using (var response = await http.PostAsync(TtsEndpoint, content))
            {
                if (!response.IsSuccessStatusCode)
                {
                    string errorBody = await response.Content.ReadAsStringAsync();
                    throw new InvalidOperationException(
                        string.Format("TTS error ({0}): {1}", (int)response.StatusCode, errorBody));
                }

                byte[] mp3Bytes = await response.Content.ReadAsByteArrayAsync();
                File.WriteAllBytes(outputPath, mp3Bytes);
            }
        }

        // ----------------------------------------------------------------
        // HTTP helpers
        // ----------------------------------------------------------------

        static HttpClient BuildHttpClient()
        {
            // Audio endpoints are OpenAI-only — always use the OpenAI key directly
            string apiKey = System.Configuration.ConfigurationManager
                .AppSettings["OPENAI_API_KEY"]?.Trim() ?? AiConfig.ApiKey;

            var http = new HttpClient();
            http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);
            return http;
        }

        static string GetAudioMime(string filePath)
        {
            switch (Path.GetExtension(filePath).ToLowerInvariant())
            {
                case ".mp3":  return "audio/mpeg";
                case ".mp4":  return "audio/mp4";
                case ".mpeg":
                case ".mpga": return "audio/mpeg";
                case ".m4a":  return "audio/mp4";
                case ".wav":  return "audio/wav";
                case ".webm": return "audio/webm";
                case ".ogg":  return "audio/ogg";
                default:      return "audio/mpeg";
            }
        }
    }
}
