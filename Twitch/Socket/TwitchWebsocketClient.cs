using CommonSocketLibrary.Abstract;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System.Text.Json;
using System.Net.WebSockets;
using TwitchChatTTS.Twitch.Socket.Messages;
using System.Text;
using TwitchChatTTS.Twitch.Socket.Handlers;
using CommonSocketLibrary.Backoff;

namespace TwitchChatTTS.Twitch.Socket
{
    public class TwitchWebsocketClient : SocketClient<TwitchWebsocketMessage>
    {
        private readonly IDictionary<string, ITwitchSocketHandler> _handlers;
        private readonly IDictionary<string, Type> _messageTypes;
        private readonly IDictionary<string, string> _subscriptions;
        private readonly IBackoff _backoff;
        private readonly Configuration _configuration;
        private DateTime _lastReceivedMessageTimestamp;
        private bool _disconnected;
        private readonly object _lock;

        public event EventHandler<EventArgs> OnIdentified;

        public string UID { get; }
        public string URL;
        public bool Connected { get; private set; }
        public bool Identified { get; private set; }
        public string SessionId { get; private set; }
        public bool ReceivedReconnecting { get; set; }
        public bool TwitchReconnected { get; set; }


        public TwitchWebsocketClient(
            [FromKeyedServices("twitch")] IEnumerable<ITwitchSocketHandler> handlers,
            [FromKeyedServices("twitch")] IBackoff backoff,
            Configuration configuration,
            ILogger logger
        ) : base(logger, new JsonSerializerOptions()
        {
            PropertyNameCaseInsensitive = false,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        })
        {
            _handlers = handlers.ToDictionary(h => h.Name, h => h);
            _backoff = backoff;
            _configuration = configuration;
            _subscriptions = new Dictionary<string, string>();
            _lock = new object();

            _messageTypes = new Dictionary<string, Type>();
            _messageTypes.Add("session_keepalive", typeof(object));
            _messageTypes.Add("session_welcome", typeof(SessionWelcomeMessage));
            _messageTypes.Add("session_reconnect", typeof(SessionWelcomeMessage));
            _messageTypes.Add("notification", typeof(NotificationMessage));

            UID = Guid.NewGuid().ToString("D");

            if (_configuration.Environment == "PROD" || string.IsNullOrWhiteSpace(_configuration.Twitch?.WebsocketUrl))
                URL = "wss://eventsub.wss.twitch.tv/ws";
            else
                URL = _configuration.Twitch.WebsocketUrl;
        }


        public void AddSubscription(string broadcasterId, string type, string id)
        {
            if (_subscriptions.ContainsKey(broadcasterId + '|' + type))
                _subscriptions[broadcasterId + '|' + type] = id;
            else
                _subscriptions.Add(broadcasterId + '|' + type, id);
        }

        public string? GetSubscriptionId(string broadcasterId, string type)
        {
            if (_subscriptions.TryGetValue(broadcasterId + '|' + type, out var id))
                return id;
            return null;
        }

        public void RemoveSubscription(string broadcasterId, string type)
        {
            _subscriptions.Remove(broadcasterId + '|' + type);
        }

        public void Initialize()
        {
            _logger.Information($"Initializing Twitch websocket client.");
            OnConnected += (sender, e) =>
            {
                Connected = true;
                _logger.Information("Twitch websocket client connected.");
                _disconnected = false;
            };

            OnDisconnected += async (sender, e) =>
            {
                lock (_lock)
                {
                    if (_disconnected)
                        return;

                    _disconnected = true;
                }

                _logger.Information($"Twitch websocket client disconnected [status: {e.Status}][reason: {e.Reason}][client: {UID}]");

                Connected = false;
                Identified = false;
            };
        }

        public override async Task Connect()
        {
            if (string.IsNullOrWhiteSpace(URL))
            {
                _logger.Warning("Lacking connection info for Twitch websockets. Not connecting to Twitch.");
                return;
            }

            _logger.Debug($"Twitch websocket client attempting to connect to {URL}");
            await ConnectAsync(URL);
        }

        public async Task Reconnect() => await Reconnect(_backoff);

        public void Identify(string sessionId)
        {
            Identified = true;
            SessionId = sessionId;
            OnIdentified?.Invoke(this, EventArgs.Empty);
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

        protected override Task OnResponseReceived(TwitchWebsocketMessage? message)
        {
            return Task.Run(async () =>
            {
                if (message == null || message.Metadata == null)
                {
                    _logger.Information("Twitch message is null");
                    return;
                }

                _lastReceivedMessageTimestamp = DateTime.UtcNow;

                string content = message.Payload?.ToString() ?? string.Empty;
                if (message.Metadata.MessageType != "session_keepalive")
                    _logger.Debug("Twitch RX #" + message.Metadata.MessageType + ": " + content);

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
                if (data == null)
                {
                    _logger.Warning("Twitch websocket message payload is null.");
                    return;
                }
                await handler.Execute(this, data);
            });
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
                _logger.Debug("Twitch TX #" + type + ": " + content);
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