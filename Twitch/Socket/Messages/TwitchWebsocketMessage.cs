namespace TwitchChatTTS.Twitch.Socket.Messages
{
    public class TwitchWebsocketMessage
    {
        public TwitchMessageMetadata Metadata { get; set; }
        public object? Payload { get; set; }
    }

    public class TwitchMessageMetadata {
        public string MessageId { get; set; }
        public string MessageType { get; set; }
        public DateTime MessageTimestamp { get; set; }
    }

    public interface IVersionedMessage {
        string Version { get; set; }
    }
}