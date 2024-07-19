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

        public EventMessageHandler(
            ILogger logger
        )
        {
            _logger = logger;
        }

        public async Task Execute<Data>(SocketClient<WebSocketMessage> sender, Data data)
        {
            if (data is not EventMessage message || message == null)
                return;
            if (sender is not OBSSocketClient obs)
                return;

            switch (message.EventType)
            {
                case "StreamStateChanged":
                    if (sender is not OBSSocketClient client)
                        return;

                    string? raw_state = message.EventData["outputState"].ToString();
                    string? state = raw_state?.Substring(21).ToLower();
                    obs.Streaming = message.EventData["outputActive"].ToString()!.ToLower() == "true";
                    _logger.Warning("Stream " + (state != null && state.EndsWith("ing") ? "is " : "has ") + state + ".");

                    if (obs.Streaming == false && state != null && !state.EndsWith("ing"))
                    {
                        // Stream ended
                    }
                    break;
                default:
                    _logger.Debug(message.EventType + " EVENT: " + string.Join(" | ", message.EventData?.Select(x => x.Key + "=" + x.Value?.ToString()) ?? Array.Empty<string>()));
                    break;
            }
        }
    }
}