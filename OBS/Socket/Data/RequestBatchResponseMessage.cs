namespace TwitchChatTTS.OBS.Socket.Data
{
    public class RequestBatchResponseMessage
    {
        public string RequestId { get; set; }
        public IEnumerable<object> Results { get; set; }
    }
}