using static TwitchChatTTS.Chat.Commands.TTSCommands;

namespace TwitchChatTTS.Chat.Commands
{
    public interface ICommandFactory
    {
        ICommandSelector Build();
    }
}