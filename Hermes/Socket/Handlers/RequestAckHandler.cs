using System.Collections.Concurrent;
using System.Text.Json;
using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;
using HermesSocketLibrary.Requests.Messages;
using HermesSocketLibrary.Socket.Data;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using TwitchChatTTS.Seven;

namespace TwitchChatTTS.Hermes.Socket.Handlers
{
    public class RequestAckHandler : IWebSocketHandler
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly JsonSerializerOptions _options;
        private readonly ILogger _logger;

        private readonly object _voicesAvailableLock = new object();

        public int OperationCode { get; } = 4;

        public RequestAckHandler(IServiceProvider serviceProvider, JsonSerializerOptions options, ILogger logger)
        {
            _serviceProvider = serviceProvider;
            _options = options;
            _logger = logger;
        }

        public async Task Execute<Data>(SocketClient<WebSocketMessage> sender, Data data)
        {
            if (data is not RequestAckMessage message || message == null)
                return;
            if (message.Request == null)
                return;
            var context = _serviceProvider.GetRequiredService<User>();
            if (context == null)
                return;

            if (message.Request.Type == "get_tts_voices")
            {
                _logger.Verbose("Updating all available voices for TTS.");
                var voices = JsonSerializer.Deserialize<IEnumerable<VoiceDetails>>(message.Data.ToString(), _options);
                if (voices == null)
                    return;

                lock (_voicesAvailableLock)
                {
                    context.VoicesAvailable = voices.ToDictionary(e => e.Id, e => e.Name);
                }
                _logger.Information("Updated all available voices for TTS.");
            }
            else if (message.Request.Type == "create_tts_user")
            {
                _logger.Verbose("Adding new tts voice for user.");
                if (!long.TryParse(message.Request.Data["user"].ToString(), out long chatterId))
                    return;
                string userId = message.Request.Data["user"].ToString();
                string voice = message.Request.Data["voice"].ToString();

                context.VoicesSelected.Add(chatterId, voice);
                _logger.Information($"Added new TTS voice [voice: {voice}] for user [user id: {userId}]");
            }
            else if (message.Request.Type == "update_tts_user")
            {
                _logger.Verbose("Updating user's voice");
                if (!long.TryParse(message.Request.Data["chatter"].ToString(), out long chatterId))
                    return;
                string userId = message.Request.Data["user"].ToString();
                string voice = message.Request.Data["voice"].ToString();

                context.VoicesSelected[chatterId] = voice;
                _logger.Information($"Updated TTS voice [voice: {voice}] for user [user id: {userId}]");
            }
            else if (message.Request.Type == "create_tts_voice")
            {
                _logger.Verbose("Creating new tts voice.");
                string? voice = message.Request.Data["voice"].ToString();
                string? voiceId = message.Data.ToString();
                if (voice == null || voiceId == null)
                    return;

                lock (_voicesAvailableLock)
                {
                    var list = context.VoicesAvailable.ToDictionary(k => k.Key, v => v.Value);
                    list.Add(voiceId, voice);
                    context.VoicesAvailable = list;
                }
                _logger.Information($"Created new tts voice [voice: {voice}][id: {voiceId}].");
            }
            else if (message.Request.Type == "delete_tts_voice")
            {
                _logger.Verbose("Deleting tts voice.");
                var voice = message.Request.Data["voice"].ToString();
                if (!context.VoicesAvailable.TryGetValue(voice, out string voiceName) || voiceName == null)
                    return;

                lock (_voicesAvailableLock)
                {
                    var dict = context.VoicesAvailable.ToDictionary(k => k.Key, v => v.Value);
                    dict.Remove(voice);
                    context.VoicesAvailable.Remove(voice);
                }
                _logger.Information($"Deleted a voice [voice: {voiceName}]");
            }
            else if (message.Request.Type == "update_tts_voice")
            {
                _logger.Verbose("Updating TTS voice.");
                string voiceId = message.Request.Data["idd"].ToString();
                string voice = message.Request.Data["voice"].ToString();

                if (!context.VoicesAvailable.TryGetValue(voiceId, out string voiceName) || voiceName == null)
                    return;

                context.VoicesAvailable[voiceId] = voice;
                _logger.Information($"Updated TTS voice [voice: {voice}][id: {voiceId}]");
            }
            else if (message.Request.Type == "get_tts_users")
            {
                _logger.Verbose("Updating all chatters' selected voice.");
                var users = JsonSerializer.Deserialize<IDictionary<long, string>>(message.Data.ToString(), _options);
                if (users == null)
                    return;

                var temp = new ConcurrentDictionary<long, string>();
                foreach (var entry in users)
                    temp.TryAdd(entry.Key, entry.Value);
                context.VoicesSelected = temp;
                _logger.Information($"Updated {temp.Count()} chatters' selected voice.");
            }
            else if (message.Request.Type == "get_chatter_ids")
            {
                _logger.Verbose("Fetching all chatters' id.");
                var chatters = JsonSerializer.Deserialize<IEnumerable<long>>(message.Data.ToString(), _options);
                if (chatters == null)
                    return;

                var client = _serviceProvider.GetRequiredService<ChatMessageHandler>();
                client.Chatters = [.. chatters];
                _logger.Information($"Fetched {chatters.Count()} chatters' id.");
            }
            else if (message.Request.Type == "get_emotes")
            {
                _logger.Verbose("Updating emotes.");
                var emotes = JsonSerializer.Deserialize<IEnumerable<EmoteInfo>>(message.Data.ToString(), _options);
                if (emotes == null)
                    return;

                var emoteDb = _serviceProvider.GetRequiredService<EmoteDatabase>();
                var count = 0;
                foreach (var emote in emotes)
                {
                    if (emoteDb.Get(emote.Name) == null)
                    {
                        emoteDb.Add(emote.Name, emote.Id);
                        count++;
                    }
                }
                _logger.Information($"Fetched {count} emotes from various sources.");
            }
            else if (message.Request.Type == "update_tts_voice_state")
            {
                _logger.Verbose("Updating TTS voice states.");
                string voiceId = message.Request.Data["voice"].ToString();
                bool state = message.Request.Data["state"].ToString() == "true";

                if (!context.VoicesAvailable.TryGetValue(voiceId, out string voiceName) || voiceName == null)
                {
                    _logger.Warning($"Failed to find voice [id: {voiceId}]");
                    return;
                }

                if (state)
                    context.VoicesEnabled.Add(voiceId);
                else
                    context.VoicesEnabled.Remove(voiceId);
                _logger.Information($"Updated voice state [voice: {voiceName}][new state: {(state ? "enabled" : "disabled")}]");
            }
        }
    }
}