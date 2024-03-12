namespace TwitchChatTTS.OBS.Socket.Context
{
    [Serializable]
    public class HelloContext
    {
        public string? Host { get; set; }
        public short? Port { get; set; }
        public string? Password { get; set; }
    }
}