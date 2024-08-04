using TwitchChatTTS.Twitch.Socket.Messages;

namespace TwitchChatTTS.Chat.Commands.Parameters
{
    public class UnvalidatedParameter : CommandParameter
    {
        public UnvalidatedParameter(string name, bool optional = false) : base(name, optional)
        {
        }

        public override bool Validate(string value, ChannelChatMessage message)
        {
            return true;
        }
    }
}