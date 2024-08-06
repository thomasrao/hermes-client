using Serilog;
using TwitchChatTTS.Twitch.Socket.Messages;

namespace TwitchChatTTS.Twitch.Socket.Handlers
{
    public class ChannelChatClearHandler : ITwitchSocketHandler
    {
        public string Name => "channel.chat.clear";

        private readonly TTSPlayer _player;
        private readonly AudioPlaybackEngine _playback;
        private readonly ILogger _logger;

        public ChannelChatClearHandler(TTSPlayer player, AudioPlaybackEngine playback, ILogger logger)
        {
            _player = player;
            _playback = playback;
            _logger = logger;
        }

        public Task Execute(TwitchWebsocketClient sender, object data)
        {
            if (data is not ChannelChatClearMessage message)
                return Task.CompletedTask;

            _player.RemoveAll();
            if (_player.Playing != null)
            {
                _playback.RemoveMixerInput(_player.Playing.Audio!);
                _player.Playing = null;
            }

            _logger.Information($"Chat cleared [broadcaster: {message.BroadcasterUserLogin}][broadcaster id: {message.BroadcasterUserId}]");
            return Task.CompletedTask;
        }
    }
}