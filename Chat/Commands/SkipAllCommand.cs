using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TwitchLib.Client.Models;

namespace TwitchChatTTS.Chat.Commands
{
    public class SkipAllCommand : ChatCommand
    {
        private IServiceProvider _serviceProvider;
        private ILogger<SkipAllCommand> _logger;

        public SkipAllCommand(IServiceProvider serviceProvider, ILogger<SkipAllCommand> logger)
        : base("skipall", "Skips all text to speech messages in queue and playing.") {
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
            player.RemoveAll();

            if (player.Playing == null)
                return;
            
            AudioPlaybackEngine.Instance.RemoveMixerInput(player.Playing);
            player.Playing = null;

            _logger.LogInformation("Skipped all queued and playing tts.");
        }
    }
}