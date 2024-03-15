using CommonSocketLibrary.Common;
using CommonSocketLibrary.Socket.Manager;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace TwitchChatTTS.Hermes.Socket.Managers
{
    public class HermesHandlerManager : WebSocketHandlerManager
    {
        public HermesHandlerManager(ILogger<HermesHandlerManager> logger, IServiceProvider provider) : base(logger) {
            //Add(provider.GetRequiredService<HeartbeatHandler>());
            try {
                var basetype = typeof(IWebSocketHandler);
                var assembly = GetType().Assembly;
                var types = assembly.GetTypes().Where(t => t.IsClass && basetype.IsAssignableFrom(t) && t.AssemblyQualifiedName?.Contains(".Hermes.") == true);

                foreach (var type in types)  {
                    var key = "hermes-" + type.Name.Replace("Handlers", "Hand#lers")
                            .Replace("Handler", "")
                            .Replace("Hand#lers", "Handlers")
                            .ToLower();
                    var handler = provider.GetKeyedService<IWebSocketHandler>(key);
                    if (handler == null) {
                        logger.LogError("Failed to find hermes websocket handler: " + type.AssemblyQualifiedName);
                        continue;
                    }
                    
                    Logger.LogDebug($"Linked type {type.AssemblyQualifiedName} to hermes websocket handlers.");
                    Add(handler);
                }
            } catch (Exception e) {
                Logger.LogError(e, "Failed to load hermes websocket handler types.");
            }
        }
    }
}