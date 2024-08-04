using CommonSocketLibrary.Abstract;
using Serilog;
using TwitchChatTTS.Twitch.Socket.Messages;

namespace TwitchChatTTS.Twitch.Socket.Handlers
{
    public class SessionWelcomeHandler : ITwitchSocketHandler
    {
        public string Name => "session_welcome";

        private readonly TwitchApiClient _api;
        private readonly User _user;
        private readonly ILogger _logger;

        public SessionWelcomeHandler(TwitchApiClient api, User user, ILogger logger)
        {
            _api = api;
            _user = user;
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
            if (data is not SessionWelcomeMessage message)
                return;
            if (_api == null)
                return;

            if (string.IsNullOrEmpty(message.Session.Id))
            {
                _logger.Warning($"No session info provided by Twitch [status: {message.Session.Status}]");
                return;
            }

            string[] subscriptionsv1 = [
                "channel.chat.message",
                "channel.chat.message_delete",
                "channel.chat.notification",
                "channel.chat.clear",
                "channel.chat.clear_user_messages",
                "channel.ad_break.begin",
                "channel.subscription.message",
                "channel.ban",
                "channel.channel_points_custom_reward_redemption.add"
            ];
            string[] subscriptionsv2 = [
                "channel.follow",
            ];
            string broadcasterId = _user.TwitchUserId.ToString();
            foreach (var subscription in subscriptionsv1)
                await Subscribe(subscription, message.Session.Id, broadcasterId, "1");
            foreach (var subscription in subscriptionsv2)
                await Subscribe(subscription, message.Session.Id, broadcasterId, "2");
            
            sender.SessionId = message.Session.Id;
            sender.Identified = sender.SessionId != null;
        }

        private async Task Subscribe(string subscriptionName, string sessionId, string broadcasterId, string version)
        {
            try
            {
                var response = await _api.CreateEventSubscription(subscriptionName, version, sessionId, broadcasterId);
                if (response == null)
                {
                    _logger.Error($"Failed to create an event subscription [subscription type: {subscriptionName}][reason: response is null]");
                    return;
                }
                if (response.Data == null)
                {
                    _logger.Error($"Failed to create an event subscription [subscription type: {subscriptionName}][reason: data is null]");
                    return;
                }
                if (!response.Data.Any())
                {
                    _logger.Error($"Failed to create an event subscription [subscription type: {subscriptionName}][reason: data is empty]");
                    return;
                }
                _logger.Information($"Sucessfully added subscription to Twitch websockets [subscription type: {subscriptionName}]");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to create an event subscription [subscription type: {subscriptionName}][reason: exception]");
            }
        }
    }
}