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

            _logger.Information($"A raid has started. Starting raid spam prevention. [from: {message.FromBroadcasterUserLogin}][from id: {message.FromBroadcasterUserId}].");
            EventResponse<ChatterMessage>? chatters = null;

            if (!_user.Raids.ContainsKey(message.ToBroadcasterUserId))
            {
                await _api.GetChatters(_user.TwitchUserId.ToString(), _user.TwitchUserId.ToString());
                if (chatters?.Data == null)
                {
                    var extraErrorInfo = _user.TwitchUserId.ToString() != message.ToBroadcasterUserId ? " Ensure you have moderator status in your joined channel(s) to prevent raid spam." : string.Empty;
                    _logger.Error("Could not fetch the list of chatters in chat." + extraErrorInfo);
                    return;
                }
            }

            var startDate = DateTime.Now;
            var endDate = startDate + TimeSpan.FromSeconds(30);
            lock (_lock)
            {
                if (_user.Raids.TryGetValue(message.ToBroadcasterUserId, out var raid))
                    raid.RaidSpamPreventionEndDate = endDate;
                else
                {
                    var chatterIds = chatters!.Data!.Select(c => long.Parse(c.UserId));
                    _user.Raids.Add(message.ToBroadcasterUserId, new RaidInfo(endDate, new HashSet<long>(chatterIds)));
                }
            }

            Task.Run(async () => await EndOfRaidSpamProtection(message.ToBroadcasterUserId, endDate));
        }

        private async Task EndOfRaidSpamProtection(string raidedId, DateTime endDate)
        {
            await Task.Delay(endDate - DateTime.Now);

            lock (_lock)
            {
                if (_user.Raids.TryGetValue(raidedId, out var raid))
                {
                    if (raid.RaidSpamPreventionEndDate == endDate)
                    {
                        _logger.Information("Raid message spam prevention ended.");
                        _user.Raids.Remove(raidedId);
                    }
                    else
                        _logger.Debug("Raid spam prevention would have stopped now if it wasn't for the consecutive raid.");
                }
                else
                    _logger.Error("Something went wrong ending a raid spam prevention.");
            }
        }
    }

    public sealed class RaidInfo
    {
        public DateTime RaidSpamPreventionEndDate { get; set; }
        public HashSet<long> Chatters { get; set; }


        public RaidInfo(DateTime raidEnd, HashSet<long> chatters)
        {
            RaidSpamPreventionEndDate = raidEnd;
            Chatters = chatters;
        }
    }
}