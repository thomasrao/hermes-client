namespace TwitchChatTTS.OBS.Socket.Data
{
    [Serializable]
    public class RequestMessage
    {
        public string requestType { get; set; }
        public string requestId { get; set; }
        public Dictionary<string, object> requestData { get; set; }

        public RequestMessage(string type, string id, Dictionary<string, object> data) {
            requestType = type;
            requestId = id;
            requestData = data;
        }
    }
}