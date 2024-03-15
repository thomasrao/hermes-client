namespace TwitchChatTTS.Chat.Commands.Parameters
{
    public abstract class ChatCommandParameter
    {
        public string Name { get; }
        public string Description { get; }
        public bool Optional { get; }

        public ChatCommandParameter(string name, string description, bool optional = false) {
            Name = name;
            Description = description;
            Optional = optional;
        }

        public abstract bool Validate(string value);
    }
}