using TwitchChatTTS.Hermes.Socket;
using TwitchLib.Client.Models;
using static TwitchChatTTS.Chat.Commands.TTSCommands;

namespace TwitchChatTTS.Chat.Commands
{
    public interface IChatCommand {
        string Name { get; }
        void Build(ICommandBuilder builder);
    }

    public interface IChatPartialCommand {
        bool AcceptCustomPermission { get; }
        bool CheckDefaultPermissions(ChatMessage message);
        Task Execute(IDictionary<string, string> values, ChatMessage message, HermesSocketClient client);
    }
}