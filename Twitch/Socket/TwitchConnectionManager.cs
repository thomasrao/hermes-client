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
                    if (_identified == client)
                    {
                        _logger.Warning("Twitch client has been re-identified.");
                        return;
                    }
                    if (_backup != client)
                    {
                        _logger.Warning("Twitch client has been identified, but isn't backup. Disconnecting.");
                        clientDisconnect = true;
                        return;
                    }

                    if (_identified != null)
                    {
                        _logger.Debug("Second Twitch client has been identified; hopefully a reconnection.");
                        return;
                    }

                    _identified = _backup;
                    _backup = null;
                }

                if (clientDisconnect)
                    await client.DisconnectAsync(new SocketDisconnectionEventArgs("Closed", "No need for a tertiary client."));

                _logger.Information("Twitch client has been identified.");
            };
            client.OnDisconnected += (s, e) =>
            {
                lock (_lock)
                {
                    if (_identified == client)
                    {
                        _logger.Debug("Identified Twitch client has disconnected.");
                        _identified = null;
                    }
                    else if (_backup == client)
                    {
                        _logger.Debug("Backup Twitch client has disconnected.");
                        _backup = null;
                    }
                    else
                        _logger.Error("Twitch client disconnection from unknown source.");
                }
            };

            _logger.Debug("Created a Twitch websocket client.");
            return client;
        }
    }
}