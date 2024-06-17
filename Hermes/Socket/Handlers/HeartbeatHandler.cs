using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;
using HermesSocketLibrary.Socket.Data;
using Serilog;

namespace TwitchChatTTS.Hermes.Socket.Handlers
{
    public class HeartbeatHandler : IWebSocketHandler
    {
        private ILogger _logger { get; }
        public int OperationCode { get; set; } = 0;

        public HeartbeatHandler(ILogger logger)
        {
            _logger = logger;
        }

        public async Task Execute<Data>(SocketClient<WebSocketMessage> sender, Data message)
        {
            if (message is not HeartbeatMessage obj || obj == null)
                return;

            if (sender is not HermesSocketClient client)
            {
                return;
            }

            _logger.Verbose("Received heartbeat.");

            client.LastHeartbeatReceived = DateTime.UtcNow;

            if (obj.Respond)
                await sender.Send(0, new HeartbeatMessage()
                {
                    DateTime = DateTime.UtcNow,
                    Respond = false
                });
        }
    }
}