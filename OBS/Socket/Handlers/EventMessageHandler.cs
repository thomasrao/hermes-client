using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;
using Serilog;
using TwitchChatTTS.OBS.Socket.Data;

namespace TwitchChatTTS.OBS.Socket.Handlers
{
    public class EventMessageHandler : IWebSocketHandler
    {
        private ILogger _logger { get; }
        private IServiceProvider _serviceProvider { get; }
        public int OperationCode { get; set; } = 5;

        public EventMessageHandler(ILogger logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        public async Task Execute<Data>(SocketClient<WebSocketMessage> sender, Data message)
        {
            if (message is not EventMessage obj || obj == null)
                return;

            switch (obj.EventType)
            {
                case "StreamStateChanged":
                case "RecordStateChanged":
                    if (sender is not OBSSocketClient client)
                        return;

                    string? raw_state = obj.EventData["outputState"].ToString();
                    string? state = raw_state?.Substring(21).ToLower();
                    client.Live = obj.EventData["outputActive"].ToString() == "True";
                    _logger.Warning("Stream " + (state != null && state.EndsWith("ing") ? "is " : "has ") + state + ".");

                    if (client.Live == false && state != null && !state.EndsWith("ing"))
                    {
                        OnStreamEnd();
                    }
                    break;
                default:
                    _logger.Debug(obj.EventType + " EVENT: " + string.Join(" | ", obj.EventData?.Select(x => x.Key + "=" + x.Value?.ToString()) ?? new string[0]));
                    break;
            }
        }

        private void OnStreamEnd()
        {
        }
    }
}