using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;
using Serilog;
using TwitchChatTTS.Seven.Socket.Data;

namespace TwitchChatTTS.Seven.Socket.Handlers
{
    public class ReconnectHandler : IWebSocketHandler
    {
        private ILogger Logger { get; }
        public int OperationCode { get; set; } = 4;

        public ReconnectHandler(ILogger logger)
        {
            Logger = logger;
        }

        public async Task Execute<Data>(SocketClient<WebSocketMessage> sender, Data message)
        {
            if (message is not ReconnectMessage obj || obj == null)
                return;

            Logger.Information($"7tv server wants us to reconnect (reason: {obj.Reason}).");
        }
    }
}