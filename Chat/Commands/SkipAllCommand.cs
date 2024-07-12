using Serilog;
using TwitchLib.Client.Models;

namespace TwitchChatTTS.Chat.Commands
{
    public class SkipAllCommand : ChatCommand
    {
        private readonly TTSPlayer _ttsPlayer;
        private readonly ILogger _logger;

        public SkipAllCommand(TTSPlayer ttsPlayer, ILogger logger)
        : base("skipall", "Skips all text to speech messages in queue and playing.")
        {
            _ttsPlayer = ttsPlayer;
            _logger = logger;
        }

        public override async Task<bool> CheckDefaultPermissions(ChatMessage message, long broadcasterId)
        {
            return message.IsModerator || message.IsVip || message.IsBroadcaster;
        }

        public override async Task Execute(IList<string> args, ChatMessage message, long broadcasterId)
        {
            _ttsPlayer.RemoveAll();

            if (_ttsPlayer.Playing == null)
                return;

            AudioPlaybackEngine.Instance.RemoveMixerInput(_ttsPlayer.Playing);
            _ttsPlayer.Playing = null;

            _logger.Information("Skipped all queued and playing tts.");
        }
    }
}