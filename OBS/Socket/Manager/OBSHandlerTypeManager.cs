using CommonSocketLibrary.Common;
using CommonSocketLibrary.Socket.Manager;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace TwitchChatTTS.OBS.Socket.Manager
{
    public class OBSMessageTypeManager : WebSocketMessageTypeManager
    {
        public OBSMessageTypeManager(
            [FromKeyedServices("obs")] IEnumerable<IWebSocketHandler> handlers,
            ILogger logger
        ) : base(handlers, logger)
        {
        }
    }
}