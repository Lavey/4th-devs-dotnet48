using System;
using System.Configuration;

namespace FourthDevs.Common
{
    /// <summary>
    /// Reads AI provider configuration from the application's App.config.
    /// Copy App.config.example to App.config in each project and fill in your API keys.
    /// </summary>
    public static class AiConfig
    {
        private const string EndpointOpenAi             = "https://api.openai.com/v1/responses";
        private const string EndpointOpenRouter         = "https://openrouter.ai/api/v1/responses";
        private const string EmbeddingsEndpointOpenAi     = "https://api.openai.com/v1/embeddings";
        private const string EmbeddingsEndpointOpenRouter = "https://openrouter.ai/api/v1/embeddings";

        public static string Provider             { get; }
        public static string ApiKey               { get; }
        public static string ApiEndpoint          { get; }
        public static string EmbeddingsEndpoint   { get; }
        public static string HttpReferer          { get; }
        public static string AppName              { get; }
        public static string GeminiApiKey         { get; }

        static AiConfig()
        {
            string openAiKey      = Get("OPENAI_API_KEY");
            string openRouterKey  = Get("OPENROUTER_API_KEY");
            string requested      = Get("AI_PROVIDER")?.ToLowerInvariant() ?? string.Empty;

            bool hasOpenAi      = !string.IsNullOrWhiteSpace(openAiKey);
            bool hasOpenRouter  = !string.IsNullOrWhiteSpace(openRouterKey);

            if (!hasOpenAi && !hasOpenRouter)
                throw new InvalidOperationException(
                    "No API key configured. Copy App.config.example to App.config " +
                    "and set either OPENAI_API_KEY or OPENROUTER_API_KEY.");

            if (!string.IsNullOrWhiteSpace(requested) &&
                requested != "openai" && requested != "openrouter")
                throw new InvalidOperationException(
                    "AI_PROVIDER must be \"openai\" or \"openrouter\".");

            if (requested == "openai" && !hasOpenAi)
                throw new InvalidOperationException("AI_PROVIDER=openai requires OPENAI_API_KEY.");
            if (requested == "openrouter" && !hasOpenRouter)
                throw new InvalidOperationException("AI_PROVIDER=openrouter requires OPENROUTER_API_KEY.");

            Provider = !string.IsNullOrWhiteSpace(requested)
                ? requested
                : (hasOpenAi ? "openai" : "openrouter");

            ApiKey             = Provider == "openai" ? openAiKey : openRouterKey;
            ApiEndpoint        = Provider == "openai" ? EndpointOpenAi : EndpointOpenRouter;
            EmbeddingsEndpoint = Provider == "openai" ? EmbeddingsEndpointOpenAi : EmbeddingsEndpointOpenRouter;
            HttpReferer  = Get("OPENROUTER_HTTP_REFERER");
            AppName      = Get("OPENROUTER_APP_NAME");
            GeminiApiKey = Get("GEMINI_API_KEY");
        }

        /// <summary>
        /// For OpenRouter, OpenAI-style model names (e.g. "gpt-4.1-mini") are
        /// prefixed with "openai/" to form a valid OpenRouter model identifier.
        /// </summary>
        public static string ResolveModel(string model)
        {
            if (string.IsNullOrWhiteSpace(model))
                throw new ArgumentException("Model name must not be empty.", nameof(model));

            if (Provider != "openrouter" || model.Contains("/"))
                return model;

            return model.StartsWith("gpt-", StringComparison.OrdinalIgnoreCase)
                ? "openai/" + model
                : model;
        }

        private static string Get(string key)
        {
            return ConfigurationManager.AppSettings[key]?.Trim() ?? string.Empty;
        }
    }
}
