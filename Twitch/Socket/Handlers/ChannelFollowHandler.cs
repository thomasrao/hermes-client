using Serilog;
using TwitchChatTTS.Twitch.Redemptions;
using TwitchChatTTS.Twitch.Socket.Messages;

namespace TwitchChatTTS.Twitch.Socket.Handlers
{
    public class ChannelFollowHandler : ITwitchSocketHandler
    {
        public string Name => "channel.follow";

        private readonly IRedemptionManager _redemptionManager;
        private readonly ILogger _logger;

        public ChannelFollowHandler(IRedemptionManager redemptionManager, ILogger logger)
        {
            _redemptionManager = redemptionManager;
            _logger = logger;
        }

        public async Task Execute(TwitchWebsocketClient sender, object data)
        {
            if (data is not ChannelFollowMessage message)
                return;
            
            _logger.Information($"User followed [chatter: {message.UserLogin}][chatter id: {message.UserId}]");
            try
            {
                var actions = _redemptionManager.Get("follow");
                if (!actions.Any())
                {
                    _logger.Debug($"No redemable actions for follow was found");
                    return;
                }
                _logger.Debug($"Found {actions.Count} actions for this Twitch follow");

                foreach (var action in actions)
                    try
                    {
                        await _redemptionManager.Execute(action, message.UserName, long.Parse(message.UserId));
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, $"Failed to execute redeeemable action [action: {action.Name}][action type: {action.Type}][redeem: follow]");
                    }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to fetch the redeemable actions for follow");
            }
        }
    }
}