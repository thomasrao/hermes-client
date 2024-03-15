namespace TwitchChatTTS.OBS.Socket.Data
{
    [Serializable]
    public class RequestResponseMessage
    {
        public string RequestType { get; set; }
        public string RequestId { get; set; }
        public object RequestStatus { get; set; }
        public Dictionary<string, object> ResponseData { get; set; }
    }
}