using Serilog;
using TwitchLib.Client.Models;

namespace TwitchChatTTS.Chat.Commands
{
    public class VersionCommand : ChatCommand
    {
        private ILogger _logger;

        public VersionCommand(ILogger logger)
        : base("version", "Does nothing.")
        {
            _logger = logger;
        }

        public override async Task<bool> CheckDefaultPermissions(ChatMessage message, long broadcasterId)
        {
            return message.IsBroadcaster;
        }

        public override async Task Execute(IList<string> args, ChatMessage message, long broadcasterId)
        {
            _logger.Information($"Version: {TTS.MAJOR_VERSION}.{TTS.MINOR_VERSION}");
        }
    }
}