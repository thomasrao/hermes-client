using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;
using CommonSocketLibrary.Socket.Manager;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace TwitchChatTTS.OBS.Socket.Manager
{
    public class OBSHandlerTypeManager : WebSocketHandlerTypeManager
    {
        public OBSHandlerTypeManager(
            ILogger factory,
            [FromKeyedServices("obs")] HandlerManager<WebSocketClient, IWebSocketHandler> handlers
        ) : base(factory, handlers)
        {
        }
    }
}