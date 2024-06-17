using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;
using Serilog;
using TwitchChatTTS.OBS.Socket.Data;

namespace TwitchChatTTS.OBS.Socket.Handlers
{
    public class RequestResponseHandler : IWebSocketHandler
    {
        private ILogger Logger { get; }
        public int OperationCode { get; set; } = 7;

        public RequestResponseHandler(ILogger logger)
        {
            Logger = logger;
        }

        public async Task Execute<Data>(SocketClient<WebSocketMessage> sender, Data message)
        {
            if (message is not RequestResponseMessage obj || obj == null)
                return;

            switch (obj.RequestType)
            {
                case "GetOutputStatus":
                    if (sender is not OBSSocketClient client)
                        return;

                    if (obj.RequestId == "stream")
                    {
                        client.Live = obj.ResponseData["outputActive"].ToString() == "True";
                        Logger.Warning("Updated stream's live status to " + client.Live);
                    }
                    break;
            }
        }
    }
}