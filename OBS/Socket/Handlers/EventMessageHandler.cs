using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;
using Serilog;
using TwitchChatTTS.OBS.Socket.Data;

namespace TwitchChatTTS.OBS.Socket.Handlers
{
    public class EventMessageHandler : IWebSocketHandler
    {
        private readonly ILogger _logger;
        public int OperationCode { get; } = 5;

        public EventMessageHandler(ILogger logger)
        {
            _logger = logger;
        }

        public async Task Execute<Data>(SocketClient<WebSocketMessage> sender, Data data)
        {
            if (data is not EventMessage message || message == null)
                return;

            switch (message.EventType)
            {
                case "StreamStateChanged":
                case "RecordStateChanged":
                    if (sender is not OBSSocketClient client)
                        return;

                    string? raw_state = message.EventData["outputState"].ToString();
                    string? state = raw_state?.Substring(21).ToLower();
                    client.Live = message.EventData["outputActive"].ToString() == "True";
                    _logger.Warning("Stream " + (state != null && state.EndsWith("ing") ? "is " : "has ") + state + ".");

                    if (client.Live == false && state != null && !state.EndsWith("ing"))
                    {
                        OnStreamEnd();
                    }
                    break;
                default:
                    _logger.Debug(message.EventType + " EVENT: " + string.Join(" | ", message.EventData?.Select(x => x.Key + "=" + x.Value?.ToString()) ?? new string[0]));
                    break;
            }
        }

        private void OnStreamEnd()
        {
        }
    }
}