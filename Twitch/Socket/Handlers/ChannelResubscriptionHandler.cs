using Serilog;
using TwitchChatTTS.Chat.Messaging;
using TwitchChatTTS.Twitch.Redemptions;
using TwitchChatTTS.Twitch.Socket.Messages;

namespace TwitchChatTTS.Twitch.Socket.Handlers
{
    public class ChannelResubscriptionHandler : ITwitchSocketHandler
    {
        public string Name => "channel.subscription.message";

        private readonly ChatMessageReader _reader;
        private readonly IRedemptionManager _redemptionManager;
        private readonly ILogger _logger;

        public ChannelResubscriptionHandler(ChatMessageReader reader, IRedemptionManager redemptionManager, ILogger logger)
        {
            _reader = reader;
            _redemptionManager = redemptionManager;
            _logger = logger;
        }

        public async Task Execute(TwitchWebsocketClient sender, object data)
        {
            if (data is not ChannelResubscriptionMessage message)
                return;

            _logger.Debug($"Resubscription occured [chatter: {message.UserLogin}][chatter id: {message.UserId}][Tier: {message.Tier}][Streak: {message.StreakMonths}][Cumulative: {message.CumulativeMonths}][Duration: {message.DurationMonths}]");
            try
            {
                var actions = _redemptionManager.Get("subscription");
                if (!actions.Any())
                {
                    _logger.Debug($"No redeemable actions for this subscription was found [message: {message.Message.Text}]");
                    return;
                }
                _logger.Debug($"Found {actions.Count} actions for this Twitch subscription [message: {message.Message.Text}]");

                foreach (var action in actions)
                    try
                    {
                        await _redemptionManager.Execute(action, message.UserName!, long.Parse(message.UserId!));
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, $"Failed to execute redeemable action [action: {action.Name}][action type: {action.Type}][redeem: resubscription][message: {message.Message.Text}]");
                    }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to fetch the redeemable actions for subscription [message: {message.Message.Text}]");
            }
        }
    }
}