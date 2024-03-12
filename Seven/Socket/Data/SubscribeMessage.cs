namespace TwitchChatTTS.Seven.Socket.Data
{
    public class SubscribeMessage
    {
        public string? Type { get; set; }
        public IDictionary<string, string>? Condition { get; set; }
    }
}