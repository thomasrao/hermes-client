namespace TwitchChatTTS.OBS.Socket.Data
{
    [Serializable]
    public class EventMessage
    {
        public string EventType { get; set; }
        public int EventIntent { get; set; }
        public Dictionary<string, object> EventData { get; set; }
    }
}