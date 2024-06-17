namespace TwitchChatTTS.OBS.Socket.Data
{
    public class IdentifyMessage
    {
        public int RpcVersion { get; set; }
        public string? Authentication { get; set; }
        public int EventSubscriptions { get; set; }

        public IdentifyMessage(int version, string auth, int subscriptions)
        {
            RpcVersion = version;
            Authentication = auth;
            EventSubscriptions = subscriptions;
        }
    }
}