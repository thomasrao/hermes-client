using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;
using HermesSocketLibrary.Socket.Data;
using Serilog;

namespace TwitchChatTTS.Hermes.Socket.Handlers
{
    public class LoginAckHandler : IWebSocketHandler
    {
        private readonly User _user;
        private readonly ILogger _logger;
        public int OperationCode { get; } = 2;

        public LoginAckHandler(User user, ILogger logger)
        {
            _user = user;
            _logger = logger;
        }

        public async Task Execute<Data>(SocketClient<WebSocketMessage> sender, Data data)
        {
            if (data is not LoginAckMessage message || message == null)
                return;
            if (sender is not HermesSocketClient client)
                return;

            if (message.AnotherClient && client.LoggedIn)
            {
                _logger.Warning("Another client has connected to the same account.");
                return;
            }
            if (client.LoggedIn)
            {
                _logger.Warning("Attempted to log in again while still logged in.");
                return;
            }

            _user.HermesUserId = message.UserId;
            _user.OwnerId = message.OwnerId;
            client.LoggedIn = true;
            _logger.Information($"Logged in as {_user.TwitchUsername} {(message.WebLogin ? "via web" : "via TTS app")}.");

            await client.FetchTTSVoices();
            await client.FetchEnabledTTSVoices();
            await client.FetchTTSWordFilters();
            await client.FetchTTSChatterVoices();
            await client.FetchDefaultTTSVoice();
            await client.FetchChatterIdentifiers();
            await client.FetchEmotes();
            await client.FetchRedemptions();
            await client.FetchPermissions();

            _logger.Information("TTS is now ready.");
            client.Ready = true;
        }
    }
}