using Serilog;
using TwitchChatTTS.Twitch.Redemptions;
using TwitchChatTTS.Twitch.Socket.Messages;

namespace TwitchChatTTS.Twitch.Socket.Handlers
{
    public class ChannelSubscriptionGiftHandler : ITwitchSocketHandler
    {
        public string Name => "channel.subscription.gift";

        private readonly IRedemptionManager _redemptionManager;
        private readonly ILogger _logger;

        public ChannelSubscriptionGiftHandler(IRedemptionManager redemptionManager, ILogger logger)
        {
            _redemptionManager = redemptionManager;
            _logger = logger;
        }

        public async Task Execute(TwitchWebsocketClient sender, object data)
        {
            if (data is not ChannelSubscriptionGiftMessage message)
                return;

            _logger.Debug("Gifted subscription occured.");
            try
            {
                var actions = _redemptionManager.Get("subscription.gift");
                if (!actions.Any())
                {
                    _logger.Debug($"No redeemable actions for this gifted subscription was found");
                    return;
                }
                _logger.Debug($"Found {actions.Count} actions for this Twitch gifted subscription [gifted: {message.UserLogin}][gifted id: {message.UserId}][Anonymous: {message.IsAnonymous}][cumulative: {message.CumulativeTotal ?? -1}]");

                foreach (var action in actions)
                    try
                    {
                        await _redemptionManager.Execute(action, message.UserName, long.Parse(message.UserId));
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, $"Failed to execute redeemable action [action: {action.Name}][action type: {action.Type}][redeem: gifted subscription]");
                    }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to fetch the redeemable actions for gifted subscription");
            }
        }
    }
}