using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;
using Microsoft.Extensions.Logging;
using TwitchChatTTS.OBS.Socket.Data;

namespace TwitchChatTTS.OBS.Socket.Handlers
{
    public class RequestResponseHandler : IWebSocketHandler
    {
        private ILogger Logger { get; }
        public int OperationCode { get; set; } = 7;

        public RequestResponseHandler(ILogger<RequestResponseHandler> logger) {
            Logger = logger;
        }

        public async Task Execute<Data>(SocketClient<WebSocketMessage> sender, Data message)
        {
            if (message is not RequestResponseMessage obj || obj == null)
                return;
            
            switch (obj.requestType) {
                case "GetOutputStatus":
                    if (sender is not OBSSocketClient client)
                        return;
                    
                    if (obj.requestId == "stream") {
                        client.Live = obj.responseData["outputActive"].ToString() == "True";
                        Logger.LogWarning("Updated stream's live status to " + client.Live);
                    }
                    break;
            }
        }
    }
}