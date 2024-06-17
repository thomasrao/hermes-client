using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;
using CommonSocketLibrary.Socket.Manager;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace TwitchChatTTS.Seven.Socket.Managers
{
    public class SevenHandlerTypeManager : WebSocketHandlerTypeManager
    {
        public SevenHandlerTypeManager(
            ILogger factory,
            [FromKeyedServices("7tv")] HandlerManager<WebSocketClient,
            IWebSocketHandler> handlers
        ) : base(factory, handlers)
        {
        }
    }
}