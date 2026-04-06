using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FourthDevs.AgentGraph.Core;
using FourthDevs.AgentGraph.Models;

namespace FourthDevs.AgentGraph.Memory
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
        private static int _observerLogCounter;
        private static int _reflectorLogCounter;

        public static async Task ProcessTaskMemory(string taskId, Runtime rt, MemoryConfig config = null)
        {
            if (config == null) config = DefaultConfig;

            var task = await rt.Tasks.GetById(taskId);
            if (task == null) return;

            var memory = task.Memory ?? new MemoryState();

            var newItems = await rt.Items.Find(i => i.TaskId == taskId && i.Sequence > memory.LastObservedSeq);
            if (newItems.Count == 0) return;

            var actors = await rt.Actors.Find(a => a.SessionId == task.SessionId);
            var actorNames = actors.ToDictionary(a => a.Id, a => a.Name);
            var pendingTokens = Observer.EstimateTokens(Observer.SerializeItems(newItems, actorNames));

            Log.MemoryStatus(newItems.Count, pendingTokens, memory.ObservationTokens, memory.Generation);

            if (pendingTokens < config.ObservationThresholdTokens)
            {
                Log.MemorySkipped(pendingTokens, config.ObservationThresholdTokens);
                return;
            }

            // ── Observe ──────────────────────────────────────────────
            var memoryUsage = TokenUsage.Empty();

            var tailBudget = Math.Max(MinActiveTailTokens, (int)(config.ObservationThresholdTokens * ActiveTailRatio));
            SplitResult split = SplitByTailBudget(newItems, tailBudget, actorNames);
            var itemsToObserve = split.Head.Count > 0 ? split.Head : newItems;

            var observed = await Observer.RunObserver(memory.Observations, itemsToObserve, actorNames);
            memoryUsage = TokenUsage.Add(memoryUsage, observed.Usage);
            if (string.IsNullOrEmpty(observed.Observations))
            {
                await AccumulateMemoryUsage(task.SessionId, memoryUsage, rt);
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

            PersistLog(rt.DataDir, "observer", observed.Observations, taskId, task.SessionId, memory.Generation, observedTokens);

            // ── Reflect ──────────────────────────────────────────────
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
                    PersistLog(rt.DataDir, "reflector", reflected.Observations, taskId, task.SessionId, memory.Generation, reflected.TokenCount);
                }
                catch (Exception ex)
                {
                    Log.Warn("[memory] reflector failed: " + ex.Message);
                }
            }

            // ── Persist ──────────────────────────────────────────────
            await rt.Tasks.Update(task.Id, t => t.Memory = memory);
            await AccumulateMemoryUsage(task.SessionId, memoryUsage, rt);
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

        private static SplitResult SplitByTailBudget(List<Item> items, int tailBudget, Dictionary<string, string> actorNames)
        {
            var sorted = items.OrderBy(i => i.Sequence).ToList();
            int tailTokens = 0;
            int splitIndex = sorted.Count;

            for (int i = sorted.Count - 1; i >= 0; i--)
            {
                var tokens = Observer.EstimateTokens(Observer.SerializeItems(new List<Item> { sorted[i] }, actorNames));
                if (tailTokens + tokens > tailBudget && splitIndex < sorted.Count) break;
                tailTokens += tokens;
                splitIndex = i;
            }

            // Don't split invocation/result pairs
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

        private static void PersistLog(string dataDir, string type, string content, string taskId, string sessionId, int generation, int tokens)
        {
            try
            {
                int counter = type == "observer" ? ++_observerLogCounter : ++_reflectorLogCounter;
                var filename = string.Format("{0}-{1:D3}.md", type, counter);
                var filePath = Path.Combine(dataDir, "memory", filename);
                var dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var header = string.Format("---\nsession: {0}\ntask: {1}\ngeneration: {2}\ntokens: {3}\ncreated: {4}\n---\n\n{5}\n",
                    sessionId, taskId, generation, tokens, DateTime.UtcNow.ToString("o"), content);
                File.WriteAllText(filePath, header);
                Log.MemoryPersisted(filename);
            }
            catch { /* best-effort */ }
        }
    }
}
