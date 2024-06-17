using System.Text.Json;
using System.Timers;
using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;
using HermesSocketLibrary.Socket.Data;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace TwitchChatTTS.Hermes.Socket
{
    public class HermesSocketClient : WebSocketClient
    {
        private Configuration _configuration;
        public DateTime LastHeartbeatReceived { get; set; }
        public DateTime LastHeartbeatSent { get; set; }
        public string? UserId { get; set; }
        private System.Timers.Timer _heartbeatTimer;
        private System.Timers.Timer _reconnectTimer;

        public HermesSocketClient(
            Configuration configuration,
            [FromKeyedServices("hermes")] HandlerManager<WebSocketClient, IWebSocketHandler> handlerManager,
            [FromKeyedServices("hermes")] HandlerTypeManager<WebSocketClient, IWebSocketHandler> typeManager,
            ILogger logger
        ) : base(logger, handlerManager, typeManager, new JsonSerializerOptions()
        {
            PropertyNameCaseInsensitive = false,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        })
        {
            _configuration = configuration;

            _heartbeatTimer = new System.Timers.Timer(TimeSpan.FromSeconds(15));
            _heartbeatTimer.Elapsed += async (sender, e) => await HandleHeartbeat(e);

            _reconnectTimer = new System.Timers.Timer(TimeSpan.FromSeconds(15));
            _reconnectTimer.Elapsed += async (sender, e) => await Reconnect(e);

            LastHeartbeatReceived = LastHeartbeatSent = DateTime.UtcNow;
        }

        protected override async Task OnConnection()
        {
            _heartbeatTimer.Enabled = true;
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
                        await Send(0, new HeartbeatMessage() { DateTime = LastHeartbeatSent });
                    }
                    catch (Exception)
                    {
                    }
                }
                else if (signalTime - LastHeartbeatReceived > TimeSpan.FromSeconds(120))
                {
                    try
                    {
                        await DisconnectAsync();
                    }
                    catch (Exception)
                    {
                    }
                    UserId = null;
                    _heartbeatTimer.Enabled = false;

                    _logger.Information("Logged off due to disconnection. Attempting to reconnect...");
                    _reconnectTimer.Enabled = true;
                }
            }
        }

        private async Task Reconnect(ElapsedEventArgs e)
        {
            try
            {
                await ConnectAsync($"wss://hermes-ws.goblincaves.com");
                Connected = true;
            }
            catch (Exception)
            {
            }
            finally
            {
                if (Connected)
                {
                    _logger.Information("Reconnected.");
                    _reconnectTimer.Enabled = false;
                    _heartbeatTimer.Enabled = true;
                    LastHeartbeatReceived = DateTime.UtcNow;

                    if (_configuration.Hermes?.Token != null)
                        await Send(1, new HermesLoginMessage() { ApiKey = _configuration.Hermes.Token });
                }
            }
        }
    }
}