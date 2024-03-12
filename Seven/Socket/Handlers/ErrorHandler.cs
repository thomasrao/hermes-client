using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;
using Microsoft.Extensions.Logging;
using TwitchChatTTS.Seven.Socket.Data;

namespace TwitchChatTTS.Seven.Socket.Handlers
{
    public class ErrorHandler : IWebSocketHandler
    {
        private ILogger Logger { get; }
        public int OperationCode { get; set; } = 6;

        public ErrorHandler(ILogger<ErrorHandler> logger) {
            Logger = logger;
        }

        public async Task Execute<Data>(SocketClient<WebSocketMessage> sender, Data message)
        {
            if (message is not ErrorMessage obj || obj == null)
                return;
        }
    }
}