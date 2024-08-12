using CommonSocketLibrary.Abstract;
using Serilog;
using TwitchChatTTS.Twitch.Socket.Messages;

namespace TwitchChatTTS.Twitch.Socket.Handlers
{
    public class SessionReconnectHandler : ITwitchSocketHandler
    {
        public string Name => "session_reconnect";

        private readonly ITwitchConnectionManager _manager;
        private readonly ILogger _logger;

        public SessionReconnectHandler(ITwitchConnectionManager manager, ILogger logger)
        {
            _manager = manager;
            _logger = logger;
        }

        public async Task Execute(TwitchWebsocketClient sender, object data)
        {
            if (sender == null)
                return;
            if (data is not SessionWelcomeMessage message)
                return;

            if (string.IsNullOrEmpty(message.Session.Id))
            {
                _logger.Warning($"No session id provided by Twitch [status: {message.Session.Status}]");
                return;
            }

            if (message.Session.ReconnectUrl == null)
            {
                _logger.Warning($"No reconnection info provided by Twitch [status: {message.Session.Status}]");
                return;
            }

            sender.ReceivedReconnecting = true;

            var backup = _manager.GetBackupClient();
            var identified = _manager.GetWorkingClient();
            _logger.Debug($"Reconnection received [receiver: {sender.UID}][main: {identified.UID}][backup: {backup.UID}]");

            backup.URL = message.Session.ReconnectUrl;
            backup.TwitchReconnected = true;
            await backup.Connect();
        }
    }
}