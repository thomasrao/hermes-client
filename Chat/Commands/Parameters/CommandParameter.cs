namespace TwitchChatTTS.Chat.Commands.Parameters
{
    public abstract class CommandParameter : ICloneable
    {
        public string Name { get; }
        public bool Optional { get; }

        public CommandParameter(string name, bool optional)
        {
            Name = name;
            Optional = optional;
        }

        public abstract bool Validate(string value);

        public object Clone() {
            return (CommandParameter) MemberwiseClone();
        }
    }
}