using System.Net.WebSockets;
using System.Text.Json;
using System.Timers;
using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;
using HermesSocketLibrary.Requests.Callbacks;
using HermesSocketLibrary.Requests.Messages;
using HermesSocketLibrary.Socket.Data;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using TwitchChatTTS.Hermes.Socket.Handlers;

namespace TwitchChatTTS.Hermes.Socket
{
    public class HermesSocketClient : WebSocketClient
    {
        public const string BASE_URL = "ws.tomtospeech.com";

        private readonly User _user;
        private readonly Configuration _configuration;
        private readonly ICallbackManager<HermesRequestData> _callbackManager;
        private string URL;

        public DateTime LastHeartbeatReceived { get; set; }
        public DateTime LastHeartbeatSent { get; set; }
        public string? UserId { get; set; }
        private System.Timers.Timer _heartbeatTimer;
        private System.Timers.Timer _reconnectTimer;

        public bool Connected { get; set; }
        public bool LoggedIn { get; set; }
        public bool Ready { get; set; }


        public HermesSocketClient(
            User user,
            Configuration configuration,
            ICallbackManager<HermesRequestData> callbackManager,
            [FromKeyedServices("hermes")] IEnumerable<IWebSocketHandler> handlers,
            [FromKeyedServices("hermes")] MessageTypeManager<IWebSocketHandler> typeManager,
            ILogger logger
        ) : base(handlers, typeManager, new JsonSerializerOptions()
        {
            PropertyNameCaseInsensitive = false,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        }, logger)
        {
            _user = user;
            _configuration = configuration;
            _callbackManager = callbackManager;

            _heartbeatTimer = new System.Timers.Timer(TimeSpan.FromSeconds(15));
            _heartbeatTimer.Elapsed += async (sender, e) => await HandleHeartbeat(e);

            _reconnectTimer = new System.Timers.Timer(TimeSpan.FromSeconds(15));
            _reconnectTimer.Elapsed += async (sender, e) => await Reconnect(e);

            LastHeartbeatReceived = LastHeartbeatSent = DateTime.UtcNow;
            URL = $"wss://{BASE_URL}";
        }


        public async Task Connect()
        {
            if (Connected)
                return;

            _logger.Debug($"Attempting to connect to {URL}");
            await ConnectAsync(URL);
        }

        private async Task Disconnect()
        {
            if (!Connected)
                return;

            await DisconnectAsync(new SocketDisconnectionEventArgs("Normal disconnection", "Disconnection was executed"));
        }

        public async Task CreateTTSVoice(string voiceName)
        {
            await Send(3, new RequestMessage()
            {
                Type = "create_tts_voice",
                Data = new Dictionary<string, object>() { { "voice", voiceName } }
            });
        }

        public async Task CreateTTSUser(long chatterId, string voiceId)
        {
            await Send(3, new RequestMessage()
            {
                Type = "create_tts_user",
                Data = new Dictionary<string, object>() { { "chatter", chatterId }, { "voice", voiceId } }
            });
        }

        public async Task DeleteTTSVoice(string voiceId)
        {
            await Send(3, new RequestMessage()
            {
                Type = "delete_tts_voice",
                Data = new Dictionary<string, object>() { { "voice", voiceId } }
            });
        }

        public async Task FetchChatterIdentifiers()
        {
            await Send(3, new RequestMessage()
            {
                Type = "get_chatter_ids",
                Data = null
            });
        }

        public async Task FetchDefaultTTSVoice()
        {
            await Send(3, new RequestMessage()
            {
                Type = "get_default_tts_voice",
                Data = null
            });
        }

        public async Task FetchEmotes()
        {
            await Send(3, new RequestMessage()
            {
                Type = "get_emotes",
                Data = null
            });
        }

        public async Task FetchEnabledTTSVoices()
        {
            await Send(3, new RequestMessage()
            {
                Type = "get_enabled_tts_voices",
                Data = null
            });
        }

        public async Task FetchTTSVoices()
        {
            await Send(3, new RequestMessage()
            {
                Type = "get_tts_voices",
                Data = null
            });
        }

        public async Task FetchTTSChatterVoices()
        {
            await Send(3, new RequestMessage()
            {
                Type = "get_tts_users",
                Data = null
            });
        }

        public async Task FetchTTSWordFilters()
        {
            await Send(3, new RequestMessage()
            {
                Type = "get_tts_word_filters",
                Data = null
            });
        }

        public async Task FetchRedemptions()
        {
            var requestId = _callbackManager.GenerateKeyForCallback(new HermesRequestData()
            {
                Callback = async (d) => await FetchRedeemableActions(d["redemptions"] as IEnumerable<Redemption>),
                Data = new Dictionary<string, object>()
            });

            await Send(3, new RequestMessage()
            {
                RequestId = requestId,
                Type = "get_redemptions",
                Data = null
            });
        }

        private async Task FetchRedeemableActions(IEnumerable<Redemption> redemptions)
        {
            var requestId = _callbackManager.GenerateKeyForCallback(new HermesRequestData()
            {
                Data = new Dictionary<string, object>() { { "redemptions", redemptions } }
            });

            await Send(3, new RequestMessage()
            {
                RequestId = requestId,
                Type = "get_redeemable_actions",
                Data = null
            });
        }

        public async Task FetchPermissions()
        {
            await Send(3, new RequestMessage()
            {
                Type = "get_permissions",
                Data = null
            });
        }

        public void Initialize()
        {
            _logger.Information("Initializing Hermes websocket client.");

            OnConnected += async (sender, e) =>
            {
                Connected = true;
                _logger.Information("Hermes websocket client connected.");

                _reconnectTimer.Enabled = false;
                _heartbeatTimer.Enabled = true;
                LastHeartbeatReceived = DateTime.UtcNow;

                await Send(1, new HermesLoginMessage()
                {
                    ApiKey = _configuration.Hermes!.Token!,
                    MajorVersion = TTS.MAJOR_VERSION,
                    MinorVersion = TTS.MINOR_VERSION,
                });
            };

            OnDisconnected += (sender, e) =>
            {
                Connected = false;
                LoggedIn = false;
                Ready = false;
                _logger.Warning("Hermes websocket client disconnected.");

                _heartbeatTimer.Enabled = false;
                _reconnectTimer.Enabled = true;
            };
        }

        public async Task SendLoggingMessage(HermesLoggingLevel level, string message)
        {
            await Send(5, new LoggingMessage(message, level));
        }

        public async Task SendLoggingMessage(Exception exception, HermesLoggingLevel level, string message)
        {
            await Send(5, new LoggingMessage(exception, message, level));
        }

        public async Task SendEmoteUsage(string messageId, long chatterId, ICollection<string> emotes)
        {
            if (!LoggedIn)
            {
                _logger.Debug("Not logged in. Cannot sent EmoteUsage message.");
                return;
            }

            await Send(8, new EmoteUsageMessage()
            {
                MessageId = messageId,
                DateTime = DateTime.UtcNow,
                BroadcasterId = _user.TwitchUserId,
                ChatterId = chatterId,
                Emotes = emotes
            });
        }

        public async Task SendChatterDetails(long chatterId, string username)
        {
            if (!LoggedIn)
            {
                _logger.Debug("Not logged in. Cannot send Chatter message.");
                return;
            }

            await Send(6, new ChatterMessage()
            {
                Id = chatterId,
                Name = username
            });
        }

        public async Task SendEmoteDetails(IDictionary<string, string> emotes)
        {
            if (!LoggedIn)
            {
                _logger.Debug("Not logged in. Cannot send EmoteDetails message.");
                return;
            }

            await Send(7, new EmoteDetailsMessage()
            {
                Emotes = emotes
            });
        }

        public async Task SendHeartbeat(bool respond = false, DateTime? date = null)
        {
            await Send(0, new HeartbeatMessage() { DateTime = date ?? DateTime.UtcNow, Respond = respond });
        }

        public async Task UpdateTTSUser(long chatterId, string voiceId)
        {
            if (!LoggedIn)
            {
                _logger.Debug("Not logged in. Cannot send UpdateTTSUser message.");
                return;
            }

            await Send(3, new RequestMessage()
            {
                Type = "update_tts_user",
                Data = new Dictionary<string, object>() { { "chatter", chatterId }, { "voice", voiceId } }
            });
        }

        public async Task UpdateTTSVoiceState(string voiceId, bool state)
        {
            if (!LoggedIn)
            {
                _logger.Debug("Not logged in. Cannot send UpdateTTSVoiceState message.");
                return;
            }

            await Send(3, new RequestMessage()
            {
                Type = "update_tts_voice_state",
                Data = new Dictionary<string, object>() { { "voice", voiceId }, { "state", state } }
            });
        }

        private async Task HandleHeartbeat(ElapsedEventArgs e)
        {
            var signalTime = e.SignalTime.ToUniversalTime();

            if (signalTime - LastHeartbeatReceived > TimeSpan.FromSeconds(60))
            {
                if (LastHeartbeatReceived > LastHeartbeatSent)
                {
                    _logger.Verbose("Sending heartbeat...");
                    LastHeartbeatSent = DateTime.UtcNow;
                    try
                    {
                        await SendHeartbeat(date: LastHeartbeatSent);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Failed to send a heartbeat back to the Hermes websocket server.");
                    }
                }
                else if (signalTime - LastHeartbeatReceived > TimeSpan.FromSeconds(120))
                {
                    try
                    {
                        await Disconnect();
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Failed to disconnect from Hermes websocket server.");
                    }
                    UserId = null;
                    _heartbeatTimer.Enabled = false;

                    _logger.Warning("Logged off due to disconnection. Attempting to reconnect...");
                    _reconnectTimer.Enabled = true;
                }
            }
        }

        private async Task Reconnect(ElapsedEventArgs e)
        {
            if (Connected)
            {
                try
                {
                    await Disconnect();
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to disconnect from Hermes websocket server.");
                }
            }

            try
            {
                await Connect();
            }
            catch (WebSocketException wse) when (wse.Message.Contains("502"))
            {
                _logger.Error($"Hermes websocket server cannot be found [code: {wse.ErrorCode}]");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to reconnect to Hermes websocket server.");
            }
        }

        public new async Task Send<T>(int opcode, T message)
        {
            if (!Connected)
            {
                _logger.Warning("Hermes websocket client is not connected. Not sending a message.");
                return;
            }

            await base.Send(opcode, message);
        }
    }
}