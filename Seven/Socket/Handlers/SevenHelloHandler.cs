using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;
using Serilog;
using TwitchChatTTS.Seven.Socket.Data;

namespace TwitchChatTTS.Seven.Socket.Handlers
{
    public class SevenHelloHandler : IWebSocketHandler
    {
        private readonly ILogger _logger;
        public int OperationCode { get; } = 1;

        public SevenHelloHandler(ILogger logger)
        {
            _logger = logger;
        }

        public async Task Execute<Data>(SocketClient<WebSocketMessage> sender, Data data)
        {
            if (data is not SevenHelloMessage message || message == null)
                return;

            if (sender is not SevenSocketClient seven || seven == null)
                return;

            seven.Connected = true;
            seven.ConnectionDetails = message;
            _logger.Information("Connected to 7tv websockets.");
        }
    }
}