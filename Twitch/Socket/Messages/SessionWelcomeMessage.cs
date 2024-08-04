namespace TwitchChatTTS.Twitch.Socket.Messages
{
    public class SessionWelcomeMessage
    {
        public TwitchSocketSession Session { get; set; }
        
        public class TwitchSocketSession {
            public string Id { get; set; }
            public string Status { get; set; }
            public DateTime ConnectedAt { get; set; }
            public int KeepaliveTimeoutSeconds { get; set; }
            public string? ReconnectUrl { get; set; }
            public string? RecoveryUrl { get; set; }
        }
    }
}