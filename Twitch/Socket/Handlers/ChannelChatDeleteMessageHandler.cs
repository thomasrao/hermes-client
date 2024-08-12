using Serilog;
using TwitchChatTTS.Chat.Soeech;
using TwitchChatTTS.Twitch.Socket.Messages;

namespace TwitchChatTTS.Twitch.Socket.Handlers
{
    public class ChannelChatDeleteMessageHandler : ITwitchSocketHandler
    {
        public string Name => "channel.chat.message_delete";

        private readonly TTSPlayer _player;
        private readonly AudioPlaybackEngine _playback;
        private readonly ILogger _logger;

        public ChannelChatDeleteMessageHandler(TTSPlayer player, AudioPlaybackEngine playback, ILogger logger)
        {
            _player = player;
            _playback = playback;
            _logger = logger;
        }

        public Task Execute(TwitchWebsocketClient sender, object data)
        {
            if (data is not ChannelChatDeleteMessage message)
                return Task.CompletedTask;
            
            if (_player.Playing?.MessageId == message.MessageId)
            {
                _playback.RemoveMixerInput(_player.Playing!.Audio!);
                _player.Playing = null;
            }
            else
                _player.RemoveMessage(message.MessageId);
            

            _logger.Information($"Deleted chat message [message id: {message.MessageId}][target chatter: {message.TargetUserLogin}][target chatter id: {message.TargetUserId}][broadcaster: {message.BroadcasterUserLogin}][broadcaster id: {message.BroadcasterUserId}]");
            return Task.CompletedTask;
        }
    }
}