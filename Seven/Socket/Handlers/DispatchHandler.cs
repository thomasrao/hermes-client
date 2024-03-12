using System.Text.Json;
using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TwitchChatTTS.Seven.Socket.Data;

namespace TwitchChatTTS.Seven.Socket.Handlers
{
    public class DispatchHandler : IWebSocketHandler
    {
        private ILogger Logger { get; }
        private IServiceProvider ServiceProvider { get; }
        public int OperationCode { get; set; } = 0;

        public DispatchHandler(ILogger<DispatchHandler> logger, IServiceProvider serviceProvider) {
            Logger = logger;
            ServiceProvider = serviceProvider;
        }

        public async Task Execute<Data>(SocketClient<WebSocketMessage> sender, Data message)
        {
            if (message is not DispatchMessage obj || obj == null)
                return;
            
            Do(obj?.Body?.Pulled, cf => cf.OldValue);
            Do(obj?.Body?.Pushed, cf => cf.Value);
        }

        private void Do(IEnumerable<ChangeField>? fields, Func<ChangeField, object> getter) {
            if (fields is null)
                return;
            
            //ServiceProvider.GetRequiredService<EmoteDatabase>()
            foreach (var val in fields) {
                if (getter(val) == null)
                    continue;
                
                var o = JsonSerializer.Deserialize<EmoteField>(val.OldValue.ToString(), new JsonSerializerOptions() {
                    PropertyNameCaseInsensitive = false,
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
                });
            }
        }
    }
}