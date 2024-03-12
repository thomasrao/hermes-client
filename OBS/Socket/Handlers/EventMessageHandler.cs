using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;
using Microsoft.Extensions.Logging;
using TwitchChatTTS.OBS.Socket.Data;

namespace TwitchChatTTS.OBS.Socket.Handlers
{
    public class EventMessageHandler : IWebSocketHandler
    {
        private ILogger Logger { get; }
        private IServiceProvider ServiceProvider { get; }
        public int OperationCode { get; set; } = 5;

        public EventMessageHandler(ILogger<EventMessageHandler> logger, IServiceProvider serviceProvider) {
            Logger = logger;
            ServiceProvider = serviceProvider;
        }

        public async Task Execute<Data>(SocketClient<WebSocketMessage> sender, Data message)
        {
            if (message is not EventMessage obj || obj == null)
                return;

            switch (obj.eventType) {
                case "StreamStateChanged":
                case "RecordStateChanged":
                    if (sender is not OBSSocketClient client)
                        return;
                    
                    string? raw_state = obj.eventData["outputState"].ToString();
                    string? state = raw_state?.Substring(21).ToLower();
                    client.Live = obj.eventData["outputActive"].ToString() == "True";
                    Logger.LogWarning("Stream " + (state != null && state.EndsWith("ing") ? "is " : "has ") + state + ".");

                    if (client.Live == false && state != null && !state.EndsWith("ing")) {
                        OnStreamEnd();
                    }
                    break;
                default:
                    Logger.LogDebug(obj.eventType + " EVENT: " + string.Join(" | ", obj.eventData?.Select(x => x.Key + "=" + x.Value?.ToString()) ?? new string[0]));
                    break;
            }
        }

        private void OnStreamEnd() {
        }
    }
}