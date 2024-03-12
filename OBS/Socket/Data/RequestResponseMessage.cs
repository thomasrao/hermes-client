namespace TwitchChatTTS.OBS.Socket.Data
{
    [Serializable]
    public class RequestResponseMessage
    {
        public string requestType { get; set; }
        public string requestId { get; set; }
        public object requestStatus { get; set; }
        public Dictionary<string, object> responseData { get; set; }
    }
}