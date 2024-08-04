using TwitchChatTTS.Twitch.Socket.Messages;

namespace TwitchChatTTS.Chat.Commands.Parameters
{
    public class OBSTransformationParameter : CommandParameter
    {
        private string[] _values = ["x", "y", "rotation", "rotate", "r"];

        public OBSTransformationParameter(string name, bool optional = false) : base(name, optional)
        {
        }

        public override bool Validate(string value, ChannelChatMessage message)
        {
            return _values.Contains(value.ToLower());
        }
    }
}