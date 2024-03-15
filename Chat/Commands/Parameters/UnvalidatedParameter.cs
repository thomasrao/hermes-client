namespace TwitchChatTTS.Chat.Commands.Parameters
{
    public class UnvalidatedParameter : ChatCommandParameter
    {
        public UnvalidatedParameter(bool optional = false) : base("TTS Voice Name", "Name of a TTS voice", optional)
        {
        }

        public override bool Validate(string value)
        {
            return true;
        }
    }
}