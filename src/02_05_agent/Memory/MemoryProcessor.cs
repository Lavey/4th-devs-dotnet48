using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FourthDevs.ContextAgent.Memory
{
    internal static class MemoryProcessor
    {
        private static string _workspaceRoot;

        public static void Init(string workspaceRoot)
        {
            _workspaceRoot = workspaceRoot;
        }

        /// <summary>
        /// Main memory processing entry point. Called before each LLM call.
        /// Runs observer and/or reflector as needed, returns context.
        /// </summary>
        public static async Task<MemoryContext> ProcessAsync(
            string baseSystemPrompt,
            Session session)
        {
            var memory = session.Memory;
            var allMessages = session.Messages;

            // Count unobserved messages
            int unobservedCount = allMessages.Count - memory.LastObservedIndex;
            if (unobservedCount <= 0)
            {
                return MemoryContextBuilder.Build(baseSystemPrompt, session);
            }

            // Estimate tokens in unobserved messages
            var unobserved = allMessages.GetRange(memory.LastObservedIndex, unobservedCount);
            int unobservedTokens = EstimateTokens(SerializeForEstimate(unobserved));

            bool shouldObserve = unobservedTokens >= MemoryConfig.ObservationThresholdTokens
                && !memory.ObserverRanThisRequest;

            if (shouldObserve)
            {
                await RunObserverAsync(session, unobserved).ConfigureAwait(false);
            }

            // Check if reflection needed
            if (memory.ObservationTokenCount >= MemoryConfig.ReflectionThresholdTokens)
            {
                await RunReflectorAsync(session).ConfigureAwait(false);
            }

            return MemoryContextBuilder.Build(baseSystemPrompt, session);
        }

        public static async Task FlushAsync(Session session)
        {
            var memory = session.Memory;
            var allMessages = session.Messages;

            int unobservedCount = allMessages.Count - memory.LastObservedIndex;
            if (unobservedCount > 0)
            {
                var unobserved = allMessages.GetRange(memory.LastObservedIndex, unobservedCount);
                await RunObserverAsync(session, unobserved).ConfigureAwait(false);
            }

            if (memory.ObservationTokenCount >= MemoryConfig.ReflectionThresholdTokens)
            {
                await RunReflectorAsync(session).ConfigureAwait(false);
            }
        }

        private static async Task RunObserverAsync(Session session, List<JObject> messagesToObserve)
        {
            var memory = session.Memory;
            int startIndex = memory.LastObservedIndex;
            int endIndex = startIndex + messagesToObserve.Count - 1;

            var result = await Observer.RunAsync(messagesToObserve, memory.ActiveObservations)
                .ConfigureAwait(false);

            string newObs = result.Observations;
            if (!string.IsNullOrEmpty(memory.ActiveObservations) && !string.IsNullOrEmpty(newObs))
                memory.ActiveObservations = memory.ActiveObservations + "\n" + newObs;
            else if (!string.IsNullOrEmpty(newObs))
                memory.ActiveObservations = newObs;

            memory.LastObservedIndex += messagesToObserve.Count;
            memory.ObservationTokenCount = EstimateTokens(memory.ActiveObservations);
            memory.ObserverRanThisRequest = true;
            memory.ObserverLogSeq++;

            // Persist log
            await WriteLogAsync(
                session.Id + "_observer_" + memory.ObserverLogSeq + ".json",
                new JObject
                {
                    ["sessionId"] = session.Id,
                    ["sequence"] = memory.ObserverLogSeq,
                    ["observations"] = memory.ActiveObservations,
                    ["tokens"] = memory.ObservationTokenCount,
                    ["messagesObserved"] = messagesToObserve.Count,
                    ["generation"] = memory.GenerationCount,
                    ["sealedRange"] = new JArray(startIndex, endIndex),
                    ["timestamp"] = DateTime.UtcNow.ToString("o")
                }).ConfigureAwait(false);
        }

        private static async Task RunReflectorAsync(Session session)
        {
            var memory = session.Memory;
            var result = await Reflector.RunAsync(memory.ActiveObservations).ConfigureAwait(false);

            memory.ActiveObservations = result.Observations;
            memory.ObservationTokenCount = EstimateTokens(memory.ActiveObservations);
            memory.GenerationCount++;
            memory.LastReflectionOutputTokens = memory.ObservationTokenCount;
            memory.ReflectorLogSeq++;

            await WriteLogAsync(
                session.Id + "_reflector_" + memory.ReflectorLogSeq + ".json",
                new JObject
                {
                    ["sessionId"] = session.Id,
                    ["sequence"] = memory.ReflectorLogSeq,
                    ["observations"] = memory.ActiveObservations,
                    ["tokens"] = memory.ObservationTokenCount,
                    ["generation"] = memory.GenerationCount,
                    ["compressionLevel"] = memory.ReflectorLogSeq - 1,
                    ["timestamp"] = DateTime.UtcNow.ToString("o")
                }).ConfigureAwait(false);
        }

        private static async Task WriteLogAsync(string filename, JObject data)
        {
            if (string.IsNullOrEmpty(_workspaceRoot)) return;
            try
            {
                string dir = Path.Combine(_workspaceRoot, "memory");
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, filename);
                string json = data.ToString(Formatting.Indented);
                using (var sw = new StreamWriter(path, false, System.Text.Encoding.UTF8))
                {
                    await sw.WriteAsync(json).ConfigureAwait(false);
                }
            }
            catch
            {
                // Log persistence is best-effort
            }
        }

        private static int EstimateTokens(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            return text.Length / 4;
        }

        private static string SerializeForEstimate(List<JObject> messages)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var m in messages)
                sb.Append(m.ToString(Formatting.None));
            return sb.ToString();
        }
    }
}
