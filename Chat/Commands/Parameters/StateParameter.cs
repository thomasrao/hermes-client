using TwitchChatTTS.Twitch.Socket.Messages;

namespace TwitchChatTTS.Chat.Commands.Parameters
{
    public class StateParameter : CommandParameter
    {
        private string[] _values = ["on", "off", "true", "false", "enabled", "disabled", "enable", "disable", "yes", "no"];

        public StateParameter(string name, bool optional = false) : base(name, optional)
        {
        }

        public override bool Validate(string value, TwitchChatFragment[] fragments)
        {
            return _values.Contains(value.ToLower());
        }
    }
}