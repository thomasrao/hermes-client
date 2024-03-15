using System.Collections.Concurrent;
using System.Text.Json;
using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;
using HermesSocketLibrary.Requests.Messages;
using HermesSocketLibrary.Socket.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace TwitchChatTTS.Hermes.Socket.Handlers
{
    public class RequestAckHandler : IWebSocketHandler
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly JsonSerializerOptions _options;
        private readonly ILogger _logger;
        public int OperationCode { get; set; } = 4;

        public RequestAckHandler(IServiceProvider serviceProvider, JsonSerializerOptions options, ILogger<RequestAckHandler> logger) {
            _serviceProvider = serviceProvider;
            _options = options;
            _logger = logger;
        }

        public async Task Execute<Data>(SocketClient<WebSocketMessage> sender, Data message)
        {
            if (message is not RequestAckMessage obj || obj == null)
                return;
            if (obj.Request == null)
                return;
            var context = _serviceProvider.GetRequiredService<User>();
            if (context == null)
                return;

            if (obj.Request.Type == "get_tts_voices") {
                _logger.LogDebug("Updating all available voices.");
                var voices = JsonSerializer.Deserialize<IEnumerable<VoiceDetails>>(obj.Data.ToString(), _options);
                if (voices == null)
                    return;
                
                context.VoicesAvailable = voices.ToDictionary(e => e.Id, e => e.Name);
                _logger.LogInformation("Updated all available voices.");
            } else if (obj.Request.Type == "create_tts_user") {
                _logger.LogDebug("Creating new tts voice.");
                if (!long.TryParse(obj.Request.Data["@user"], out long userId))
                    return;
                string broadcasterId = obj.Request.Data["@broadcaster"].ToString();
                // TODO: validate  broadcaster id.
                string voice = obj.Request.Data["@voice"].ToString();
                
                context.VoicesSelected.Add(userId, voice);
                _logger.LogInformation("Created new tts user.");
            } else if (obj.Request.Type == "update_tts_user") {
                _logger.LogDebug("Updating user's voice");
                if (!long.TryParse(obj.Request.Data["@user"], out long userId))
                    return;
                string broadcasterId = obj.Request.Data["@broadcaster"].ToString();
                string voice = obj.Request.Data["@voice"].ToString();
                
                context.VoicesSelected[userId] = voice;
                _logger.LogInformation($"Updated user's voice to {voice}.");
            } else if (obj.Request.Type == "create_tts_voice") {
                _logger.LogDebug("Creating new tts voice.");
                string? voice = obj.Request.Data["@voice"];
                string? voiceId = obj.Data.ToString();
                if (voice == null || voiceId == null)
                    return;

                context.VoicesAvailable.Add(voiceId, voice);
                _logger.LogInformation($"Created new tts voice named {voice} (id: {voiceId}).");
            } else if (obj.Request.Type == "delete_tts_voice") {
                _logger.LogDebug("Deleting tts voice.");

                var voice = obj.Request.Data["@voice"];
                if (!context.VoicesAvailable.TryGetValue(voice, out string voiceName) || voiceName == null) {
                    return;
                }
                
                context.VoicesAvailable.Remove(voice);
                _logger.LogInformation("Deleted a voice, named " + voiceName + ".");
            } else if (obj.Request.Type == "update_tts_voice") {
                _logger.LogDebug("Updating tts voice.");
                string voiceId = obj.Request.Data["@idd"].ToString();
                string voice = obj.Request.Data["@voice"].ToString();
                
                if (!context.VoicesAvailable.TryGetValue(voiceId, out string voiceName) || voiceName == null) {
                    return;
                }
                
                context.VoicesAvailable[voiceId] = voice;
                _logger.LogInformation("Update tts voice: " + voice);
            } else if (obj.Request.Type == "get_tts_users") {
                _logger.LogDebug("Attempting to update all chatters' selected voice.");
                var users = JsonSerializer.Deserialize<IDictionary<long, string>>(obj.Data.ToString(), _options);
                if (users == null)
                    return;
                
                var temp = new ConcurrentDictionary<long, string>();
                foreach (var entry in users)
                    temp.TryAdd(entry.Key, entry.Value);
                context.VoicesSelected = temp;
                _logger.LogInformation($"Fetched {temp.Count()} chatters' selected voice.");
            }
        }
    }
}