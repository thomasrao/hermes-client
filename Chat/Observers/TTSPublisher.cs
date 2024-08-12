using TwitchChatTTS.Chat.Speech;

namespace TwitchChatTTS.Chat.Observers
{
    public class TTSPublisher : IObservable<TTSGroupedMessage>
    {
        private readonly HashSet<IObserver<TTSGroupedMessage>> _observers;
        private readonly HashSet<TTSGroupedMessage> _messages;


        public TTSPublisher()
        {
            _observers = new();
            _messages = new();
        }


        public IDisposable Subscribe(IObserver<TTSGroupedMessage> observer)
        {
            if (_observers.Add(observer))
            {
                foreach (var item in _messages)
                    observer.OnNext(item);
            }

            return new Unsubscriber<TTSGroupedMessage>(_observers, observer);
        }
    }

    internal sealed class Unsubscriber<T> : IDisposable
    {
        private readonly ISet<IObserver<T>> _observers;
        private readonly IObserver<T> _observer;

        internal Unsubscriber(ISet<IObserver<T>> observers, IObserver<T> observer)
            => (_observers, _observer) = (observers, observer);

        public void Dispose() => _observers.Remove(_observer);
    }
}