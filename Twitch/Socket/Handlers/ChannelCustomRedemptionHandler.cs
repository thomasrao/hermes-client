using Serilog;
using TwitchChatTTS.Twitch.Redemptions;
using TwitchChatTTS.Twitch.Socket.Messages;

namespace TwitchChatTTS.Twitch.Socket.Handlers
{
    public class ChannelCustomRedemptionHandler : ITwitchSocketHandler
    {
        public string Name => "channel.channel_points_custom_reward_redemption.add";

        private readonly RedemptionManager _redemptionManager;
        private readonly ILogger _logger;

        public ChannelCustomRedemptionHandler(
            RedemptionManager redemptionManager,
            ILogger logger
        )
        {
            _redemptionManager = redemptionManager;
            _logger = logger;
        }

        public async Task Execute(TwitchWebsocketClient sender, object? data)
        {
            if (data is not ChannelCustomRedemptionMessage message)
                return;

            _logger.Information($"Channel Point Reward Redeemed [redeem: {message.Reward.Title}][redeem id: {message.Reward.Id}][transaction: {message.Id}]");

            try
            {
                var actions = _redemptionManager.Get(message.Reward.Id);
                if (!actions.Any())
                {
                    _logger.Debug($"No redemable actions for this redeem was found [redeem: {message.Reward.Title}][redeem id: {message.Reward.Id}][transaction: {message.Id}]");
                    return;
                }
                _logger.Debug($"Found {actions.Count} actions for this Twitch channel point redemption [redeem: {message.Reward.Title}][redeem id: {message.Reward.Id}][transaction: {message.Id}]");

                foreach (var action in actions)
                    try
                    {
                        await _redemptionManager.Execute(action, message.UserName, long.Parse(message.UserId));
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, $"Failed to execute redeeemable action [action: {action.Name}][action type: {action.Type}][redeem: {message.Reward.Title}][redeem id: {message.Reward.Id}][transaction: {message.Id}]");
                    }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to fetch the redeemable actions for a redemption [redeem: {message.Reward.Title}][redeem id: {message.Reward.Id}][transaction: {message.Id}]");
            }
        }
    }
}