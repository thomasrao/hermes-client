using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using HermesSocketLibrary.Requests.Messages;
using HermesSocketLibrary.Socket.Data;
using TwitchChatTTS.Twitch.Socket.Handlers;

namespace TwitchChatTTS
{
    public class User
    {
        // Hermes user id
        public string HermesUserId { get; set; }
        public string HermesUsername { get; set; }
        public long TwitchUserId { get; set; }
        public string TwitchUsername { get; set; }
        public string SevenEmoteSetId { get; set; }
        public long? OwnerId { get; set; }

        public Connection? TwitchConnection { get; set; }
        public Connection? NightbotConnection { get; set; }

        public string DefaultTTSVoice { get; set; }
        // voice id -> voice name
        public IDictionary<string, string> VoicesAvailable { get => _voicesAvailable; set { _voicesAvailable = value; VoiceNameRegex = GenerateEnabledVoicesRegex(); } }
        // chatter/twitch id -> voice id
        public IDictionary<long, string> VoicesSelected { get; set; }
        // voice names
        public HashSet<string> VoicesEnabled { get => _voicesEnabled; set { _voicesEnabled = value; VoiceNameRegex = GenerateEnabledVoicesRegex(); } }

        public IDictionary<string, RaidInfo> Raids { get; set; } = new Dictionary<string, RaidInfo>();
        public HashSet<long> Chatters { get; set; }
        public TTSWordFilter[] RegexFilters { get; set; }
        [JsonIgnore]
        public Regex? VoiceNameRegex { get; set; }

        private IDictionary<string, string> _voicesAvailable;
        private HashSet<string> _voicesEnabled;


        private Regex? GenerateEnabledVoicesRegex()
        {
            if (VoicesAvailable == null || VoicesAvailable.Count() <= 0)
                return null;

            var enabledVoicesString = string.Join("|", VoicesAvailable.Where(v => VoicesEnabled == null || !VoicesEnabled.Any() || VoicesEnabled.Contains(v.Value)).Select(v => v.Value));
            return new Regex($@"\b({enabledVoicesString})\:(.*?)(?=\Z|\b(?:{enabledVoicesString})\:)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        }
    }
}