namespace TwitchChatTTS.Seven.Socket.Context
{
    public class SevenHelloContext
    {
        public IEnumerable<SevenSubscriptionConfiguration>? Subscriptions;
    }

    public class SevenSubscriptionConfiguration {
        public string? Type;
        public IDictionary<string, string>? Condition;
    }
}