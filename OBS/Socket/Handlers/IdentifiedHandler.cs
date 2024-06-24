using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;
using Serilog;
using TwitchChatTTS.OBS.Socket.Data;

namespace TwitchChatTTS.OBS.Socket.Handlers
{
    public class IdentifiedHandler : IWebSocketHandler
    {
        private readonly ILogger _logger;
        public int OperationCode { get; } = 2;

        public IdentifiedHandler(ILogger logger)
        {
            _logger = logger;
        }

        public async Task Execute<Data>(SocketClient<WebSocketMessage> sender, Data data)
        {
            if (data is not IdentifiedMessage message || message == null)
                return;

            sender.Connected = true;
            _logger.Information("Connected to OBS via rpc version " + message.NegotiatedRpcVersion + ".");

            await Task.Delay(TimeSpan.FromSeconds(5));

            /*var messages = new RequestMessage[] {
                //new RequestMessage("Sleep", string.Empty, new Dictionary<string, object>() { { "sleepMillis", 5000 } }),
                new RequestMessage("GetSceneItemId", string.Empty, new Dictionary<string, object>() { { "sceneName", "Generic" }, { "sourceName", "ABCDEF" } }),
            };
            await _manager.Send(messages);*/
        }
    }
}