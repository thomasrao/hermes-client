using TwitchChatTTS.Twitch.Socket.Messages;

namespace TwitchChatTTS.Chat.Commands.Parameters
{
    public class StaticParameter : CommandParameter
    {
        private readonly string _value;

        public string Value { get => _value; }

        public StaticParameter(string name, string value, bool optional = false) : base(name, optional)
        {
            _value = value.ToLower();
        }

        public override bool Validate(string value, ChannelChatMessage message)
        {
            return _value == value.ToLower();
        }
    }
}