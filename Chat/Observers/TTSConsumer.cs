using Serilog;
using TwitchChatTTS.Chat.Speech;

namespace TwitchChatTTS.Chat.Observers
{
    public class TTSConsumer : IObserver<TTSGroupedMessage>
    {
        private readonly TTSEngine _engine;
        private readonly ILogger _logger;

        private IDisposable? _cancellation;

        public TTSConsumer(TTSEngine engine, ILogger logger)
        {
            _engine = engine;
            _logger = logger;
        }

        public virtual void Subscribe(TTSPublisher provider) =>
            _cancellation = provider.Subscribe(this);

        public virtual void Unsubscribe()
        {
            _cancellation?.Dispose();
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
            _logger.Error(error, "An error happened while observing for TTS messages.");
        }

        public void OnNext(TTSGroupedMessage value)
        {
            _engine.PlayerSource?.Cancel();
        }
    }
}