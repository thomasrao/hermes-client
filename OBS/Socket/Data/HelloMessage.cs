namespace TwitchChatTTS.OBS.Socket.Data
{
    [Serializable]
    public class HelloMessage
    {
        public string obsWebSocketVersion { get; set; }
        public int rpcVersion { get; set; }
        public AuthenticationMessage authentication { get; set; }
    }

    public class AuthenticationMessage {
        public string challenge { get; set; }
        public string salt { get; set; }
    }
}