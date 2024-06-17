using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;
using Serilog;
using TwitchChatTTS.OBS.Socket.Data;

namespace TwitchChatTTS.OBS.Socket.Handlers
{
    public class IdentifiedHandler : IWebSocketHandler
    {
        private ILogger Logger { get; }
        public int OperationCode { get; set; } = 2;

        public IdentifiedHandler(ILogger logger)
        {
            Logger = logger;
        }

        public async Task Execute<Data>(SocketClient<WebSocketMessage> sender, Data message)
        {
            if (message is not IdentifiedMessage obj || obj == null)
                return;

            sender.Connected = true;
            Logger.Information("Connected to OBS via rpc version " + obj.NegotiatedRpcVersion + ".");
        }
    }
}