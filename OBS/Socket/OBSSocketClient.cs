using TwitchChatTTS.OBS.Socket.Manager;
using CommonSocketLibrary.Common;
using CommonSocketLibrary.Abstract;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace TwitchChatTTS.OBS.Socket
{
    public class OBSSocketClient : WebSocketClient {
        private bool _live;
        public bool? Live {
            get => Connected ? _live : null;
            set {
                if (value.HasValue)
                    _live = value.Value;
            }
        }

        public OBSSocketClient(
            ILogger<OBSSocketClient> logger,
            [FromKeyedServices("obs")] HandlerManager<WebSocketClient, IWebSocketHandler> handlerManager,
            [FromKeyedServices("obs")] HandlerTypeManager<WebSocketClient, IWebSocketHandler> typeManager
        ) : base(logger, handlerManager, typeManager, new JsonSerializerOptions() {
            PropertyNameCaseInsensitive = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }) {
            _live = false;
        }
    }
}