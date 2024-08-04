using Serilog;
using TwitchChatTTS.Twitch.Socket.Messages;

namespace TwitchChatTTS.Twitch.Socket.Handlers
{
    public class ChannelBanHandler : ITwitchSocketHandler
    {
        public string Name => "channel.ban";

        private readonly ILogger _logger;

        public ChannelBanHandler(ILogger logger)
        {
            _logger = logger;
        }

        public Task Execute(TwitchWebsocketClient sender, object? data)
        {
            if (data is not ChannelBanMessage message)
                return Task.CompletedTask;

            _logger.Warning($"Chatter banned [chatter: {message.UserLogin}][chatter id: {message.UserId}][End: {(message.IsPermanent ? "Permanent" : message.EndsAt.ToString())}]");
            return Task.CompletedTask;
        }
    }
}