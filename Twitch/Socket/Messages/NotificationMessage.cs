namespace TwitchChatTTS.Twitch.Socket.Messages
{
    public class NotificationMessage
    {
        public NotificationInfo Subscription { get; set; }
        public object Event { get; set; }
    }

    public class NotificationInfo : EventSubscriptionMessage
    {
        public string Id { get; set; }
        public string Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public object? Event { get; set; }
    }
}