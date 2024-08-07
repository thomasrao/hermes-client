namespace TwitchChatTTS
{
    public class Configuration
    {
        public string Environment = "PROD";

        public HermesConfiguration? Hermes;
        public TwitchConfiguration? Twitch;
        public OBSConfiguration? Obs;


        public class HermesConfiguration {
            public string? Token;
        }

        public class TwitchConfiguration {
            public IEnumerable<string>? Channels;
            public bool TtsWhenOffline;
            public string? WebsocketUrl;
            public string? ApiUrl;
        }

        public class OBSConfiguration {
            public string? Host;
            public short? Port;
            public string? Password;
        }
    }
}