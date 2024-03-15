using System.Text.RegularExpressions;
using TwitchChatTTS.Hermes;

namespace TwitchChatTTS
{
    public class User
    {
        // Hermes user id
        public string HermesUserId { get; set; }
        public long TwitchUserId { get; set; }
        public string TwitchUsername { get; set; }
        
        public string DefaultTTSVoice { get; set; }
        // voice id -> voice name
        public IDictionary<string, string> VoicesAvailable { get; set; }
        // chatter/twitch id -> voice name
        public IDictionary<long, string> VoicesSelected { get; set; }
        public HashSet<string> VoicesEnabled { get; set; }

        public IDictionary<string, TTSUsernameFilter> ChatterFilters { get; set; }
        public IList<TTSWordFilter> RegexFilters { get; set;  }


        public User() {
            
        }

        public Regex? GenerateEnabledVoicesRegex() {
            if (VoicesAvailable == null || VoicesAvailable.Count() <= 0)
                return null;

            var enabledVoicesString = string.Join("|", VoicesAvailable.Select(v => v.Value));
            return new Regex($@"\b({enabledVoicesString})\:(.*?)(?=\Z|\b(?:{enabledVoicesString})\:)", RegexOptions.IgnoreCase);
        }
    }
}