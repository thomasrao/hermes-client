using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;
using Serilog;
using TwitchChatTTS.Seven.Socket.Data;

namespace TwitchChatTTS.Seven.Socket.Handlers
{
    public class ErrorHandler : IWebSocketHandler
    {
        private readonly ILogger _logger;
        public int OperationCode { get; } = 6;

        public ErrorHandler(ILogger logger)
        {
            _logger = logger;
        }

        public async Task Execute<Data>(SocketClient<WebSocketMessage> sender, Data data)
        {
            if (data is not ErrorMessage message || message == null)
                return;
        }
    }
}