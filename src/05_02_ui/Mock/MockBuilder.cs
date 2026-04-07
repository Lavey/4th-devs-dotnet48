using System;
using System.Collections.Generic;
using FourthDevs.ChatUi.Models;
using Newtonsoft.Json.Linq;

namespace FourthDevs.ChatUi.Mock
{
    /// <summary>
    /// Fluent builder for constructing mock event streams.
    /// </summary>
    internal sealed class MockBuilder
    {
        private readonly List<DelayedEvent> _events = new List<DelayedEvent>();
        private readonly string _messageId;
        private int _seq;

        public MockBuilder(string messageId)
        {
            _messageId = messageId;
        }

        public List<DelayedEvent> Build()
        {
            return _events;
        }

        public MockBuilder Start(string title = null)
        {
            var e = Create<AssistantMessageStartEvent>();
            e.Title = title;
            _events.Add(new DelayedEvent(e, 0));
            return this;
        }

        public MockBuilder ThinkingStart(string label = null, int delayMs = 100)
        {
            var e = Create<ThinkingStartEvent>();
            e.Label = label ?? "Thinking...";
            _events.Add(new DelayedEvent(e, delayMs));
            return this;
        }

        public MockBuilder ThinkingDelta(string text, int delayMs = 30)
        {
            var e = Create<ThinkingDeltaEvent>();
            e.TextDelta = text;
            _events.Add(new DelayedEvent(e, delayMs));
            return this;
        }

        public MockBuilder ThinkingEnd(int delayMs = 50)
        {
            _events.Add(new DelayedEvent(Create<ThinkingEndEvent>(), delayMs));
            return this;
        }

        public MockBuilder TextDelta(string text, int delayMs = 20)
        {
            var e = Create<TextDeltaEvent>();
            e.TextDelta = text;
            _events.Add(new DelayedEvent(e, delayMs));
            return this;
        }

        public MockBuilder TextChunked(string fullText, int chunkSize = 4, int delayMs = 15)
        {
            for (int i = 0; i < fullText.Length; i += chunkSize)
            {
                int len = Math.Min(chunkSize, fullText.Length - i);
                TextDelta(fullText.Substring(i, len), delayMs);
            }
            return this;
        }

        public MockBuilder ToolCall(string toolCallId, string name, JObject args, int delayMs = 100)
        {
            var e = Create<ToolCallEvent>();
            e.ToolCallId = toolCallId;
            e.Name = name;
            e.Args = args;
            _events.Add(new DelayedEvent(e, delayMs));
            return this;
        }

        public MockBuilder ToolResult(string toolCallId, bool ok, JObject output, int delayMs = 300)
        {
            var e = Create<ToolResultEvent>();
            e.ToolCallId = toolCallId;
            e.Ok = ok;
            e.Output = output;
            _events.Add(new DelayedEvent(e, delayMs));
            return this;
        }

        public MockBuilder Artifact(string id, string kind, string title,
            string description, string path, string preview, int delayMs = 200)
        {
            var e = Create<ArtifactEvent>();
            e.ArtifactId = id;
            e.Kind = kind;
            e.Title = title;
            e.Description = description;
            e.Path = path;
            e.Preview = preview;
            _events.Add(new DelayedEvent(e, delayMs));
            return this;
        }

        public MockBuilder Error(string message, int delayMs = 0)
        {
            var e = Create<ErrorEvent>();
            e.Message = message;
            _events.Add(new DelayedEvent(e, delayMs));
            return this;
        }

        public MockBuilder Complete(string finishReason = "stop", int delayMs = 50)
        {
            var e = Create<CompleteEvent>();
            e.FinishReason = finishReason;
            _events.Add(new DelayedEvent(e, delayMs));
            return this;
        }

        private T Create<T>() where T : BaseStreamEvent, new()
        {
            var e = new T();
            e.Id = Guid.NewGuid().ToString("N").Substring(0, 12);
            e.MessageId = _messageId;
            e.Seq = ++_seq;
            e.At = DateTime.UtcNow.ToString("o");
            return e;
        }
    }

    internal sealed class DelayedEvent
    {
        public readonly BaseStreamEvent Event;
        public readonly int DelayMs;

        public DelayedEvent(BaseStreamEvent evt, int delayMs)
        {
            Event = evt;
            DelayMs = delayMs;
        }
    }
}
