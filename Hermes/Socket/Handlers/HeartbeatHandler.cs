using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;
using HermesSocketLibrary.Socket.Data;
using Microsoft.Extensions.Logging;

namespace TwitchChatTTS.Hermes.Socket.Handlers
{
    public class HeartbeatHandler : IWebSocketHandler
    {
        private ILogger _logger { get; }
        public int OperationCode { get; set; } = 0;

        public HeartbeatHandler(ILogger<HeartbeatHandler> logger) {
            _logger = logger;
        }

        public async Task Execute<Data>(SocketClient<WebSocketMessage> sender, Data message)
        {
            if (message is not HeartbeatMessage obj || obj == null)
                return;
            
            if (sender is not HermesSocketClient client) {
                return;
            }

            _logger.LogTrace("Received heartbeat.");

            client.LastHeartbeat = DateTime.UtcNow;

            await sender.Send(0, new HeartbeatMessage() {
                DateTime = DateTime.UtcNow
            });
        }
    }
}