using System.Text.RegularExpressions;
using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;
using HermesSocketLibrary.Socket.Data;
using Serilog;

namespace TwitchChatTTS.Hermes.Socket.Handlers
{
    public class LoginAckHandler : IWebSocketHandler
    {
        private readonly User _user;
        private readonly NightbotApiClient _nightbot;
        private readonly ILogger _logger;
        public int OperationCode { get; } = 2;

        public LoginAckHandler(User user, NightbotApiClient nightbot, ILogger logger)
        {
            _user = user;
            _nightbot = nightbot;
            _logger = logger;
        }

        public async Task Execute<Data>(SocketClient<WebSocketMessage> sender, Data data)
        {
            if (data is not LoginAckMessage message || message == null)
                return;
            if (sender is not HermesSocketClient client)
                return;

            if (message.AnotherClient)
            {
                if (client.LoggedIn)
                    _logger.Warning($"Another client has connected to the same account via {(message.WebLogin ? "web login" : "application")}.");
                return;
            }
            if (client.LoggedIn)
            {
                _logger.Error("Attempted to log in again while still logged in.");
                return;
            }

            _user.HermesUserId = message.UserId;
            _user.OwnerId = message.OwnerId;
            _user.DefaultTTSVoice = message.DefaultTTSVoice;
            _user.VoicesAvailable = message.TTSVoicesAvailable;
            _user.VoicesEnabled = new HashSet<string>(message.EnabledTTSVoices);
            _user.TwitchConnection = message.Connections.FirstOrDefault(c => c.Default && c.Type == "twitch");
            _user.NightbotConnection = message.Connections.FirstOrDefault(c => c.Default && c.Type == "nightbot");

            var filters = message.WordFilters.Where(f => f.Search != null && f.Replace != null).ToArray();
            foreach (var filter in filters)
            {
                try
                {
                    var re = new Regex(filter.Search!, RegexOptions.Compiled);
                    re.Match(string.Empty);
                    filter.Regex = re;
                }
                catch (Exception) { }
            }
            _user.RegexFilters = filters;

            client.LoggedIn = true;
            _logger.Information($"Logged in as {_user.TwitchUsername} {(message.WebLogin ? "via web" : "via TTS app")}.");

            await client.FetchTTSChatterVoices();
            await client.FetchChatterIdentifiers();
            await client.FetchEmotes();
            await client.FetchRedemptions();
            await client.FetchPermissions();

            if (_user.NightbotConnection != null)
            {
                _nightbot.Initialize(_user.NightbotConnection.ClientId, _user.NightbotConnection.AccessToken);
                var span = _user.NightbotConnection.ExpiresAt - DateTime.Now;
                var timeLeft = span.Days >= 2 ? span.Days + " days" : (span.Hours >= 2 ? span.Hours + " hours" : span.Minutes + " minutes");
                if (span.Days >= 3)
                    _logger.Information($"Nightbot connection has {timeLeft} before it is revoked.");
                else if (span.Minutes >= 0)
                    _logger.Warning($"Nightbot connection has {timeLeft} before it is revoked. Refreshing the token is soon required.");
                else
                    _logger.Error("Nightbot connection has its permissions revoked. Refresh the token. Anything related to Nightbot from this application will not work.");
            }

            _logger.Information("TTS is now ready.");
            client.Ready = true;
        }
    }
}