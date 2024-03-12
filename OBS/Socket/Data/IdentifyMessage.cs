namespace TwitchChatTTS.OBS.Socket.Data
{
    [Serializable]
    public class IdentifyMessage
    {
        public int rpcVersion { get; set; }
        public string? authentication { get; set; }
        public int eventSubscriptions { get; set; }

        public IdentifyMessage(int version, string auth, int subscriptions) {
            rpcVersion = version;
            authentication = auth;
            eventSubscriptions = subscriptions;
        }
    }
}