using Serilog;
using TwitchChatTTS.Twitch.Redemptions;
using TwitchChatTTS.Twitch.Socket.Messages;

namespace TwitchChatTTS.Twitch.Socket.Handlers
{
    public class ChannelAdBreakBeginHandler : ITwitchSocketHandler
    {
        public string Name => "channel.ad_break.begin";

        private readonly IRedemptionManager _redemptionManager;
        private readonly ILogger _logger;

        public ChannelAdBreakBeginHandler(IRedemptionManager redemptionManager, ILogger logger)
        {
            _redemptionManager = redemptionManager;
            _logger = logger;
        }

        public async Task Execute(TwitchWebsocketClient sender, object data)
        {
            if (data is not ChannelAdBreakMessage message)
                return;

            if (message.IsAutomatic)
                _logger.Information($"Ad break has begun [duration: {message.DurationSeconds} seconds][automatic: true]");
            else
                _logger.Information($"Ad break has begun [duration: {message.DurationSeconds} seconds][requester: {message.RequesterUserLogin}][requester id: {message.RequesterUserId}]");

            try
            {
                var actions = _redemptionManager.Get("adbreak_begin");
                if (!actions.Any())
                {
                    _logger.Debug($"Found {actions.Count} actions for this Twitch ad break");

                    foreach (var action in actions)
                        try
                        {
                            await _redemptionManager.Execute(action, message.RequesterUserLogin, long.Parse(message.RequesterUserId));
                        }
                        catch (Exception ex)
                        {
                            _logger.Error(ex, $"Failed to execute redeemable action [action: {action.Name}][action type: {action.Type}][redeem: ad break begin]");
                        }
                }
                else
                    _logger.Debug($"No redeemable actions for ad break begin was found");

                Task.Run(async () =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(message.DurationSeconds));
                    if (message.IsAutomatic)
                        _logger.Information($"Ad break has ended [duration: {message.DurationSeconds} seconds][automatic: true]");
                    else
                        _logger.Information($"Ad break has ended [duration: {message.DurationSeconds} seconds][requester: {message.RequesterUserLogin}][requester id: {message.RequesterUserId}]");
                    
                    actions = _redemptionManager.Get("adbreak_end");
                    if (!actions.Any())
                    {
                        _logger.Debug($"Found {actions.Count} actions for this Twitch ad break");

                        foreach (var action in actions)
                            try
                            {
                                await _redemptionManager.Execute(action, message.RequesterUserLogin, long.Parse(message.RequesterUserId));
                            }
                            catch (Exception ex)
                            {
                                _logger.Error(ex, $"Failed to execute redeemable action [action: {action.Name}][action type: {action.Type}][redeem: ad break end]");
                            }
                    }
                    else
                        _logger.Debug($"No redeemable actions for ad break end was found");
                });
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to fetch the redeemable actions for ad break begin");
            }
        }
    }
}