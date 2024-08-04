using TwitchChatTTS.Hermes.Socket;
using TwitchChatTTS.Twitch.Socket.Messages;
using static TwitchChatTTS.Chat.Commands.TTSCommands;

namespace TwitchChatTTS.Chat.Commands
{
    public interface IChatCommand
    {
        string Name { get; }
        void Build(ICommandBuilder builder);
    }

    public interface IChatPartialCommand
    {
        bool AcceptCustomPermission { get; }
        Task Execute(IDictionary<string, string> values, ChannelChatMessage message, HermesSocketClient client);
    }
}