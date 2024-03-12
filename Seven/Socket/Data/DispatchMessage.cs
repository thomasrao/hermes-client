namespace TwitchChatTTS.Seven.Socket.Data
{
    public class DispatchMessage
    {
        public object EventType { get; set; }
        public ChangeMapMessage Body { get; set; }
    }
}