using System.Text.Json;
using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;
using Serilog;
using TwitchChatTTS.Seven.Socket.Data;

namespace TwitchChatTTS.Seven.Socket.Handlers
{
    public class DispatchHandler : IWebSocketHandler
    {
        private readonly ILogger _logger;
        private readonly EmoteDatabase _emotes;
        private readonly object _lock = new object();
        public int OperationCode { get; } = 0;

        public DispatchHandler(ILogger logger, EmoteDatabase emotes)
        {
            _logger = logger;
            _emotes = emotes;
        }

        public async Task Execute<Data>(SocketClient<WebSocketMessage> sender, Data data)
        {
            if (data is not DispatchMessage message || message == null)
                return;

            ApplyChanges(message?.Body?.Pulled, cf => cf.OldValue, true);
            ApplyChanges(message?.Body?.Pushed, cf => cf.Value, false);
            ApplyChanges(message?.Body?.Removed, cf => cf.OldValue, true);
            ApplyChanges(message?.Body?.Updated, cf => cf.OldValue, false, cf => cf.Value);
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
                        _logger.Information($"Removed 7tv emote [name: {o.Name}][id: {o.Id}]");
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
                            _emotes.Add(u.Name, u.Id);
                            _logger.Information($"Updated 7tv emote [old name: {o.Name}][new name: {u.Name}][id: {u.Id}]");
                        }
                        else
                        {
                            _logger.Warning("Failed to update 7tv emote.");
                        }
                    }
                    else
                    {
                        _emotes.Add(o.Name, o.Id);
                        _logger.Information($"Added 7tv emote [name: {o.Name}][id: {o.Id}]");
                    }
                }
            }
        }

        private void RemoveEmoteById(string id)
        {
            string? key = null;
            foreach (var e in _emotes.Emotes)
            {
                if (e.Value == id)
                {
                    key = e.Key;
                    break;
                }
            }
            if (key != null)
                _emotes.Remove(key);
        }
    }
}