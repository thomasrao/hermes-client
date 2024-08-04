using TwitchChatTTS.Twitch.Socket.Messages;

namespace TwitchChatTTS.Chat.Commands.Parameters
{
    public class MentionParameter : CommandParameter
    {
        public MentionParameter(string name, bool optional = false) : base(name, optional)
        {
        }

        public override bool Validate(string value, ChannelChatMessage message)
        {
            return value.StartsWith('@') && message.Message.Fragments.Any(f => f.Text == value && f.Mention != null);
        }
    }
}