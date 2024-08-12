using TwitchChatTTS.Twitch.Socket.Messages;

namespace TwitchChatTTS.Chat.Commands.Parameters
{
    public class TTSVoiceNameParameter : CommandParameter
    {
        private bool _enabled;
        private readonly User _user;

        public TTSVoiceNameParameter(string name, bool enabled, User user, bool optional = false) : base(name, optional)
        {
            _enabled = enabled;
            _user = user;
        }

        public override bool Validate(string value, TwitchChatFragment[] fragments)
        {
            if (_user.VoicesAvailable == null)
                return false;
            
            value = value.ToLower();
            if (_enabled)
                return _user.VoicesEnabled.Any(v => v.ToLower() == value);
            
            return _user.VoicesAvailable.Any(e => e.Value.ToLower() == value);
        }
    }
}