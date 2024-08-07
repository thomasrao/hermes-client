using Serilog;
using TwitchChatTTS.Twitch.Socket.Messages;

namespace TwitchChatTTS.Twitch.Socket.Handlers
{
    public class SessionWelcomeHandler : ITwitchSocketHandler
    {
        public string Name => "session_welcome";

        private readonly HermesApiClient _hermes;
        private readonly TwitchApiClient _api;
        private readonly User _user;
        private readonly ILogger _logger;

        public SessionWelcomeHandler(HermesApiClient hermes, TwitchApiClient api, User user, ILogger logger)
        {
            _hermes = hermes;
            _api = api;
            _user = user;
            _logger = logger;
        }

        public async Task Execute(TwitchWebsocketClient sender, object data)
        {
            if (data is not SessionWelcomeMessage message)
                return;

            if (string.IsNullOrEmpty(message.Session.Id))
            {
                _logger.Warning($"No session info provided by Twitch [status: {message.Session.Status}]");
                return;
            }

            int waited = 0;
            while (_user.TwitchUserId <= 0 && ++waited < 3)
                await Task.Delay(TimeSpan.FromSeconds(1));

            try
            {
                await _hermes.AuthorizeTwitch();
                var token = await _hermes.FetchTwitchBotToken();
                _api.Initialize(token);
            }
            catch (Exception)
            {
                _logger.Error("Ensure you have your Twitch account linked on TTS. Restart application once you do.");
                return;
            }

            string broadcasterId = _user.TwitchUserId.ToString();
            string[] subscriptionsv1 = [
                "channel.chat.message",
                "channel.chat.message_delete",
                "channel.chat.clear",
                "channel.chat.clear_user_messages",
                "channel.subscribe",
                "channel.subscription.gift",
                "channel.subscription.message",
                "channel.ad_break.begin",
                "channel.ban",
                "channel.channel_points_custom_reward_redemption.add"
            ];
            string[] subscriptionsv2 = [
                "channel.follow",
            ];

            string? pagination = null;
            int size = 0;
            do
            {
                var subscriptionsData = await _api.GetSubscriptions(status: "enabled", broadcasterId: broadcasterId, after: pagination);
                var subscriptionNames = subscriptionsData?.Data == null ? [] : subscriptionsData.Data.Select(s => s.Type).ToArray();

                if (subscriptionNames.Length == 0)
                    break;

                foreach (var d in subscriptionsData!.Data!)
                    sender.AddSubscription(broadcasterId, d.Type, d.Id);

                subscriptionsv1 = subscriptionsv1.Except(subscriptionNames).ToArray();
                subscriptionsv2 = subscriptionsv2.Except(subscriptionNames).ToArray();

                pagination = subscriptionsData?.Pagination?.Cursor;
                size = subscriptionNames.Length;
            } while (size >= 100 && pagination != null && subscriptionsv1.Length + subscriptionsv2.Length > 0);

            foreach (var subscription in subscriptionsv1)
                await Subscribe(sender, subscription, message.Session.Id, broadcasterId, "1");
            foreach (var subscription in subscriptionsv2)
                await Subscribe(sender, subscription, message.Session.Id, broadcasterId, "2");
            
            await Subscribe(sender, "channel.raid", broadcasterId, async () => await _api.CreateChannelRaidEventSubscription("1", message.Session.Id, to: broadcasterId));

            sender.Identify(message.Session.Id);
        }

        private async Task Subscribe(TwitchWebsocketClient sender, string subscriptionName, string sessionId, string broadcasterId, string version)
        {
            try
            {
                var response = await _api.CreateEventSubscription(subscriptionName, version, sessionId, broadcasterId);
                if (response == null)
                {
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

                foreach (var d in response.Data)
                    sender.AddSubscription(broadcasterId, d.Type, d.Id);

                _logger.Information($"Sucessfully added subscription to Twitch websockets [subscription type: {subscriptionName}]");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to create an event subscription [subscription type: {subscriptionName}][reason: exception]");
            }
        }

        private async Task Subscribe(TwitchWebsocketClient sender, string subscriptionName, string broadcasterId, Func<Task<EventResponse<NotificationInfo>?>> subscribe)
        {
            try
            {
                var response = await subscribe();
                if (response == null)
                {
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

                foreach (var d in response.Data)
                    sender.AddSubscription(broadcasterId, d.Type, d.Id);

                _logger.Information($"Sucessfully added subscription to Twitch websockets [subscription type: {subscriptionName}]");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to create an event subscription [subscription type: {subscriptionName}][reason: exception]");
            }
        }
    }
}