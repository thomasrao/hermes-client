using Serilog;
using TwitchChatTTS.Twitch.Redemptions;
using TwitchChatTTS.Twitch.Socket.Messages;

namespace TwitchChatTTS.Twitch.Socket.Handlers
{
    public class ChannelResubscriptionHandler : ITwitchSocketHandler
    {
        public string Name => "channel.subscription.message";

        private readonly IRedemptionManager _redemptionManager;
        private readonly ILogger _logger;

        public ChannelResubscriptionHandler(IRedemptionManager redemptionManager, ILogger logger)
        {
            _redemptionManager = redemptionManager;
            _logger = logger;
        }

        public async Task Execute(TwitchWebsocketClient sender, object data)
        {
            if (data is not ChannelResubscriptionMessage message)
                return;

            _logger.Debug("Resubscription occured.");
            try
            {
                var actions = _redemptionManager.Get("subscription");
                if (!actions.Any())
                {
                    _logger.Debug($"No redemable actions for this subscription was found [message: {message.Message.Text}]");
                    return;
                }
                _logger.Debug($"Found {actions.Count} actions for this Twitch subscription [message: {message.Message.Text}]");

                foreach (var action in actions)
                    try
                    {
                        await _redemptionManager.Execute(action, message.UserName, long.Parse(message.UserId));
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, $"Failed to execute redeeemable action [action: {action.Name}][action type: {action.Type}][redeem: subscription][message: {message.Message.Text}]");
                    }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to fetch the redeemable actions for subscription [message: {message.Message.Text}]");
            }
        }
    }
}