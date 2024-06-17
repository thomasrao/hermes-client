using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;
using Serilog;
using TwitchChatTTS.Seven.Socket.Data;

namespace TwitchChatTTS.Seven.Socket.Handlers
{
    public class SevenHelloHandler : IWebSocketHandler
    {
        private ILogger Logger { get; }
        private Configuration Configuration { get; }
        public int OperationCode { get; set; } = 1;

        public SevenHelloHandler(ILogger logger, Configuration configuration)
        {
            Logger = logger;
            Configuration = configuration;
        }

        public async Task Execute<Data>(SocketClient<WebSocketMessage> sender, Data message)
        {
            if (message is not SevenHelloMessage obj || obj == null)
                return;

            if (sender is not SevenSocketClient seven || seven == null)
                return;

            seven.Connected = true;
            seven.ConnectionDetails = obj;
            Logger.Information("Connected to 7tv websockets.");
        }
    }
}