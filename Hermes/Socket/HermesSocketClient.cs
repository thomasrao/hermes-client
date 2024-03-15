using System.Text.Json;
using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace TwitchChatTTS.Hermes.Socket
{
    public class HermesSocketClient : WebSocketClient {
        public DateTime LastHeartbeat { get; set; }
        public string? UserId { get; set; } 

        public HermesSocketClient(
            ILogger<HermesSocketClient> logger,
            [FromKeyedServices("hermes")] HandlerManager<WebSocketClient, IWebSocketHandler> handlerManager,
            [FromKeyedServices("hermes")] HandlerTypeManager<WebSocketClient, IWebSocketHandler> typeManager
        ) : base(logger, handlerManager, typeManager, new JsonSerializerOptions() {
            PropertyNameCaseInsensitive = false,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        }) {
            
        }
    }
}