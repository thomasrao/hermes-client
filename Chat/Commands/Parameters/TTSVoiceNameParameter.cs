namespace TwitchChatTTS.Chat.Commands.Parameters
{
    public class TTSVoiceNameParameter : ChatCommandParameter
    {
        private readonly User _user;

        public TTSVoiceNameParameter(User user, bool optional = false) : base("TTS Voice Name", "Name of a TTS voice", optional)
        {
            _user = user;
        }

        public override bool Validate(string value)
        {
            if (_user.VoicesAvailable == null)
                return false;

            value = value.ToLower();
            return _user.VoicesAvailable.Any(e => e.Value.ToLower() == value);
        }
    }
}