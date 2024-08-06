using TwitchChatTTS.Twitch.Socket.Messages;

namespace TwitchChatTTS.Chat.Commands
{
    public interface ICommandManager {
        Task<ChatCommandResult> Execute(string arg, ChannelChatMessage message, IEnumerable<string> groups);
        void Update(ICommandFactory factory);
    }
}