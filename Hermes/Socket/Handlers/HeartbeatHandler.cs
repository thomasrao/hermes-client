using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;
using HermesSocketLibrary.Socket.Data;
using Serilog;

namespace TwitchChatTTS.Hermes.Socket.Handlers
{
    public class HeartbeatHandler : IWebSocketHandler
    {
        private readonly ILogger _logger;
        public int OperationCode { get; } = 0;

        public HeartbeatHandler(ILogger logger)
        {
            _logger = logger;
        }

        public async Task Execute<Data>(SocketClient<WebSocketMessage> sender, Data data)
        {
            if (data is not HeartbeatMessage message || message == null)
                return;

            if (sender is not HermesSocketClient client)
                return;

            _logger.Verbose("Received heartbeat from server.");

            client.LastHeartbeatReceived = DateTime.UtcNow;

            if (message.Respond)
                await sender.Send(0, new HeartbeatMessage()
                {
                    DateTime = DateTime.UtcNow,
                    Respond = false
                });
        }
    }
}