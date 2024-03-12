namespace TwitchChatTTS.Seven.Socket.Data
{
    public class SevenHelloMessage
    {
        public uint HeartbeatInterval { get; set; }
        public string SessionId { get; set; }
        public int SubscriptionLimit { get; set; }
    }
}