using TwitchChatTTS.Hermes;

namespace TwitchChatTTS.Twitch
{
    public class TTSContext
    {
        public string DefaultVoice;
        public IEnumerable<TTSVoice>? EnabledVoices;
        public IDictionary<string, TTSUsernameFilter>? UsernameFilters;
        public IEnumerable<TTSWordFilter>? WordFilters;
    }
}