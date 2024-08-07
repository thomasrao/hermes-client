using CommonSocketLibrary.Abstract;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using TwitchChatTTS.Twitch.Socket.Messages;

namespace TwitchChatTTS.Twitch.Socket
{
    public interface ITwitchConnectionManager
    {
        TwitchWebsocketClient GetWorkingClient();
        TwitchWebsocketClient GetBackupClient();
    }

    public class TwitchConnectionManager : ITwitchConnectionManager
    {
        private TwitchWebsocketClient? _identified;
        private TwitchWebsocketClient? _backup;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger _logger;

        private readonly object _lock;

        public TwitchConnectionManager(IServiceProvider serviceProvider, ILogger logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;

            _lock = new object();
        }


        public TwitchWebsocketClient GetBackupClient()
        {
            lock (_lock)
            {
                if (_identified == null)
                    throw new InvalidOperationException("Cannot get backup Twitch client yet. Waiting for identification.");
                if (_backup != null)
                    return _backup;

                return CreateNewClient();
            }
        }

        public TwitchWebsocketClient GetWorkingClient()
        {
            lock (_lock)
            {
                if (_identified == null)
                {
                    return CreateNewClient();
                }

                return _identified;
            }
        }

        private TwitchWebsocketClient CreateNewClient()
        {
            if (_backup != null)
                return _backup;

            var client = (_serviceProvider.GetRequiredKeyedService<SocketClient<TwitchWebsocketMessage>>("twitch-create") as TwitchWebsocketClient)!;
            client.Initialize();
            _backup = client;

            client.OnIdentified += async (s, e) =>
            {
                bool clientDisconnect = false;
                lock (_lock)
                {
                    if (_identified == null || _identified.ReceivedReconnecting)
                    {
                        if (_backup != null && _backup.UID == client.UID)
                        {
                            _logger.Information($"Twitch client has been identified [client: {client.UID}][main: {_identified?.UID}][backup: {_backup.UID}]");
                            _identified = _backup;
                            _backup = null;
                        }
                        else
                            _logger.Warning($"Twitch client identified from unknown sources [client: {client.UID}][main: {_identified?.UID}][backup: {_backup?.UID}]");
                    }
                    else if (_identified.UID == client.UID)
                    {
                        _logger.Warning($"Twitch client has been re-identified [client: {client.UID}][main: {_identified.UID}][backup: {_backup?.UID}]");
                    }
                    else if (_backup == null || _backup.UID != client.UID)
                    {
                        _logger.Warning($"Twitch client has been identified, but isn't main or backup [client: {client.UID}][main: {_identified.UID}][backup: {_backup?.UID}]");
                        clientDisconnect = true;
                    }
                }

                if (clientDisconnect)
                    await client.DisconnectAsync(new SocketDisconnectionEventArgs("Closed", "No need for a tertiary client."));
            };
            client.OnDisconnected += async (s, e) =>
            {
                bool reconnecting = false;
                lock (_lock)
                {
                    if (_identified?.UID == client.UID)
                    {
                        _logger.Warning($"Identified Twitch client has disconnected [client: {client.UID}][main: {_identified.UID}][backup: {_backup?.UID}]");
                        _identified = null;
                        reconnecting = true;
                    }
                    else if (_backup?.UID == client.UID)
                    {
                        _logger.Warning($"Backup Twitch client has disconnected [client: {client.UID}][main: {_identified?.UID}][backup: {_backup.UID}]");
                        _backup = null;
                    }
                    else if (client.ReceivedReconnecting)
                    {
                        _logger.Debug($"Twitch client disconnected due to reconnection [client: {client.UID}][main: {_identified?.UID}][backup: {_backup?.UID}]");
                    }
                    else
                        _logger.Error($"Twitch client disconnected from unknown source [client: {client.UID}][main: {_identified?.UID}][backup: {_backup?.UID}]");
                }

                if (reconnecting)
                {
                    var client = GetWorkingClient();
                    await client.Connect();
                }
            };

            _logger.Debug("Created a Twitch websocket client.");
            return client;
        }
    }
}