using CommonSocketLibrary.Common;
using CommonSocketLibrary.Socket.Manager;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace TwitchChatTTS.Seven.Socket.Managers
{
    public class SevenMessageTypeManager : WebSocketMessageTypeManager
    {
        public SevenMessageTypeManager(
            [FromKeyedServices("7tv")] IEnumerable<IWebSocketHandler> handlers,
            ILogger logger
        ) : base(handlers, logger)
        {
        }
    }
}