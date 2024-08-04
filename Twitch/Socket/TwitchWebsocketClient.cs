using CommonSocketLibrary.Abstract;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System.Text.Json;
using System.Net.WebSockets;
using TwitchChatTTS.Twitch.Socket.Messages;
using System.Text;
using TwitchChatTTS.Twitch.Socket.Handlers;

namespace TwitchChatTTS.Twitch.Socket
{
    public class TwitchWebsocketClient : SocketClient<TwitchWebsocketMessage>
    {
        public string URL;

        private IDictionary<string, ITwitchSocketHandler> _handlers;
        private IDictionary<string, Type> _messageTypes;
        private readonly Configuration _configuration;
        private System.Timers.Timer _reconnectTimer;

        public bool Connected { get; set; }
        public bool Identified { get; set; }
        public string SessionId { get; set; }


        public TwitchWebsocketClient(
            Configuration configuration,
            [FromKeyedServices("twitch")] IEnumerable<ITwitchSocketHandler> handlers,
            ILogger logger
        ) : base(logger, new JsonSerializerOptions()
        {
            PropertyNameCaseInsensitive = false,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        })
        {
            _handlers = handlers.ToDictionary(h => h.Name, h => h);
            _configuration = configuration;

            _reconnectTimer = new System.Timers.Timer(TimeSpan.FromSeconds(30));
            _reconnectTimer.AutoReset = false;
            _reconnectTimer.Elapsed += async (sender, e) => await Reconnect();
            _reconnectTimer.Enabled = false;

            _messageTypes = new Dictionary<string, Type>();
            _messageTypes.Add("session_welcome", typeof(SessionWelcomeMessage));
            _messageTypes.Add("session_reconnect", typeof(SessionWelcomeMessage));
            _messageTypes.Add("notification", typeof(NotificationMessage));

            URL = "wss://eventsub.wss.twitch.tv/ws";
        }


        public void Initialize()
        {
            _logger.Information($"Initializing OBS websocket client.");
            OnConnected += (sender, e) =>
            {
                Connected = true;
                _reconnectTimer.Enabled = false;
                _logger.Information("Twitch websocket client connected.");
            };

            OnDisconnected += (sender, e) =>
            {
                _reconnectTimer.Enabled = Identified;
                _logger.Information($"Twitch websocket client disconnected [status: {e.Status}][reason: {e.Reason}] " + (Identified ? "Will be attempting to reconnect every 30 seconds." : "Will not be attempting to reconnect."));

                Connected = false;
                Identified = false;
            };
        }

        public async Task Connect()
        {
            if (string.IsNullOrWhiteSpace(URL))
            {
                _logger.Warning("Lacking connection info for Twitch websockets. Not connecting to Twitch.");
                return;
            }

            _logger.Debug($"Twitch websocket client attempting to connect to {URL}");
            try
            {
                await ConnectAsync(URL);
            }
            catch (Exception)
            {
                _logger.Warning("Connecting to twitch failed. Skipping Twitch websockets.");
            }
        }

        private async Task Reconnect()
        {
            if (Connected)
            {
                try
                {
                    await DisconnectAsync(new SocketDisconnectionEventArgs(WebSocketCloseStatus.Empty.ToString(), ""));
                }
                catch (Exception)
                {
                    _logger.Error("Failed to disconnect from Twitch websocket server.");
                }
            }

            try
            {
                await Connect();
            }
            catch (WebSocketException wse) when (wse.Message.Contains("502"))
            {
                _logger.Error("Twitch websocket server cannot be found.");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to reconnect to Twitch websocket server.");
            }
        }

        protected TwitchWebsocketMessage GenerateMessage<T>(string messageType, T data)
        {
            var metadata = new TwitchMessageMetadata()
            {
                MessageId = Guid.NewGuid().ToString(),
                MessageType = messageType,
                MessageTimestamp = DateTime.UtcNow
            };
            return new TwitchWebsocketMessage()
            {
                Metadata = metadata,
                Payload = data
            };
        }

        protected override async Task OnResponseReceived(TwitchWebsocketMessage? message)
        {
            if (message == null || message.Metadata == null) {
                _logger.Information("Twitch message is null");
                return;
            }

            string content = message.Payload?.ToString() ?? string.Empty;
            if (message.Metadata.MessageType != "session_keepalive")
                _logger.Information("Twitch RX #" + message.Metadata.MessageType + ": " + content);

            if (!_messageTypes.TryGetValue(message.Metadata.MessageType, out var type) || type == null)
            {
                _logger.Debug($"Could not find Twitch message type [message type: {message.Metadata.MessageType}]");
                return;
            }

            if (!_handlers.TryGetValue(message.Metadata.MessageType, out ITwitchSocketHandler? handler) || handler == null)
            {
                _logger.Debug($"Could not find Twitch handler [message type: {message.Metadata.MessageType}]");
                return;
            }

            var data = JsonSerializer.Deserialize(content, type, _options);
            await handler.Execute(this, data);
        }

        public async Task Send<T>(string type, T data)
        {
            if (_socket == null || type == null || data == null)
                return;

            try
            {
                var message = GenerateMessage(type, data);
                var content = JsonSerializer.Serialize(message, _options);

                var bytes = Encoding.UTF8.GetBytes(content);
                var array = new ArraySegment<byte>(bytes);
                var total = bytes.Length;
                var current = 0;

                while (current < total)
                {
                    var size = Encoding.UTF8.GetBytes(content.Substring(current), array);
                    await _socket!.SendAsync(array, WebSocketMessageType.Text, current + size >= total, _cts!.Token);
                    current += size;
                }
                _logger.Information("TX #" + type + ": " + content);
            }
            catch (Exception e)
            {
                if (_socket.State.ToString().Contains("Close") || _socket.State == WebSocketState.Aborted)
                {
                    await DisconnectAsync(new SocketDisconnectionEventArgs(_socket.CloseStatus.ToString()!, _socket.CloseStatusDescription ?? string.Empty));
                    _logger.Warning($"Socket state on closing = {_socket.State} | {_socket.CloseStatus?.ToString()} | {_socket.CloseStatusDescription}");
                }
                _logger.Error(e, $"Failed to send a websocket message [message type: {type}]");
            }
        }
    }
}