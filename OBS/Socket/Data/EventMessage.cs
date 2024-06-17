namespace TwitchChatTTS.OBS.Socket.Data
{
    public class EventMessage
    {
        public string EventType { get; set; }
        public int EventIntent { get; set; }
        public Dictionary<string, object> EventData { get; set; }
    }
}