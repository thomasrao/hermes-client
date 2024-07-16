using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace TwitchChatTTS.Seven.Socket
{
    public class SevenManager
    {
        private readonly User _user;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger _logger;
        private string URL;

        public bool Connected { get; set; }
        public bool Streaming { get; set; }


        public SevenManager(User user, IServiceProvider serviceProvider, ILogger logger)
        {
            _user = user;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public void Initialize() {
            _logger.Information("Initializing 7tv websocket client.");
            var client = _serviceProvider.GetRequiredKeyedService<SocketClient<WebSocketMessage>>("7tv");

            client.OnConnected += (sender, e) => {
                Connected = true;
                _logger.Information("7tv websocket client connected.");
            };

            client.OnDisconnected += (sender, e) => {
                Connected = false;
                _logger.Information("7tv websocket client disconnected.");
            };

            if (!string.IsNullOrEmpty(_user.SevenEmoteSetId))
                URL = $"{SevenApiClient.WEBSOCKET_URL}@emote_set.*<object_id={_user.SevenEmoteSetId}>";
        }

        public async Task Connect()
        {
            if (string.IsNullOrWhiteSpace(_user.SevenEmoteSetId))
            {
                _logger.Warning("Cannot find 7tv data for your channel. Not connecting to 7tv websockets.");
                return;
            }

            var client = _serviceProvider.GetRequiredKeyedService<SocketClient<WebSocketMessage>>("7tv");
            _logger.Debug($"7tv client attempting to connect to {URL}");
            await client.ConnectAsync($"{URL}");
        }
    }
}