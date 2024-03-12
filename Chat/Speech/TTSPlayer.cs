using NAudio.Wave;

public class TTSPlayer {
    private PriorityQueue<TTSMessage, int> _messages; // ready to play
    private PriorityQueue<TTSMessage, int> _buffer;
    private Mutex _mutex;
    private Mutex _mutex2;

    public TTSPlayer() {
        _messages = new PriorityQueue<TTSMessage, int>();
        _buffer = new PriorityQueue<TTSMessage, int>();
        _mutex = new Mutex();
        _mutex2 = new Mutex();
    }

    public void Add(TTSMessage message) {
        try {
            _mutex2.WaitOne();
            _buffer.Enqueue(message, message.Priority);
        } finally {
            _mutex2.ReleaseMutex();
        }
    }

    public TTSMessage? ReceiveReady() {
        try {
            _mutex.WaitOne();
            if (_messages.TryDequeue(out TTSMessage? message, out int _)) {
                return message;
            }
            return null;
        } finally {
            _mutex.ReleaseMutex();
        }
    }

    public TTSMessage? ReceiveBuffer() {
        try {
            _mutex2.WaitOne();
            if (_buffer.TryDequeue(out TTSMessage? message, out int _)) {
                return message;
            }
            return null;
        } finally {
            _mutex2.ReleaseMutex();
        }
    }

    public void Ready(TTSMessage message) {
        try {
            _mutex.WaitOne();
            _messages.Enqueue(message, message.Priority);
        } finally {
            _mutex.ReleaseMutex();
        }
    }

    public void RemoveAll() {
        try {
            _mutex2.WaitOne();
            _buffer.Clear();
        } finally {
            _mutex2.ReleaseMutex();
        }

        try {
            _mutex.WaitOne();
            _messages.Clear();
        } finally {
            _mutex.ReleaseMutex();
        }
    }

    public bool IsEmpty() {
        return _messages.Count == 0;
    }
}

public class TTSMessage {
    public string? Voice { get; set; }
    public string? Channel { get; set; }
    public string? Username { get; set; }
    public string? Message { get; set; }
    public string? File { get; set; }
    public DateTime Timestamp { get; set; }
    public bool Moderator { get; set; }
    public bool Bot { get; set; }
    public IEnumerable<KeyValuePair<string, string>>? Badges { get; set; }
    public int Bits { get; set; }
    public int Priority { get; set; }
    public ISampleProvider? Audio { get; set; }
}