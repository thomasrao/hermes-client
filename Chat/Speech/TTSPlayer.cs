using NAudio.Wave;
using TwitchChatTTS.Twitch.Socket.Messages;

public class TTSPlayer
{
    private readonly PriorityQueue<TTSMessage, int> _messages; // ready to play
    private readonly PriorityQueue<TTSMessage, int> _buffer;
    private readonly Mutex _mutex;
    private readonly Mutex _mutex2;

    public TTSMessage? Playing { get; set; }

    public TTSPlayer()
    {
        _messages = new PriorityQueue<TTSMessage, int>(new DescendingOrder());
        _buffer = new PriorityQueue<TTSMessage, int>(new DescendingOrder());
        _mutex = new Mutex();
        _mutex2 = new Mutex();
    }

    public void Add(TTSMessage message)
    {
        try
        {
            _mutex2.WaitOne();
            _buffer.Enqueue(message, message.Priority);
        }
        finally
        {
            _mutex2.ReleaseMutex();
        }
    }

    public TTSMessage? ReceiveReady()
    {
        try
        {
            _mutex.WaitOne();
            if (_messages.TryDequeue(out TTSMessage? message, out int _))
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

    public TTSMessage? ReceiveBuffer()
    {
        try
        {
            _mutex2.WaitOne();
            if (_buffer.TryDequeue(out TTSMessage? message, out int _))
            {
                return message;
            }
            return null;
        }
        finally
        {
            _mutex2.ReleaseMutex();
        }
    }

    public void Ready(TTSMessage message)
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

    public void RemoveAll(long chatterId)
    {
        try
        {
            _mutex2.WaitOne();
            if (_buffer.UnorderedItems.Any(i => i.Element.ChatterId == chatterId)) {
                var list = _buffer.UnorderedItems.Where(i => i.Element.ChatterId != chatterId).ToArray();
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
            if (_messages.UnorderedItems.Any(i => i.Element.ChatterId == chatterId)) {
                var list = _messages.UnorderedItems.Where(i => i.Element.ChatterId != chatterId).ToArray();
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
            if (_buffer.UnorderedItems.Any(i => i.Element.MessageId == messageId)) {
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
            if (_messages.UnorderedItems.Any(i => i.Element.MessageId == messageId)) {
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
    public long ChatterId { get; set; }
    public string MessageId { get; set; }
    public string? Message { get; set; }
    public string? File { get; set; }
    public DateTime Timestamp { get; set; }
    public IEnumerable<TwitchBadge> Badges { get; set; }
    public int Priority { get; set; }
    public ISampleProvider? Audio { get; set; }
}