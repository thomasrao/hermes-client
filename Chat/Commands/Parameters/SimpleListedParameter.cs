namespace TwitchChatTTS.Chat.Commands.Parameters
{
    public class SimpleListedParameter : ChatCommandParameter
    {
        private readonly string[] _values;

        public SimpleListedParameter(string[] possibleValues, bool optional = false) : base("TTS Voice Name", "Name of a TTS voice", optional)
        {
            _values = possibleValues;
        }

        public override bool Validate(string value)
        {
            return _values.Contains(value.ToLower());
        }
    }
}