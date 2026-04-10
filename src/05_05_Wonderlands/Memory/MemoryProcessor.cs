using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FourthDevs.Wonderlands.Core;
using FourthDevs.Wonderlands.Models;

namespace FourthDevs.Wonderlands.Memory
{
    public class MemoryConfig
    {
        public int ObservationThresholdTokens { get; set; } = 800;
        public int ReflectionThresholdTokens { get; set; } = 1500;
        public int ReflectionTargetTokens { get; set; } = 800;
    }

    public static class MemoryProcessor
    {
        private static readonly MemoryConfig DefaultConfig = new MemoryConfig();
        private const double ActiveTailRatio = 0.3;
        private const int MinActiveTailTokens = 120;

        public static async Task ProcessRunMemory(string runId, Runtime rt, MemoryConfig config = null)
        {
            if (config == null) config = DefaultConfig;

            var run = await rt.Runs.GetById(runId);
            if (run == null) return;

            var memory = run.Memory ?? new MemoryState();

            var newItems = await rt.Items.Find(i => i.RunId == runId && i.Sequence > memory.LastObservedSeq);
            if (newItems.Count == 0) return;

            var runs = await rt.Runs.Find(r => r.SessionId == run.SessionId);
            var runAgents = runs.ToDictionary(r => r.Id, r => r.AgentName ?? "?");
            var pendingTokens = Observer.EstimateTokens(Observer.SerializeItems(newItems, runAgents));

            Log.MemoryStatus(newItems.Count, pendingTokens, memory.ObservationTokens, memory.Generation);

            if (pendingTokens < config.ObservationThresholdTokens)
            {
                Log.MemorySkipped(pendingTokens, config.ObservationThresholdTokens);
                return;
            }

            var memoryUsage = TokenUsage.Empty();

            var tailBudget = Math.Max(MinActiveTailTokens, (int)(config.ObservationThresholdTokens * ActiveTailRatio));
            var split = SplitByTailBudget(newItems, tailBudget, runAgents);
            var itemsToObserve = split.Head.Count > 0 ? split.Head : newItems;

            var observed = await Observer.RunObserver(memory.Observations, itemsToObserve, runAgents);
            memoryUsage = TokenUsage.Add(memoryUsage, observed.Usage);
            if (string.IsNullOrEmpty(observed.Observations))
            {
                await AccumulateMemoryUsage(run.SessionId, memoryUsage, rt);
                return;
            }

            var sealedThroughSeq = itemsToObserve.Max(i => i.Sequence);

            var merged = !string.IsNullOrEmpty(memory.Observations)
                ? memory.Observations.Trim() + "\n\n" + observed.Observations.Trim()
                : observed.Observations.Trim();

            var observationLines = observed.Observations.Split('\n').Count(l => !string.IsNullOrWhiteSpace(l));
            var observedTokens = Observer.EstimateTokens(observed.Observations);

            memory.Observations = merged;
            memory.LastObservedSeq = sealedThroughSeq;
            memory.ObservationTokens = Observer.EstimateTokens(merged);

            Log.MemoryObserved(itemsToObserve.Count, observationLines, observedTokens, sealedThroughSeq);

            PersistLog(rt.DataDir, "observer", observed.Observations, runId, run.SessionId, memory.Generation, observedTokens);

            if (memory.ObservationTokens > config.ReflectionThresholdTokens)
            {
                var tokensBefore = memory.ObservationTokens;
                try
                {
                    var reflected = await Reflector.RunReflector(memory.Observations, config.ReflectionTargetTokens);
                    memoryUsage = TokenUsage.Add(memoryUsage, reflected.Usage);
                    memory.Observations = reflected.Observations;
                    memory.ObservationTokens = reflected.TokenCount;
                    memory.Generation += 1;
                    Log.MemoryReflected(tokensBefore, reflected.TokenCount, reflected.CompressionLevel, memory.Generation);
                    PersistLog(rt.DataDir, "reflector", reflected.Observations, runId, run.SessionId, memory.Generation, reflected.TokenCount);
                }
                catch (Exception ex)
                {
                    Log.Warn("[memory] reflector failed: " + ex.Message);
                }
            }

            await rt.Runs.Update(run.Id, r => r.Memory = memory);
            await AccumulateMemoryUsage(run.SessionId, memoryUsage, rt);
        }

        private static async Task AccumulateMemoryUsage(string sessionId, TokenUsage usage, Runtime rt)
        {
            if (usage.TotalTokens == 0) return;
            var session = await rt.Sessions.GetById(sessionId);
            if (session == null) return;
            await rt.Sessions.Update(sessionId, s => s.Usage = TokenUsage.Add(s.Usage ?? TokenUsage.Empty(), usage));
        }

        private class SplitResult
        {
            public List<Item> Head { get; set; }
            public List<Item> Tail { get; set; }
        }

        private static SplitResult SplitByTailBudget(List<Item> items, int tailBudget, Dictionary<string, string> runAgents)
        {
            var sorted = items.OrderBy(i => i.Sequence).ToList();
            int tailTokens = 0;
            int splitIndex = sorted.Count;

            for (int i = sorted.Count - 1; i >= 0; i--)
            {
                var tokens = Observer.EstimateTokens(Observer.SerializeItems(new List<Item> { sorted[i] }, runAgents));
                if (tailTokens + tokens > tailBudget && splitIndex < sorted.Count) break;
                tailTokens += tokens;
                splitIndex = i;
            }

            while (splitIndex > 0 && splitIndex < sorted.Count)
            {
                if (sorted[splitIndex].Type == "result" && splitIndex > 0 && sorted[splitIndex - 1].Type == "invocation")
                {
                    splitIndex--;
                    continue;
                }
                break;
            }

            return new SplitResult
            {
                Head = sorted.Take(splitIndex).ToList(),
                Tail = sorted.Skip(splitIndex).ToList(),
            };
        }

        private static void PersistLog(string dataDir, string kind, string content, string runId, string sessionId, int generation, int tokens)
        {
            try
            {
                var dir = Path.Combine(dataDir, "memory_logs");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                var filename = string.Format("{0}_{1}_{2}_gen{3}.txt", kind, sessionId.Substring(0, 8), runId.Substring(0, 8), generation);
                File.WriteAllText(Path.Combine(dir, filename), content);
            }
            catch { }
        }
    }
}
