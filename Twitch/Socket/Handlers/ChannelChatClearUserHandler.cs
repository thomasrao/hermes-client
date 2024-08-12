using Serilog;
using TwitchChatTTS.Chat.Speech;
using TwitchChatTTS.Twitch.Socket.Messages;

namespace TwitchChatTTS.Twitch.Socket.Handlers
{
    public class ChannelChatClearUserHandler : ITwitchSocketHandler
    {
        public string Name => "channel.chat.clear_user_messages";

        private readonly TTSPlayer _player;
        private readonly AudioPlaybackEngine _playback;
        private readonly ILogger _logger;

        public ChannelChatClearUserHandler(TTSPlayer player, AudioPlaybackEngine playback, ILogger logger)
        {
            _player = player;
            _playback = playback;
            _logger = logger;
        }

        public Task Execute(TwitchWebsocketClient sender, object data)
        {
            if (data is not ChannelChatClearUserMessage message)
                return Task.CompletedTask;

            long broadcasterId = long.Parse(message.BroadcasterUserId);
            long chatterId = long.Parse(message.TargetUserId);
            _player.RemoveAll(broadcasterId, chatterId);
            if (_player.Playing != null && _player.Playing.RoomId == broadcasterId && _player.Playing.ChatterId == chatterId)
            {
                _playback.RemoveMixerInput(_player.Playing.Audio!);
                _player.Playing = null;
            }

            _logger.Information($"Cleared all messages by user [target chatter: {message.TargetUserLogin}][target chatter id: {chatterId}][broadcaster: {message.BroadcasterUserLogin}][broadcaster id: {message.BroadcasterUserId}]");
            return Task.CompletedTask;
        }
    }
}