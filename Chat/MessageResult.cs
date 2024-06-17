public class MessageResult
{
    public MessageStatus Status;
    public long BroadcasterId;
    public long ChatterId;
    public HashSet<string> Emotes;


    public MessageResult(MessageStatus status, long broadcasterId, long chatterId, HashSet<string>? emotes = null)
    {
        Status = status;
        BroadcasterId = broadcasterId;
        ChatterId = chatterId;
        Emotes = emotes ?? new HashSet<string>();
    }
}

public enum MessageStatus
{
    None = 0,
    NotReady = 1,
    Blocked = 2,
    Command = 3
}