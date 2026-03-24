using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using FourthDevs.Events.Helpers;
using FourthDevs.Events.Models;

namespace FourthDevs.Events.Memory
{
    /// <summary>
    /// Orchestrates the observer/reflector memory cycle.
    /// </summary>
    internal static class Processor
    {
        private const int ObservationTokenBudget = 2000;
        private const int MinNewMessagesForObservation = 4;

        public static async Task<Session> ProcessMemory(Session session, string model)
        {
            if (session.Memory == null)
                session.Memory = new MemoryState();

            var mem = session.Memory;
            int newMessageCount = session.Messages.Count - mem.LastObservedIndex;

            // Not enough new messages, pass through
            if (newMessageCount < MinNewMessagesForObservation)
            {
                mem.ObserverRanThisRequest = false;
                return session;
            }

            // Run observer to extract observations
            var newObservations = await Observer.ExtractObservations(
                session.Messages, mem.LastObservedIndex, model);

            if (newObservations.Count > 0)
            {
                mem.ActiveObservations.AddRange(newObservations);
                mem.LastObservedIndex = session.Messages.Count;
                mem.ObserverRanThisRequest = true;
                mem.GenerationCount++;

                // Recalculate token count
                int totalTokens = 0;
                foreach (string obs in mem.ActiveObservations)
                {
                    totalTokens += TokenEstimator.Estimate(obs);
                }
                mem.ObservationTokenCount = totalTokens;

                // If over budget, run reflector
                if (mem.ObservationTokenCount > ObservationTokenBudget)
                {
                    Core.Logger.Info("memory", "Observations exceed budget (" +
                        mem.ObservationTokenCount + " > " + ObservationTokenBudget +
                        "), running reflector...");

                    mem.ActiveObservations = await Reflector.CompressObservations(
                        mem.ActiveObservations, model);

                    // Recalculate
                    totalTokens = 0;
                    foreach (string obs in mem.ActiveObservations)
                    {
                        totalTokens += TokenEstimator.Estimate(obs);
                    }
                    mem.ObservationTokenCount = totalTokens;
                }
            }
            else
            {
                mem.ObserverRanThisRequest = false;
            }

            return session;
        }
    }
}
