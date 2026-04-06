using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FourthDevs.AgentGraph.Models;
using FourthDevs.AgentGraph.Store;
using Newtonsoft.Json.Linq;

namespace FourthDevs.AgentGraph.Core
{
    public sealed class Runtime
    {
        public string DataDir { get; }
        public FileStore<Session> Sessions { get; }
        public FileStore<Actor> Actors { get; }
        public FileStore<AgentTask> Tasks { get; }
        public FileStore<Item> Items { get; }
        public FileStore<Artifact> Artifacts { get; }
        public FileStore<Relation> Relations { get; }

        private int _seq;

        private Runtime(string dataDir, int initialSeq)
        {
            DataDir = dataDir;
            _seq = initialSeq;
            Sessions = new FileStore<Session>("sessions", dataDir);
            Actors = new FileStore<Actor>("actors", dataDir);
            Tasks = new FileStore<AgentTask>("tasks", dataDir);
            Items = new FileStore<Item>("items", dataDir);
            Artifacts = new FileStore<Artifact>("artifacts", dataDir);
            Relations = new FileStore<Relation>("relations", dataDir);
        }

        public static async Task<Runtime> Create(string dataDir = ".data")
        {
            var items = new FileStore<Item>("items", dataDir);
            var existing = await items.All();
            int maxSeq = existing.Count > 0 ? existing.Max(i => i.Sequence) : 0;
            return new Runtime(dataDir, maxSeq);
        }

        public int NextSequence() => ++_seq;
    }

    public static class RuntimeHelpers
    {
        public static async Task<Item> AddItem(Runtime rt, string sessionId, string type,
            JObject content, string taskId = null, string actorId = null)
        {
            return await rt.Items.Add(new Item
            {
                Id = DomainHelpers.NewId(),
                SessionId = sessionId,
                TaskId = taskId,
                ActorId = actorId,
                Type = type,
                Content = content,
                Sequence = rt.NextSequence(),
                CreatedAt = DomainHelpers.Now()
            });
        }

        public static async Task<Relation> AddRelation(Runtime rt, string sessionId,
            string fromKind, string fromId, string relationType, string toKind, string toId)
        {
            return await rt.Relations.Add(new Relation
            {
                Id = DomainHelpers.NewId(),
                SessionId = sessionId,
                FromKind = fromKind,
                FromId = fromId,
                RelationType = relationType,
                ToKind = toKind,
                ToId = toId,
                CreatedAt = DomainHelpers.Now()
            });
        }

        public static async Task<Artifact> AddArtifact(Runtime rt, string sessionId,
            string kind, string artifactPath, string taskId = null, JObject metadata = null)
        {
            return await rt.Artifacts.Add(new Artifact
            {
                Id = DomainHelpers.NewId(),
                SessionId = sessionId,
                TaskId = taskId,
                Kind = kind,
                Path = artifactPath,
                Version = 1,
                Metadata = metadata,
                CreatedAt = DomainHelpers.Now()
            });
        }

        public static async Task<Relation> EnsureRelation(Runtime rt, string sessionId,
            string fromKind, string fromId, string relationType, string toKind, string toId)
        {
            var existing = await rt.Relations.Find(r =>
                r.SessionId == sessionId
                && r.FromKind == fromKind && r.FromId == fromId
                && r.RelationType == relationType
                && r.ToKind == toKind && r.ToId == toId);
            if (existing.Count > 0) return existing[0];
            return await AddRelation(rt, sessionId, fromKind, fromId, relationType, toKind, toId);
        }
    }
}
