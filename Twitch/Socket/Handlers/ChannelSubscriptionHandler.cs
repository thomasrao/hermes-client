using Serilog;
using TwitchChatTTS.Twitch.Redemptions;
using TwitchChatTTS.Twitch.Socket.Messages;

namespace TwitchChatTTS.Twitch.Socket.Handlers
{
    public class ChannelSubscriptionHandler : ITwitchSocketHandler
    {
        public string Name => "channel.subscribe";

        private readonly IRedemptionManager _redemptionManager;
        private readonly ILogger _logger;

        public ChannelSubscriptionHandler(IRedemptionManager redemptionManager, ILogger logger)
        {
            _redemptionManager = redemptionManager;
            _logger = logger;
        }

        public async Task Execute(TwitchWebsocketClient sender, object data)
        {
            if (data is not ChannelSubscriptionMessage message)
                return;
            if (message.IsGifted)
                return;

            _logger.Debug($"Subscription occured [chatter: {message.UserLogin}][chatter id: {message.UserId}][Tier: {message.Tier}]");
            try
            {
                var actions = _redemptionManager.Get("subscription");
                if (!actions.Any())
                {
                    _logger.Debug($"No redeemable actions for this subscription was found [subscriber: {message.UserLogin}][subscriber id: {message.UserId}]");
                    return;
                }
                _logger.Debug($"Found {actions.Count} actions for this Twitch subscription [subscriber: {message.UserLogin}][subscriber id: {message.UserId}]");

                foreach (var action in actions)
                    try
                    {
                        await _redemptionManager.Execute(action, message.UserName!, long.Parse(message.UserId!));
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, $"Failed to execute redeemable action [action: {action.Name}][action type: {action.Type}][redeem: subscription][subscriber: {message.UserLogin}][subscriber id: {message.UserId}]");
                    }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to fetch the redeemable actions for subscription [subscriber: {message.UserLogin}][subscriber id: {message.UserId}]");
            }
        }
    }
}