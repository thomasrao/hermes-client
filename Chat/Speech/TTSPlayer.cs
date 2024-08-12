using NAudio.Wave;

namespace TwitchChatTTS.Chat.Speech
{
    public class TTSPlayer
    {
        private readonly PriorityQueue<TTSGroupedMessage, int> _messages; // ready to play
        private readonly PriorityQueue<TTSGroupedMessage, int> _buffer;
        private readonly Mutex _mutex;
        private readonly Mutex _mutex2;

        public TTSGroupedMessage? Playing { get; set; }

        public TTSPlayer()
        {
            _messages = new PriorityQueue<TTSGroupedMessage, int>(new DescendingOrder());
            _buffer = new PriorityQueue<TTSGroupedMessage, int>(new DescendingOrder());
            _mutex = new Mutex();
            _mutex2 = new Mutex();
        }

        public void Add(TTSGroupedMessage message, int priority)
        {
            try
            {
                _mutex2.WaitOne();
                _buffer.Enqueue(message, priority);
            }
            finally
            {
                _mutex2.ReleaseMutex();
            }
        }

        public TTSGroupedMessage? ReceiveReady()
        {
            try
            {
                _mutex.WaitOne();
                if (_messages.TryDequeue(out TTSGroupedMessage? message, out int _))
                {
                    return message;
                }
                return null;
            }
            finally
            {
                _mutex.ReleaseMutex();
            }
        }

        public TTSGroupedMessage? ReceiveBuffer()
        {
            try
            {
                _mutex2.WaitOne();
                if (_buffer.TryDequeue(out TTSGroupedMessage? messages, out int _))
                {
                    return messages;
                }
                return null;
            }
            finally
            {
                _mutex2.ReleaseMutex();
            }
        }

        public void Ready(TTSGroupedMessage message)
        {
            try
            {
                _mutex.WaitOne();
                _messages.Enqueue(message, message.Priority);
            }
            finally
            {
                _mutex.ReleaseMutex();
            }
        }

        public void RemoveAll()
        {
            try
            {
                _mutex2.WaitOne();
                _buffer.Clear();
            }
            finally
            {
                _mutex2.ReleaseMutex();
            }

            try
            {
                _mutex.WaitOne();
                _messages.Clear();
            }
            finally
            {
                _mutex.ReleaseMutex();
            }
        }

        public void RemoveAll(long broadcasterId, long chatterId)
        {
            try
            {
                _mutex2.WaitOne();
                if (_buffer.UnorderedItems.Any(i => i.Element.RoomId == broadcasterId && i.Element.ChatterId == chatterId))
                {
                    var list = _buffer.UnorderedItems.Where(i => i.Element.RoomId == broadcasterId && i.Element.ChatterId != chatterId).ToArray();
                    _buffer.Clear();
                    foreach (var item in list)
                        _buffer.Enqueue(item.Element, item.Element.Priority);
                }
            }
            finally
            {
                _mutex2.ReleaseMutex();
            }

            try
            {
                _mutex.WaitOne();
                if (_messages.UnorderedItems.Any(i => i.Element.RoomId == broadcasterId && i.Element.ChatterId == chatterId))
                {
                    var list = _messages.UnorderedItems.Where(i => i.Element.RoomId == broadcasterId && i.Element.ChatterId != chatterId).ToArray();
                    _messages.Clear();
                    foreach (var item in list)
                        _messages.Enqueue(item.Element, item.Element.Priority);
                }
            }
            finally
            {
                _mutex.ReleaseMutex();
            }
        }

        public void RemoveMessage(string messageId)
        {
            try
            {
                _mutex2.WaitOne();
                if (_buffer.UnorderedItems.Any(i => i.Element.MessageId == messageId))
                {
                    var list = _buffer.UnorderedItems.Where(i => i.Element.MessageId != messageId).ToArray();
                    _buffer.Clear();
                    foreach (var item in list)
                        _buffer.Enqueue(item.Element, item.Element.Priority);
                    return;
                }
            }
            finally
            {
                _mutex2.ReleaseMutex();
            }

            try
            {
                _mutex.WaitOne();
                if (_messages.UnorderedItems.Any(i => i.Element.MessageId == messageId))
                {
                    var list = _messages.UnorderedItems.Where(i => i.Element.MessageId != messageId).ToArray();
                    _messages.Clear();
                    foreach (var item in list)
                        _messages.Enqueue(item.Element, item.Element.Priority);
                }
            }
            finally
            {
                _mutex.ReleaseMutex();
            }
        }

        public bool IsEmpty()
        {
            return _messages.Count == 0;
        }

        private class DescendingOrder : IComparer<int>
        {
            public int Compare(int x, int y) => y.CompareTo(x);
        }
    }

    public class TTSMessage
    {
        public string? Voice { get; set; }
        public string? Message { get; set; }
        public string? File { get; set; }
    }

    public class TTSGroupedMessage
    {
        public long RoomId { get; set; }
        public long? ChatterId { get; set; }
        public string? MessageId { get; set; }
        public DateTime Timestamp { get; set; }
        public int Priority { get; set; }
        public IList<TTSMessage> Messages { get; set; }
        public ISampleProvider? Audio { get; set; }


        public TTSGroupedMessage(long broadcasterId, long? chatterId, string? messageId, IList<TTSMessage> messages, DateTime timestamp, int priority)
        {
            RoomId = broadcasterId;
            ChatterId = chatterId;
            MessageId = messageId;
            Messages = messages;
            Timestamp = timestamp;
            Priority = priority;
        }
    }
}