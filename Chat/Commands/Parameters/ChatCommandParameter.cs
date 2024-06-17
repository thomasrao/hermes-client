namespace TwitchChatTTS.Chat.Commands.Parameters
{
    public abstract class ChatCommandParameter : ICloneable
    {
        public string Name { get; }
        public string Description { get; }
        public bool Optional { get; private set; }

        public ChatCommandParameter(string name, string description, bool optional = false)
        {
            Name = name;
            Description = description;
            Optional = optional;
        }

        public abstract bool Validate(string value);

        public object Clone() {
            return (ChatCommandParameter) MemberwiseClone();
        }

        public ChatCommandParameter Permissive() {
            Optional = true;
            return this;
        }
    }
}