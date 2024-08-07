using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using TwitchChatTTS.Twitch.Socket.Messages;

namespace TwitchChatTTS.Twitch.Socket.Handlers
{
    public sealed class NotificationHandler : ITwitchSocketHandler
    {
        public string Name => "notification";

        private IDictionary<string, ITwitchSocketHandler> _handlers;
        private readonly ILogger _logger;

        private IDictionary<string, Type> _messageTypes;
        private readonly JsonSerializerOptions _options;

        public NotificationHandler(
            [FromKeyedServices("twitch-notifications")] IEnumerable<ITwitchSocketHandler> handlers,
            ILogger logger
        )
        {
            _handlers = handlers.ToDictionary(h => h.Name, h => h);
            _logger = logger;

            _options = new JsonSerializerOptions()
            {
                PropertyNameCaseInsensitive = false,
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            };

            _messageTypes = new Dictionary<string, Type>();
            _messageTypes.Add("channel.adbreak.begin", typeof(ChannelAdBreakMessage));
            _messageTypes.Add("channel.ban", typeof(ChannelBanMessage));
            _messageTypes.Add("channel.chat.message", typeof(ChannelChatMessage));
            _messageTypes.Add("channel.chat.clear", typeof(ChannelChatClearMessage));
            _messageTypes.Add("channel.chat.clear_user_messages", typeof(ChannelChatClearUserMessage));
            _messageTypes.Add("channel.chat.message_delete", typeof(ChannelChatDeleteMessage));
            _messageTypes.Add("channel.channel_points_custom_reward_redemption.add", typeof(ChannelCustomRedemptionMessage));
            _messageTypes.Add("channel.raid", typeof(ChannelRaidMessage));
            _messageTypes.Add("channel.follow", typeof(ChannelFollowMessage));
            _messageTypes.Add("channel.subscribe", typeof(ChannelSubscriptionMessage));
            _messageTypes.Add("channel.subscription.message", typeof(ChannelResubscriptionMessage));
            _messageTypes.Add("channel.subscription.gift", typeof(ChannelSubscriptionGiftMessage));
        }

        public async Task Execute(TwitchWebsocketClient sender, object data)
        {
            if (sender == null)
                return;
            if (data is not NotificationMessage message)
                return;

            if (!_messageTypes.TryGetValue(message.Subscription.Type, out var type) || type == null)
            {
                _logger.Warning($"Could not find Twitch notification type [message type: {message.Subscription.Type}]");
                return;
            }

            if (!_handlers.TryGetValue(message.Subscription.Type, out ITwitchSocketHandler? handler) || handler == null)
            {
                _logger.Warning($"Could not find Twitch notification handler [message type: {message.Subscription.Type}]");
                return;
            }

            var d = JsonSerializer.Deserialize(message.Event.ToString()!, type, _options);
            await handler.Execute(sender, d);
        }
    }
}