using System.Configuration;

namespace FourthDevs.VoiceAgent.Core
{
    /// <summary>
    /// Describes the detected voice pipeline mode based on available API keys.
    /// </summary>
    internal sealed class VoiceMode
    {
        public string Status { get; set; }
        public string Id { get; set; }
        public string Label { get; set; }
        public string Description { get; set; }
        public string Error { get; set; }
    }

    /// <summary>
    /// Resolves voice mode from App.config keys, mirroring the TypeScript
    /// logic that selects Gemini / ElevenLabs / OpenAI based on available
    /// environment variables.
    /// </summary>
    internal static class VoiceModeResolver
    {
        public static VoiceMode Resolve()
        {
            string googleKey = GetGoogleApiKey();
            string elevenKey = Get("ELEVEN_API_KEY");
            string openAiKey = Get("OPENAI_API_KEY");

            bool hasGoogle = !string.IsNullOrWhiteSpace(googleKey);
            bool hasEleven = !string.IsNullOrWhiteSpace(elevenKey);
            bool hasOpenAi = !string.IsNullOrWhiteSpace(openAiKey);

            if (hasGoogle)
            {
                return new VoiceMode
                {
                    Status = "ready",
                    Id = "gemini",
                    Label = "Gemini Realtime",
                    Description = "Google Gemini multimodal realtime voice mode"
                };
            }

            if (hasEleven && hasOpenAi)
            {
                return new VoiceMode
                {
                    Status = "ready",
                    Id = "elevenlabs",
                    Label = "ElevenLabs + OpenAI",
                    Description = "ElevenLabs TTS with OpenAI LLM backend"
                };
            }

            if (hasOpenAi)
            {
                return new VoiceMode
                {
                    Status = "ready",
                    Id = "openai",
                    Label = "OpenAI Realtime",
                    Description = "OpenAI realtime voice mode"
                };
            }

            return new VoiceMode
            {
                Status = "invalid",
                Id = "invalid",
                Label = "No provider",
                Description = "No voice provider configured",
                Error = "Set GOOGLE_API_KEY / GEMINI_API_KEY (Gemini), " +
                        "ELEVEN_API_KEY + OPENAI_API_KEY (ElevenLabs), " +
                        "or OPENAI_API_KEY (OpenAI) in App.config."
            };
        }

        /// <summary>
        /// Returns the Google / Gemini API key checking multiple possible names.
        /// </summary>
        public static string GetGoogleApiKey()
        {
            string key = Get("GOOGLE_API_KEY");
            if (!string.IsNullOrWhiteSpace(key)) return key;

            key = Get("GOOGLE_GENAI_API_KEY");
            if (!string.IsNullOrWhiteSpace(key)) return key;

            key = Get("GEMINI_API_KEY");
            return key;
        }

        private static string Get(string name)
        {
            return (ConfigurationManager.AppSettings[name] ?? string.Empty).Trim();
        }
    }
}
