using HermesSocketLibrary.Socket.Data;
using Serilog;
using TwitchChatTTS.Hermes.Socket;
using TwitchLib.Client.Models;

namespace TwitchChatTTS.Chat.Commands
{
    public class VersionCommand : ChatCommand
    {
        private readonly User _user;
        private ILogger _logger;

        public VersionCommand(User user, ILogger logger)
        : base("version", "Does nothing.")
        {
            _user = user;
            _logger = logger;
        }

        public override async Task<bool> CheckDefaultPermissions(ChatMessage message)
        {
            return message.IsBroadcaster;
        }

        public override async Task Execute(IList<string> args, ChatMessage message, HermesSocketClient client)
        {
            _logger.Information($"Version: {TTS.MAJOR_VERSION}.{TTS.MINOR_VERSION}");

            await client.SendLoggingMessage(HermesLoggingLevel.Info, $"{_user.TwitchUsername} [twitch id: {_user.TwitchUserId}] using version {TTS.MAJOR_VERSION}.{TTS.MINOR_VERSION}.");
        }
    }
}