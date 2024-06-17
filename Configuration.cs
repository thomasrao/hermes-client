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
            public IDictionary<string, RedeemConfiguration>? Redeems;
            public bool? TtsWhenOffline;
        }

        public class RedeemConfiguration {
            public string? AudioFilePath;
            public string? OutputFilePath;
            public string? OutputContent;
            public bool? OutputAppend;
        }

        public class OBSConfiguration {
            public string? Host;
            public short? Port;
            public string? Password;
        }
    }
}