using CommonSocketLibrary.Common;
using CommonSocketLibrary.Socket.Manager;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace TwitchChatTTS.Hermes.Socket.Managers
{
    public class HermesHandlerManager : WebSocketHandlerManager
    {
        public HermesHandlerManager(ILogger logger, IServiceProvider provider) : base(logger)
        {
            try
            {
                var basetype = typeof(IWebSocketHandler);
                var assembly = GetType().Assembly;
                var types = assembly.GetTypes().Where(t => t.IsClass && basetype.IsAssignableFrom(t) && t.AssemblyQualifiedName?.Contains(".Hermes.") == true);

                foreach (var type in types)
                {
                    var key = "hermes-" + type.Name.Replace("Handlers", "Hand#lers")
                            .Replace("Handler", "")
                            .Replace("Hand#lers", "Handlers")
                            .ToLower();
                    var handler = provider.GetKeyedService<IWebSocketHandler>(key);
                    if (handler == null)
                    {
                        logger.Error("Failed to find hermes websocket handler: " + type.AssemblyQualifiedName);
                        continue;
                    }

                    _logger.Debug($"Linked type {type.AssemblyQualifiedName} to hermes websocket handlers.");
                    Add(handler);
                }
            }
            catch (Exception e)
            {
                _logger.Error(e, "Failed to load hermes websocket handler types.");
            }
        }
    }
}