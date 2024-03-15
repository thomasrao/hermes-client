namespace TwitchChatTTS.OBS.Socket.Data
{
    [Serializable]
    public class RequestMessage
    {
        public string RequestType { get; set; }
        public string RequestId { get; set; }
        public Dictionary<string, object> RequestData { get; set; }

        public RequestMessage(string type, string id, Dictionary<string, object> data) {
            RequestType = type;
            RequestId = id;
            RequestData = data;
        }
    }
}