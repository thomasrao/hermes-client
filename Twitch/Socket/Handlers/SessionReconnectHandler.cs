using CommonSocketLibrary.Abstract;
using Serilog;
using TwitchChatTTS.Twitch.Socket.Messages;

namespace TwitchChatTTS.Twitch.Socket.Handlers
{
    public class SessionReconnectHandler : ITwitchSocketHandler
    {
        public string Name => "session_reconnect";

        private readonly TwitchApiClient _api;
        private readonly ILogger _logger;

        public SessionReconnectHandler(TwitchApiClient api, ILogger logger)
        {
            _api = api;
            _logger = logger;
        }

        public async Task Execute(TwitchWebsocketClient sender, object? data)
        {
            if (sender == null)
                return;
            if (data == null)
            {
                _logger.Warning("Twitch websocket message data is null.");
                return;
            }
            if (data is not SessionWelcomeMessage message)
                return;
            if (_api == null)
                return;

            if (string.IsNullOrEmpty(message.Session.Id))
            {
                _logger.Warning($"No session info provided by Twitch [status: {message.Session.Status}]");
                return;
            }

            // TODO: Be able to handle multiple websocket connections.
            sender.URL = message.Session.ReconnectUrl;
            await Task.Delay(TimeSpan.FromSeconds(29));
            await sender.DisconnectAsync(new SocketDisconnectionEventArgs("Close", "Twitch asking to reconnect."));
            await sender.Connect();
        }
    }
}