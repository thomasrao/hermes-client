using Serilog;
using Microsoft.Extensions.DependencyInjection;
using CommonSocketLibrary.Socket.Manager;
using CommonSocketLibrary.Common;

namespace TwitchChatTTS.OBS.Socket.Manager
{
    public class OBSHandlerManager : WebSocketHandlerManager
    {
        public OBSHandlerManager(ILogger logger, IServiceProvider provider) : base(logger)
        {
            var basetype = typeof(IWebSocketHandler);
            var assembly = GetType().Assembly;
            var types = assembly.GetTypes().Where(t => t.IsClass && basetype.IsAssignableFrom(t) && t.AssemblyQualifiedName?.Contains(".OBS.") == true);

            foreach (var type in types)
            {
                var key = "obs-" + type.Name.Replace("Handlers", "Hand#lers")
                        .Replace("Handler", "")
                        .Replace("Hand#lers", "Handlers")
                        .ToLower();
                var handler = provider.GetKeyedService<IWebSocketHandler>(key);
                if (handler == null)
                {
                    logger.Error("Failed to find obs websocket handler: " + type.AssemblyQualifiedName);
                    continue;
                }

                Logger.Debug($"Linked type {type.AssemblyQualifiedName} to obs websocket handler {handler.GetType().AssemblyQualifiedName}.");
                Add(handler);
            }
        }
    }
}