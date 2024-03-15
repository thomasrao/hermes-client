using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;
using HermesSocketLibrary.Socket.Data;
using Microsoft.Extensions.Logging;

namespace TwitchChatTTS.Hermes.Socket.Handlers
{
    public class LoginAckHandler : IWebSocketHandler
    {
        private ILogger _logger { get; }
        public int OperationCode { get; set; } = 2;

        public LoginAckHandler(ILogger<LoginAckHandler> logger) {
            _logger = logger;
        }

        public async Task Execute<Data>(SocketClient<WebSocketMessage> sender, Data message)
        {
            if (message is not LoginAckMessage obj || obj == null)
                return;
            
            if (sender is not HermesSocketClient client) {
                return;
            }

            if (obj.AnotherClient) {
                _logger.LogWarning("Another client has connected to the same account.");
            } else {
                client.UserId = obj.UserId;
                _logger.LogInformation($"Logged in as {client.UserId}.");
            }
        }
    }
}