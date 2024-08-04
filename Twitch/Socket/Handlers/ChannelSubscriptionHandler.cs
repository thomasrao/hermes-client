using Serilog;
using TwitchChatTTS.Twitch.Socket.Messages;

namespace TwitchChatTTS.Twitch.Socket.Handlers
{
    public class ChannelSubscriptionHandler : ITwitchSocketHandler
    {
        public string Name => "channel.subscription.message";

        private readonly TTSPlayer _player;
        private readonly ILogger _logger;

        public ChannelSubscriptionHandler(TTSPlayer player, ILogger logger) {
            _player = player;
            _logger = logger;
        }

        public async Task Execute(TwitchWebsocketClient sender, object? data)
        {
            if (sender == null)
                return;
            if (data == null)
            {
                _logger.Warning("Twitch websocket message data is null.");
                return;
            }
            if (data is not ChannelSubscriptionMessage message)
                return;

            _logger.Debug("Subscription occured.");
        }
    }
}