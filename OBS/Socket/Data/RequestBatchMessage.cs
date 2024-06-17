namespace TwitchChatTTS.OBS.Socket.Data
{
    public class RequestBatchMessage
    {
        public string RequestId { get; set; }
        public bool HaltOnFailure { get; set; }
        public RequestBatchExecutionType ExecutionType { get; set; }
        public IEnumerable<object> Requests { get; set;}

        public RequestBatchMessage(string id, IEnumerable<object> requests, bool haltOnFailure = false, RequestBatchExecutionType executionType = RequestBatchExecutionType.SerialRealtime)
        {
            RequestId = id;
            Requests = requests;
            HaltOnFailure = haltOnFailure;
            ExecutionType = executionType;
        }
    }

    public enum RequestBatchExecutionType {
        None = -1,
        SerialRealtime = 0,
        SerialFrame = 1,
        Parallel = 2
    }
}