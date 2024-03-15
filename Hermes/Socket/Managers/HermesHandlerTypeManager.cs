using System.Reflection;
using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;
using CommonSocketLibrary.Socket.Manager;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace TwitchChatTTS.Hermes.Socket.Managers
{
    public class HermesHandlerTypeManager : WebSocketHandlerTypeManager
    {
        public HermesHandlerTypeManager(
            ILogger<HermesHandlerTypeManager> factory,
            [FromKeyedServices("hermes")] HandlerManager<WebSocketClient, IWebSocketHandler> handlers
        ) : base(factory, handlers)
        {
        }

        protected override Type? FetchMessageType(Type handlerType)
        {
            if (handlerType == null)
                return null;
            
            var name = handlerType.Namespace + "." + handlerType.Name;
            name = name.Replace(".Handlers.", ".Data.")
                        .Replace("Handler", "Message")
                        .Replace("TwitchChatTTS.Hermes.", "HermesSocketLibrary.");
            
            return Assembly.Load("HermesSocketLibrary").GetType(name);
        }
    }
}