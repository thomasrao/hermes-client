using TwitchChatTTS.Twitch.Socket.Messages;

namespace TwitchChatTTS.Chat.Commands.Parameters
{
    public abstract class CommandParameter
    {
        public string Name { get; }
        public bool Optional { get; }

        public CommandParameter(string name, bool optional)
        {
            Name = name;
            Optional = optional;
        }

        public abstract bool Validate(string value, TwitchChatFragment[] fragments);
    }
}