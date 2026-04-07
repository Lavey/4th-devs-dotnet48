using System;
using System.Collections.Generic;
using FourthDevs.ChatUi.Mock;
using FourthDevs.ChatUi.Models;

namespace FourthDevs.ChatUi.Store
{
    /// <summary>
    /// Manages the conversation state, mode switching, and turn creation.
    /// Thread-safe via locking.
    /// </summary>
    internal sealed class ConversationStore
    {
        private readonly object _lock = new object();
        private string _id;
        private string _title;
        private StreamMode _mode;
        private int _historyCount;
        private List<ConversationMessage> _messages;
        private int _mockScenarioIndex;
        private volatile bool _activeStream;

        public bool ActiveStream
        {
            get { return _activeStream; }
            set { _activeStream = value; }
        }

        public ConversationStore()
        {
            Reset(StreamMode.mock, 480);
        }

        public void Reset(StreamMode mode, int historyCount)
        {
            lock (_lock)
            {
                _id = "conv_" + Guid.NewGuid().ToString("N").Substring(0, 8);
                _title = "Chat Session";
                _mode = mode;
                _historyCount = historyCount;
                _mockScenarioIndex = 0;
                _activeStream = false;

                if (mode == StreamMode.mock)
                {
                    _messages = MockConversation.CreateSeedMessages();
                }
                else
                {
                    _messages = MockConversation.CreateEmpty();
                }
            }
        }

        public ConversationSnapshot GetSnapshot()
        {
            lock (_lock)
            {
                return new ConversationSnapshot
                {
                    Id = _id,
                    Title = _title,
                    Mode = _mode,
                    HistoryCount = _historyCount,
                    Messages = new List<ConversationMessage>(_messages)
                };
            }
        }

        public ConversationSnapshot GetSnapshot(StreamMode mode, int historyCount)
        {
            lock (_lock)
            {
                if (mode != _mode || historyCount != _historyCount)
                {
                    Reset(mode, historyCount);
                }
                return GetSnapshot();
            }
        }

        public string AddUserMessage(string text)
        {
            lock (_lock)
            {
                var msg = new ConversationMessage
                {
                    Id = "msg_" + Guid.NewGuid().ToString("N").Substring(0, 8),
                    Role = MessageRole.user,
                    Status = MessageStatus.complete,
                    CreatedAt = DateTime.UtcNow.ToString("o"),
                    Text = text
                };
                _messages.Add(msg);
                return msg.Id;
            }
        }

        public string CreateAssistantMessage()
        {
            lock (_lock)
            {
                var msg = new ConversationMessage
                {
                    Id = "msg_" + Guid.NewGuid().ToString("N").Substring(0, 8),
                    Role = MessageRole.assistant,
                    Status = MessageStatus.streaming,
                    CreatedAt = DateTime.UtcNow.ToString("o"),
                    Events = new List<BaseStreamEvent>()
                };
                _messages.Add(msg);
                return msg.Id;
            }
        }

        public void AppendEvent(string messageId, BaseStreamEvent evt)
        {
            lock (_lock)
            {
                for (int i = _messages.Count - 1; i >= 0; i--)
                {
                    if (_messages[i].Id == messageId)
                    {
                        if (_messages[i].Events == null)
                            _messages[i].Events = new List<BaseStreamEvent>();
                        _messages[i].Events.Add(evt);
                        break;
                    }
                }
            }
        }

        public void CompleteMessage(string messageId)
        {
            lock (_lock)
            {
                for (int i = _messages.Count - 1; i >= 0; i--)
                {
                    if (_messages[i].Id == messageId)
                    {
                        _messages[i].Status = MessageStatus.complete;
                        break;
                    }
                }
            }
        }

        public void ErrorMessage(string messageId)
        {
            lock (_lock)
            {
                for (int i = _messages.Count - 1; i >= 0; i--)
                {
                    if (_messages[i].Id == messageId)
                    {
                        _messages[i].Status = MessageStatus.error;
                        break;
                    }
                }
            }
        }

        public List<ConversationMessage> GetHistory()
        {
            lock (_lock)
            {
                return new List<ConversationMessage>(_messages);
            }
        }

        public StreamMode Mode
        {
            get { lock (_lock) { return _mode; } }
        }

        public int NextMockScenarioIndex()
        {
            lock (_lock)
            {
                return _mockScenarioIndex++;
            }
        }
    }
}
