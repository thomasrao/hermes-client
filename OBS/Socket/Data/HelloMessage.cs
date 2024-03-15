namespace TwitchChatTTS.OBS.Socket.Data
{
    [Serializable]
    public class HelloMessage
    {
        public string ObsWebSocketVersion { get; set; }
        public int RpcVersion { get; set; }
        public AuthenticationMessage Authentication { get; set; }
    }

    public class AuthenticationMessage {
        public string Challenge { get; set; }
        public string Salt { get; set; }
    }
}