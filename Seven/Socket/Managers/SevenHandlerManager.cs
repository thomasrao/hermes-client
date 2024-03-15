using Microsoft.Extensions.Logging;
using CommonSocketLibrary.Socket.Manager;
using CommonSocketLibrary.Common;
using Microsoft.Extensions.DependencyInjection;

namespace TwitchChatTTS.Seven.Socket.Managers
{
    public class SevenHandlerManager : WebSocketHandlerManager
    {
        public SevenHandlerManager(ILogger<SevenHandlerManager> logger, IServiceProvider provider) : base(logger) {
            try {
                var basetype = typeof(IWebSocketHandler);
                var assembly = GetType().Assembly;
                var types = assembly.GetTypes().Where(t => t.IsClass && basetype.IsAssignableFrom(t) && t.AssemblyQualifiedName?.Contains(".Seven.") == true);

                foreach (var type in types)  {
                    var key = "7tv-" + type.Name.Replace("Handlers", "Hand#lers")
                            .Replace("Handler", "")
                            .Replace("Hand#lers", "Handlers")
                            .ToLower();
                    var handler = provider.GetKeyedService<IWebSocketHandler>(key);
                    if (handler == null) {
                        logger.LogError("Failed to find 7tv websocket handler: " + type.AssemblyQualifiedName);
                        continue;
                    }
                    
                    Logger.LogDebug($"Linked type {type.AssemblyQualifiedName} to 7tv websocket handler {handler.GetType().AssemblyQualifiedName}.");
                    Add(handler);
                }
            } catch (Exception e) {
                Logger.LogError(e, "Failed to load 7tv websocket handler types.");
            }
        }
    }
}