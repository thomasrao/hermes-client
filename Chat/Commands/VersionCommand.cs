using HermesSocketLibrary.Socket.Data;
using Serilog;
using TwitchChatTTS.Hermes.Socket;
using TwitchChatTTS.Twitch.Socket;
using TwitchChatTTS.Twitch.Socket.Messages;
using static TwitchChatTTS.Chat.Commands.TTSCommands;

namespace TwitchChatTTS.Chat.Commands
{
    public class VersionCommand : IChatCommand
    {
        private readonly User _user;
        private ILogger _logger;

        public string Name => "version";

        public VersionCommand(User user, ILogger logger)
        {
            _user = user;
            _logger = logger;
        }

        public void Build(ICommandBuilder builder)
        {
            builder.CreateCommandTree(Name, b => b.CreateCommand(new AppVersionCommand(_user, _logger)));
        }

        private sealed class AppVersionCommand : IChatPartialCommand
        {
            private readonly User _user;
            private ILogger _logger;

            public bool AcceptCustomPermission { get => true; }

            public AppVersionCommand(User user, ILogger logger)
            {
                _user = user;
                _logger = logger;
            }

            public async Task Execute(IDictionary<string, string> values, ChannelChatMessage message, HermesSocketClient hermes)
            {
                _logger.Information($"TTS Version: {TTS.MAJOR_VERSION}.{TTS.MINOR_VERSION}");

                await hermes.SendLoggingMessage(HermesLoggingLevel.Info, $"{_user.TwitchUsername} [twitch id: {_user.TwitchUserId}] using version {TTS.MAJOR_VERSION}.{TTS.MINOR_VERSION}.");
            }
        }
    }
}