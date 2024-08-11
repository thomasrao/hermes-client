using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;
using Serilog;
using TwitchChatTTS.Seven.Socket.Data;

namespace TwitchChatTTS.Seven.Socket.Handlers
{
    public class ReconnectHandler : IWebSocketHandler
    {
        private readonly ILogger _logger;
        public int OperationCode { get; } = 4;

        public ReconnectHandler(ILogger logger)
        {
            _logger = logger;
        }

        public Task Execute<Data>(SocketClient<WebSocketMessage> sender, Data data)
        {
            if (data is not ReconnectMessage message || message == null)
                return Task.CompletedTask;

            _logger.Information($"7tv server wants this client to reconnect [reason: {message.Reason}]");
            return Task.CompletedTask;
        }
    }
}