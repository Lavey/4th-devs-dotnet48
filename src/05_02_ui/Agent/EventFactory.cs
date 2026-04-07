using System;
using FourthDevs.ChatUi.Models;

namespace FourthDevs.ChatUi.Agent
{
    /// <summary>
    /// Factory for creating stream events with incrementing sequence numbers.
    /// Each instance is scoped to a single assistant message / turn.
    /// </summary>
    internal sealed class EventFactory
    {
        private int _seq;
        private readonly string _messageId;

        public EventFactory(string messageId)
        {
            _messageId = messageId;
        }

        public T Create<T>() where T : BaseStreamEvent, new()
        {
            var e = new T();
            e.Id = Guid.NewGuid().ToString("N").Substring(0, 12);
            e.MessageId = _messageId;
            e.Seq = ++_seq;
            e.At = DateTime.UtcNow.ToString("o");
            return e;
        }
    }
}
