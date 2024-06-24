using Serilog;
using TwitchLib.Client.Models;

namespace TwitchChatTTS.Chat.Commands
{
    public class SkipCommand : ChatCommand
    {
        private readonly TTSPlayer _ttsPlayer;
        private readonly ILogger _logger;

        public SkipCommand(TTSPlayer ttsPlayer, ILogger logger)
        : base("skip", "Skips the current text to speech message.")
        {
            _ttsPlayer = ttsPlayer;
            _logger = logger;
        }

        public override async Task<bool> CheckPermissions(ChatMessage message, long broadcasterId)
        {
            return message.IsModerator || message.IsVip || message.IsBroadcaster;
        }

        public override async Task Execute(IList<string> args, ChatMessage message, long broadcasterId)
        {
            if (_ttsPlayer.Playing == null)
                return;

            AudioPlaybackEngine.Instance.RemoveMixerInput(_ttsPlayer.Playing);
            _ttsPlayer.Playing = null;

            _logger.Information("Skipped current tts.");
        }
    }
}