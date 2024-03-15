using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TwitchLib.Client.Models;

namespace TwitchChatTTS.Chat.Commands
{
    public class SkipCommand : ChatCommand
    {
        private IServiceProvider _serviceProvider;
        private ILogger<SkipCommand> _logger;

        public SkipCommand(IServiceProvider serviceProvider, ILogger<SkipCommand> logger)
        : base("skip", "Skips the current text to speech message.") {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public override async Task<bool> CheckPermissions(ChatMessage message, long broadcasterId)
        {
            return message.IsModerator || message.IsVip || message.IsBroadcaster;
        }

        public override async Task Execute(IList<string> args, ChatMessage message, long broadcasterId)
        {
            var player = _serviceProvider.GetRequiredService<TTSPlayer>();
            if (player.Playing == null)
                return;
            
            AudioPlaybackEngine.Instance.RemoveMixerInput(player.Playing);
            player.Playing = null;

            _logger.LogInformation("Skipped current tts.");
        }
    }
}