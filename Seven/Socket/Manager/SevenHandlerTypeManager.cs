using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;
using CommonSocketLibrary.Socket.Manager;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace TwitchChatTTS.Seven.Socket.Manager
{
    public class SevenHandlerTypeManager : WebSocketHandlerTypeManager
    {
        public SevenHandlerTypeManager(
            ILogger<SevenHandlerTypeManager> factory,
            [FromKeyedServices("7tv")] HandlerManager<WebSocketClient,
            IWebSocketHandler> handlers
        ) : base(factory, handlers)
        {
        }
    }
}