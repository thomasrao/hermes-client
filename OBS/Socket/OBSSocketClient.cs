using CommonSocketLibrary.Common;
using CommonSocketLibrary.Abstract;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System.Text.Json;

namespace TwitchChatTTS.OBS.Socket
{
    public class OBSSocketClient : WebSocketClient
    {
        private bool _live;
        public bool? Live
        {
            get => Connected ? _live : null;
            set
            {
                if (value.HasValue)
                    _live = value.Value;
            }
        }

        public OBSSocketClient(
            ILogger logger,
            [FromKeyedServices("obs")] HandlerManager<WebSocketClient, IWebSocketHandler> handlerManager,
            [FromKeyedServices("obs")] HandlerTypeManager<WebSocketClient, IWebSocketHandler> typeManager
        ) : base(logger, handlerManager, typeManager, new JsonSerializerOptions()
        {
            PropertyNameCaseInsensitive = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        })
        {
            _live = false;
        }
    }
}