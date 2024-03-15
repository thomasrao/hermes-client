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
        private EmoteDatabase Emotes { get; }
        public int OperationCode { get; set; } = 0;

        public DispatchHandler(ILogger<DispatchHandler> logger, EmoteDatabase emotes) {
            Logger = logger;
            Emotes = emotes;
        }

        public async Task Execute<Data>(SocketClient<WebSocketMessage> sender, Data message)
        {
            if (message is not DispatchMessage obj || obj == null)
                return;
            
            ApplyChanges(obj?.Body?.Pulled, cf => cf.OldValue, true);
            ApplyChanges(obj?.Body?.Pushed, cf => cf.Value, false);
        }

        private void ApplyChanges(IEnumerable<ChangeField>? fields, Func<ChangeField, object> getter, bool removing) {
            if (fields == null)
                return;
            
            foreach (var val in fields) {
                var value = getter(val);
                if (value == null)
                    continue;
                
                var o = JsonSerializer.Deserialize<EmoteField>(value.ToString(), new JsonSerializerOptions() {
                    PropertyNameCaseInsensitive = false,
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
                });

                if (removing) {
                    Emotes.Remove(o.Name);
                    Logger.LogInformation($"Removed 7tv emote: {o.Name} (id: {o.Id})");
                } else {
                    Emotes.Add(o.Name, o.Id);
                    Logger.LogInformation($"Added 7tv emote: {o.Name} (id: {o.Id})");
                }
            }
        }
    }
}