namespace TwitchChatTTS.OBS.Socket.Data
{
    public class RequestMessage
    {
        public string RequestType { get; set; }
        public string RequestId { get; set; }
        public Dictionary<string, object> RequestData { get; set; }

        public RequestMessage(string type, string id, Dictionary<string, object> data)
        {
            RequestType = type;
            RequestId = id;
            RequestData = data;
        }

        public RequestMessage(string type, Dictionary<string, object> data) : this(type, string.Empty, data) { }

        public RequestMessage(string type) : this(type, string.Empty, new()) { }
    }
}