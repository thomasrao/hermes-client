using System.Text.Json;
using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using TwitchChatTTS.Seven.Socket.Data;

namespace TwitchChatTTS.Seven.Socket.Handlers
{
    public class DispatchHandler : IWebSocketHandler
    {
        private ILogger Logger { get; }
        private EmoteDatabase Emotes { get; }
        private object _lock = new object();
        public int OperationCode { get; set; } = 0;

        public DispatchHandler(ILogger logger, EmoteDatabase emotes)
        {
            Logger = logger;
            Emotes = emotes;
        }

        public async Task Execute<Data>(SocketClient<WebSocketMessage> sender, Data message)
        {
            if (message is not DispatchMessage obj || obj == null)
                return;

            ApplyChanges(obj?.Body?.Pulled, cf => cf.OldValue, true);
            ApplyChanges(obj?.Body?.Pushed, cf => cf.Value, false);
            ApplyChanges(obj?.Body?.Removed, cf => cf.OldValue, true);
            ApplyChanges(obj?.Body?.Updated, cf => cf.OldValue, false, cf => cf.Value);
        }

        private void ApplyChanges(IEnumerable<ChangeField>? fields, Func<ChangeField, object> getter, bool removing, Func<ChangeField, object>? updater = null)
        {
            if (fields == null || !fields.Any() || removing && updater != null)
                return;

            foreach (var val in fields)
            {
                var value = getter(val);
                if (value == null)
                    continue;

                var o = JsonSerializer.Deserialize<EmoteField>(value.ToString(), new JsonSerializerOptions()
                {
                    PropertyNameCaseInsensitive = false,
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
                });
                if (o == null)
                    continue;

                lock (_lock)
                {
                    if (removing)
                    {
                        RemoveEmoteById(o.Id);
                        Logger.Information($"Removed 7tv emote: {o.Name} (id: {o.Id})");
                    }
                    else if (updater != null)
                    {
                        RemoveEmoteById(o.Id);
                        var update = updater(val);

                        var u = JsonSerializer.Deserialize<EmoteField>(update.ToString(), new JsonSerializerOptions()
                        {
                            PropertyNameCaseInsensitive = false,
                            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
                        });

                        if (u != null)
                        {
                            Emotes.Add(u.Name, u.Id);
                            Logger.Information($"Updated 7tv emote: from '{o.Name}' to '{u.Name}' (id: {u.Id})");
                        }
                        else
                        {
                            Logger.Warning("Failed to update 7tv emote.");
                        }
                    }
                    else
                    {
                        Emotes.Add(o.Name, o.Id);
                        Logger.Information($"Added 7tv emote: {o.Name} (id: {o.Id})");
                    }
                }
            }
        }

        private void RemoveEmoteById(string id)
        {
            string? key = null;
            foreach (var e in Emotes.Emotes)
            {
                if (e.Value == id)
                {
                    key = e.Key;
                    break;
                }
            }
            if (key != null)
                Emotes.Remove(key);
        }
    }
}