using TwitchChatTTS.Seven.Socket.Context;

namespace TwitchChatTTS
{
    public class Configuration
    {
        public HermesConfiguration? Hermes;
        public TwitchConfiguration? Twitch;
        public EmotesConfiguration? Emotes;
        public OBSConfiguration? Obs;
        public SevenConfiguration? Seven;


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

        public class EmotesConfiguration {
            public string? CounterFilePath;
        }

        public class OBSConfiguration {
            public string? Host;
            public short? Port;
            public string? Password;
        }

        public class SevenConfiguration {
            public string? Protocol;
            public string? Url;

            public IEnumerable<SevenSubscriptionConfiguration>? InitialSubscriptions;
        }
    }
}