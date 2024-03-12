namespace TwitchChatTTS.OBS.Socket.Data
{
    [Serializable]
    public class EventMessage
    {
        public string eventType { get; set; }
        public int eventIntent { get; set; }
        public Dictionary<string, object> eventData { get; set; }
    }
}