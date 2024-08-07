using Serilog;
using TwitchChatTTS.Twitch.Socket.Messages;

namespace TwitchChatTTS.Twitch.Socket.Handlers
{
    public class ChannelRaidHandler : ITwitchSocketHandler
    {
        public string Name => "channel.raid";

        private readonly TwitchApiClient _api;
        private readonly User _user;
        private readonly ILogger _logger;
        private readonly object _lock;

        public ChannelRaidHandler(TwitchApiClient api, User user, ILogger logger)
        {
            _api = api;
            _user = user;
            _logger = logger;
            _lock = new object();
        }

        public async Task Execute(TwitchWebsocketClient sender, object data)
        {
            if (data is not ChannelRaidMessage message)
                return;

            var chatters = await _api.GetChatters(message.ToBroadcasterUserId, message.ToBroadcasterUserLogin);
            if (chatters?.Data == null)
            {
                _logger.Error("Could not fetch the list of chatters in chat.");
                return;
            }

            var date = DateTime.Now;
            lock (_lock)
            {
                _user.RaidStart = date;
                if (_user.AllowedChatters == null)
                {
                    var chatterIds = chatters.Data.Select(c => long.Parse(c.UserId));
                    _user.AllowedChatters = new HashSet<long>(chatterIds);
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(30));

            lock (_lock)
            {
                if (_user.RaidStart == date)
                {
                    _logger.Information("Raid message spam prevention ended.");
                    _user.RaidStart = null;
                    _user.AllowedChatters = null;
                }
            }
        }
    }
}