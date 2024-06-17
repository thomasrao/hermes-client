using CommonSocketLibrary.Common;
using CommonSocketLibrary.Abstract;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using TwitchChatTTS.Seven.Socket.Data;
using System.Text.Json;

namespace TwitchChatTTS.Seven.Socket
{
    public class SevenSocketClient : WebSocketClient
    {
        public SevenHelloMessage? ConnectionDetails { get; set; }

        public SevenSocketClient(
            ILogger logger,
            [FromKeyedServices("7tv")] HandlerManager<WebSocketClient, IWebSocketHandler> handlerManager,
            [FromKeyedServices("7tv")] HandlerTypeManager<WebSocketClient, IWebSocketHandler> typeManager
        ) : base(logger, handlerManager, typeManager, new JsonSerializerOptions()
        {
            PropertyNameCaseInsensitive = false,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        })
        {
            ConnectionDetails = null;
        }
    }
}