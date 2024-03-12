namespace TwitchChatTTS.Seven.Socket.Data
{
    public class UnsubscribeMessage
    {
        public string Type { get; set; }
        public IDictionary<string, string>? Condition { get; set; }
    }
}