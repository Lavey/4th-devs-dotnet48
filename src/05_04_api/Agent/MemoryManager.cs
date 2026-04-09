using System;
using System.Collections.Generic;
using FourthDevs.MultiAgentApi.Db;
using FourthDevs.MultiAgentApi.Models;

namespace FourthDevs.MultiAgentApi.Agent
{
    /// <summary>
    /// Manages memory records: observations and reflections scoped to runs, threads, sessions, or agent profiles.
    /// </summary>
    internal sealed class MemoryManager
    {
        private readonly DatabaseManager _db;

        internal MemoryManager(DatabaseManager db)
        {
            _db = db;
        }

        /// <summary>
        /// Creates an observation memory record.
        /// </summary>
        internal MemoryRecord CreateObservation(string tenantId, string agentId, string threadId,
            string sessionId, string runId, string scope, string content, string keywords)
        {
            var record = new MemoryRecord
            {
                Id = IdGenerator.NewMemoryRecordId(),
                TenantId = tenantId,
                AgentId = agentId,
                ThreadId = threadId,
                SessionId = sessionId,
                RunId = runId,
                Scope = scope ?? "thread_shared",
                Type = "observation",
                Content = content,
                Keywords = keywords,
                Confidence = 1.0,
                CreatedAt = DatabaseManager.UtcNow(),
                UpdatedAt = DatabaseManager.UtcNow()
            };

            _db.InsertMemoryRecord(record);
            return record;
        }

        /// <summary>
        /// Creates a reflection memory record (higher-level insight).
        /// </summary>
        internal MemoryRecord CreateReflection(string tenantId, string agentId, string threadId,
            string sessionId, string runId, string scope, string content, string keywords, double confidence)
        {
            var record = new MemoryRecord
            {
                Id = IdGenerator.NewMemoryRecordId(),
                TenantId = tenantId,
                AgentId = agentId,
                ThreadId = threadId,
                SessionId = sessionId,
                RunId = runId,
                Scope = scope ?? "agent_profile",
                Type = "reflection",
                Content = content,
                Keywords = keywords,
                Confidence = confidence,
                CreatedAt = DatabaseManager.UtcNow(),
                UpdatedAt = DatabaseManager.UtcNow()
            };

            _db.InsertMemoryRecord(record);
            return record;
        }

        /// <summary>
        /// Gets all memory records for a thread (all scopes).
        /// </summary>
        internal List<MemoryRecord> GetThreadMemory(string threadId)
        {
            return _db.GetMemoryByThread(threadId);
        }

        /// <summary>
        /// Gets memory records for an agent, optionally filtered by scope.
        /// </summary>
        internal List<MemoryRecord> GetAgentMemory(string agentId, string scope)
        {
            return _db.GetMemoryByAgent(agentId, scope);
        }

        /// <summary>
        /// Builds a memory context string for injection into agent prompts.
        /// </summary>
        internal string BuildMemoryContext(string agentId, string threadId)
        {
            var memories = new List<MemoryRecord>();

            if (!string.IsNullOrWhiteSpace(threadId))
            {
                memories.AddRange(_db.GetMemoryByThread(threadId));
            }
            if (!string.IsNullOrWhiteSpace(agentId))
            {
                var agentMemories = _db.GetMemoryByAgent(agentId, "agent_profile");
                memories.AddRange(agentMemories);
            }

            if (memories.Count == 0) return string.Empty;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<memory>");
            foreach (var mem in memories)
            {
                sb.AppendFormat("  <{0} scope=\"{1}\" confidence=\"{2:F1}\">{3}</{0}>",
                    mem.Type, mem.Scope, mem.Confidence, mem.Content);
                sb.AppendLine();
            }
            sb.AppendLine("</memory>");
            return sb.ToString();
        }
    }
}
