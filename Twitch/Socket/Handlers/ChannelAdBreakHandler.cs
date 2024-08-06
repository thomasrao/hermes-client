using Serilog;
using TwitchChatTTS.Twitch.Redemptions;
using TwitchChatTTS.Twitch.Socket.Messages;

namespace TwitchChatTTS.Twitch.Socket.Handlers
{
    public class ChannelAdBreakHandler : ITwitchSocketHandler
    {
        public string Name => "channel.ad_break.begin";

        private readonly IRedemptionManager _redemptionManager;
        private readonly ILogger _logger;

        public ChannelAdBreakHandler(IRedemptionManager redemptionManager, ILogger logger)
        {
            _redemptionManager = redemptionManager;
            _logger = logger;
        }

        public async Task Execute(TwitchWebsocketClient sender, object data)
        {
            if (data is not ChannelAdBreakMessage message)
                return;

            bool isAutomatic = message.IsAutomatic == "true";
            if (isAutomatic)
                _logger.Information($"Ad break has begun [duration: {message.DurationSeconds} seconds][automatic: {isAutomatic}]");
            else
                _logger.Information($"Ad break has begun [duration: {message.DurationSeconds} seconds][requester: {message.RequesterUserLogin}][requester id: {message.RequesterUserId}]");

            try
            {
                var actions = _redemptionManager.Get("adbreak");
                if (!actions.Any())
                {
                    _logger.Debug($"No redeemable actions for ad break was found");
                    return;
                }
                _logger.Debug($"Found {actions.Count} actions for this Twitch ad break");

                foreach (var action in actions)
                    try
                    {
                        await _redemptionManager.Execute(action, message.RequesterUserLogin, long.Parse(message.RequesterUserId));
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, $"Failed to execute redeemable action [action: {action.Name}][action type: {action.Type}][redeem: ad break]");
                    }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to fetch the redeemable actions for ad break");
            }
        }
    }
}