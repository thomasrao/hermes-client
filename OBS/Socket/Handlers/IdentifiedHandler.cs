using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;
using Serilog;
using TwitchChatTTS.OBS.Socket.Data;
using TwitchChatTTS.OBS.Socket.Manager;

namespace TwitchChatTTS.OBS.Socket.Handlers
{
    public class IdentifiedHandler : IWebSocketHandler
    {
        private readonly OBSManager _manager;
        private readonly ILogger _logger;
        public int OperationCode { get; } = 2;

        public IdentifiedHandler(OBSManager manager, ILogger logger)
        {
            _manager = manager;
            _logger = logger;
        }

        public async Task Execute<Data>(SocketClient<WebSocketMessage> sender, Data data)
        {
            if (data is not IdentifiedMessage message || message == null)
                return;

            sender.Connected = true;
            _logger.Information("Connected to OBS via rpc version " + message.NegotiatedRpcVersion + ".");

            try
            {
                await _manager.GetGroupList(async groups => await _manager.GetGroupSceneItemList(groups));
            }
            catch (Exception e)
            {
                _logger.Error(e, "Failed to load OBS group info upon OBS identification.");
            }
        }
    }
}