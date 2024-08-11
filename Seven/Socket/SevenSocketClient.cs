using CommonSocketLibrary.Common;
using CommonSocketLibrary.Abstract;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using TwitchChatTTS.Seven.Socket.Data;
using System.Text.Json;
using CommonSocketLibrary.Backoff;

namespace TwitchChatTTS.Seven.Socket
{
    public class SevenSocketClient : WebSocketClient
    {
        private readonly User _user;
        private readonly string[] _errorCodes;
        private readonly int[] _reconnectDelay;
        private string? URL;

        private readonly IBackoff _backoff;

        public bool Connected { get; set; }

        public SevenHelloMessage? ConnectionDetails { get; set; }

        public SevenSocketClient(
            User user,
            [FromKeyedServices("7tv")] IBackoff backoff,
            [FromKeyedServices("7tv")] IEnumerable<IWebSocketHandler> handlers,
            [FromKeyedServices("7tv")] MessageTypeManager<IWebSocketHandler> typeManager,
            ILogger logger
        ) : base(handlers, typeManager, new JsonSerializerOptions()
        {
            PropertyNameCaseInsensitive = false,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        }, logger)
        {
            _user = user;
            _backoff = backoff;
            ConnectionDetails = null;

            _errorCodes = [
                "Server Error",
                "Unknown Operation",
                "Invalid Payload",
                "Auth Failure",
                "Already Identified",
                "Rate Limited",
                "Restart",
                "Maintenance",
                "Timeout",
                "Already Subscribed",
                "Not Subscribed",
                "Insufficient Privilege",
                "Inactivity?"
            ];
            _reconnectDelay = [
                1000,
                -1,
                -1,
                -1,
                0,
                3000,
                1000,
                300000,
                1000,
                0,
                0,
                1000,
                1000
            ];
        }


        public void Initialize()
        {
            _logger.Information("Initializing 7tv websocket client.");
            OnConnected += (sender, e) =>
            {
                Connected = true;
                _logger.Information("7tv websocket client connected.");
            };

            OnDisconnected += (sender, e) => OnDisconnection(sender, e);

            if (!string.IsNullOrEmpty(_user.SevenEmoteSetId))
                URL = $"{SevenApiClient.WEBSOCKET_URL}@emote_set.*<object_id={_user.SevenEmoteSetId}>";
        }

        public async Task Connect()
        {
            if (string.IsNullOrEmpty(URL))
            {
                _logger.Warning("Cannot find 7tv url. Not connecting to 7tv websockets.");
                return;
            }
            if (string.IsNullOrWhiteSpace(_user.SevenEmoteSetId))
            {
                _logger.Warning("Cannot find 7tv data for your channel. Not connecting to 7tv websockets.");
                return;
            }

            _logger.Debug($"7tv client attempting to connect to {URL}");
            try
            {
                await ConnectAsync(URL);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Could not connect to 7tv websocket.");
            }
        }

        private async void OnDisconnection(object? sender, SocketDisconnectionEventArgs e)
        {
            Connected = false;

            if (int.TryParse(e.Reason, out int code))
            {
                if (code >= 0 && code < _errorCodes.Length)
                    _logger.Warning($"Received end of stream message for 7tv websocket [reason: {_errorCodes[code]}][code: {code}]");
                else
                    _logger.Warning($"Received end of stream message for 7tv websocket [code: {code}]");

                if (code < 0 || code >= _reconnectDelay.Length)
                    await Task.Delay(TimeSpan.FromSeconds(30));
                else if (_reconnectDelay[code] < 0)
                {
                    _logger.Error($"7tv client will remain disconnected due to a bad client implementation.");
                    return;
                }
                else if (_reconnectDelay[code] > 1000)
                    await Task.Delay(_reconnectDelay[code] - 1000);
            }
            else
            {
                _logger.Warning("Unknown 7tv disconnection.");
            }

            Task.Run(async () =>
            {
                await Reconnect(_backoff, async () => await Connect());
                await Task.Delay(TimeSpan.FromMilliseconds(500));

                if (Connected && ConnectionDetails?.SessionId != null)
                {
                    await Send(34, new ResumeMessage() { SessionId = ConnectionDetails.SessionId });
                    _logger.Debug("Resumed connection to 7tv websocket.");
                }
                else
                {
                    _logger.Debug("Resumed connection to 7tv websocket on a different session.");
                }
            });
        }
    }
}