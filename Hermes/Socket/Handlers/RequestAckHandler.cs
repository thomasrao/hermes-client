using System.Collections.Concurrent;
using System.Text.Json;
using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;
using HermesSocketLibrary.Requests.Callbacks;
using HermesSocketLibrary.Requests.Messages;
using HermesSocketLibrary.Socket.Data;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using TwitchChatTTS.Chat.Emotes;
using TwitchChatTTS.Twitch.Redemptions;

namespace TwitchChatTTS.Hermes.Socket.Handlers
{
    public class RequestAckHandler : IWebSocketHandler
    {
        private User _user;
        //private readonly RedemptionManager _redemptionManager;
        private readonly ICallbackManager<HermesRequestData> _callbackManager;
        private readonly IServiceProvider _serviceProvider;
        private readonly JsonSerializerOptions _options;
        private readonly ILogger _logger;

        private readonly object _voicesAvailableLock = new object();

        public int OperationCode { get; } = 4;


        public RequestAckHandler(
            User user,
            //RedemptionManager redemptionManager,
            ICallbackManager<HermesRequestData> callbackManager,
            IServiceProvider serviceProvider,
            JsonSerializerOptions options,
            ILogger logger
        )
        {
            _user = user;
            //_redemptionManager = redemptionManager;
            _callbackManager = callbackManager;
            _serviceProvider = serviceProvider;
            _options = options;
            _logger = logger;
        }

        public async Task Execute<Data>(SocketClient<WebSocketMessage> sender, Data data)
        {
            if (data is not RequestAckMessage message || message == null)
                return;
            if (message.Request == null)
            {
                _logger.Warning("Received a Hermes request message without a proper request.");
                return;
            }

            HermesRequestData? hermesRequestData = null;
            if (!string.IsNullOrEmpty(message.Request.RequestId))
            {
                hermesRequestData = _callbackManager.Take(message.Request.RequestId);
                if (hermesRequestData == null)
                    _logger.Warning($"Could not find callback for request [request id: {message.Request.RequestId}][type: {message.Request.Type}]");
                else if (hermesRequestData.Data == null)
                    hermesRequestData.Data = new Dictionary<string, object>();
            }

            _logger.Debug($"Received a Hermes request message [type: {message.Request.Type}][data: {string.Join(',', message.Request.Data?.Select(entry => entry.Key + '=' + entry.Value) ?? Array.Empty<string>())}]");
            if (message.Request.Type == "get_tts_voices")
            {
                _logger.Verbose("Updating all available voices for TTS.");
                var voices = JsonSerializer.Deserialize<IEnumerable<VoiceDetails>>(message.Data.ToString(), _options);
                if (voices == null)
                    return;

                lock (_voicesAvailableLock)
                {
                    _user.VoicesAvailable = voices.ToDictionary(e => e.Id, e => e.Name);
                }
                _logger.Information("Updated all available voices for TTS.");
            }
            else if (message.Request.Type == "create_tts_user")
            {
                _logger.Verbose("Adding new tts voice for user.");
                if (!long.TryParse(message.Request.Data["chatter"].ToString(), out long chatterId))
                {
                    _logger.Warning($"Failed to parse chatter id [chatter id: {message.Request.Data["chatter"]}]");
                    return;
                }
                string userId = message.Request.Data["user"].ToString();
                string voiceId = message.Request.Data["voice"].ToString();

                _user.VoicesSelected.Add(chatterId, voiceId);
                _logger.Information($"Added new TTS voice [voice: {voiceId}] for user [user id: {userId}]");
            }
            else if (message.Request.Type == "update_tts_user")
            {
                _logger.Verbose("Updating user's voice");
                if (!long.TryParse(message.Request.Data["chatter"].ToString(), out long chatterId))
                {
                    _logger.Warning($"Failed to parse chatter id [chatter id: {message.Request.Data["chatter"]}]");
                    return;
                }
                string userId = message.Request.Data["user"].ToString();
                string voiceId = message.Request.Data["voice"].ToString();

                _user.VoicesSelected[chatterId] = voiceId;
                _logger.Information($"Updated TTS voice [voice: {voiceId}] for user [user id: {userId}]");
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
                    var list = _user.VoicesAvailable.ToDictionary(k => k.Key, v => v.Value);
                    list.Add(voiceId, voice);
                    _user.VoicesAvailable = list;
                }
                _logger.Information($"Created new tts voice [voice: {voice}][id: {voiceId}].");
            }
            else if (message.Request.Type == "delete_tts_voice")
            {
                _logger.Verbose("Deleting tts voice.");
                var voice = message.Request.Data["voice"].ToString();
                if (!_user.VoicesAvailable.TryGetValue(voice, out string? voiceName) || voiceName == null)
                    return;

                lock (_voicesAvailableLock)
                {
                    var dict = _user.VoicesAvailable.ToDictionary(k => k.Key, v => v.Value);
                    dict.Remove(voice);
                    _user.VoicesAvailable.Remove(voice);
                }
                _logger.Information($"Deleted a voice [voice: {voiceName}]");
            }
            else if (message.Request.Type == "update_tts_voice")
            {
                _logger.Verbose("Updating TTS voice.");
                string voiceId = message.Request.Data["idd"].ToString();
                string voice = message.Request.Data["voice"].ToString();

                if (!_user.VoicesAvailable.TryGetValue(voiceId, out string? voiceName) || voiceName == null)
                    return;

                _user.VoicesAvailable[voiceId] = voice;
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
                _user.VoicesSelected = temp;
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

                var emoteDb = _serviceProvider.GetRequiredService<IEmoteDatabase>();
                var count = 0;
                var duplicateNames = 0;
                foreach (var emote in emotes)
                {
                    if (emoteDb.Get(emote.Name) == null)
                    {
                        emoteDb.Add(emote.Name, emote.Id);
                        count++;
                    }
                    else
                        duplicateNames++;
                }
                _logger.Information($"Fetched {count} emotes from various sources.");
                if (duplicateNames > 0)
                    _logger.Warning($"Found {duplicateNames} emotes with duplicate names.");
            }
            else if (message.Request.Type == "update_tts_voice_state")
            {
                _logger.Verbose("Updating TTS voice states.");
                string voiceId = message.Request.Data["voice"].ToString();
                bool state = message.Request.Data["state"].ToString().ToLower() == "true";

                if (!_user.VoicesAvailable.TryGetValue(voiceId, out string? voiceName) || voiceName == null)
                {
                    _logger.Warning($"Failed to find voice by id [id: {voiceId}]");
                    return;
                }

                if (state)
                    _user.VoicesEnabled.Add(voiceId);
                else
                    _user.VoicesEnabled.Remove(voiceId);
                _logger.Information($"Updated voice state [voice: {voiceName}][new state: {(state ? "enabled" : "disabled")}]");
            }
            else if (message.Request.Type == "get_redemptions")
            {
                _logger.Verbose("Fetching all the redemptions.");
                IEnumerable<Redemption>? redemptions = JsonSerializer.Deserialize<IEnumerable<Redemption>>(message.Data!.ToString()!, _options);
                if (redemptions != null)
                {
                    _logger.Information($"Redemptions [count: {redemptions.Count()}] loaded.");
                    if (hermesRequestData != null)
                        hermesRequestData.Data!.Add("redemptions", redemptions);
                }
                else
                    _logger.Information(message.Data.GetType().ToString());
            }
            else if (message.Request.Type == "get_redeemable_actions")
            {
                _logger.Verbose("Fetching all the redeemable actions.");
                IEnumerable<RedeemableAction>? actions = JsonSerializer.Deserialize<IEnumerable<RedeemableAction>>(message.Data!.ToString()!, _options);
                if (actions == null)
                {
                    _logger.Warning("Failed to read the redeemable actions for redemptions.");
                    return;
                }
                if (hermesRequestData?.Data == null || !(hermesRequestData.Data["redemptions"] is IEnumerable<Redemption> redemptions))
                {
                    _logger.Warning("Failed to read the redemptions while updating redemption actions.");
                    return;
                }

                _logger.Information($"Redeemable actions [count: {actions.Count()}] loaded.");
                var redemptionManager = _serviceProvider.GetRequiredService<RedemptionManager>();
                redemptionManager.Initialize(redemptions, actions.ToDictionary(a => a.Name, a => a));
            }
            else if (message.Request.Type == "get_default_tts_voice")
            {
                string? defaultVoice = message.Data?.ToString();
                if (defaultVoice != null)
                {
                    _user.DefaultTTSVoice = defaultVoice;
                    _logger.Information($"Default TTS voice was changed to '{defaultVoice}'.");
                }
            }
            else if (message.Request.Type == "update_default_tts_voice")
            {
                if (message.Request.Data?.TryGetValue("voice", out object? voice) == true && voice is string v)
                {
                    _user.DefaultTTSVoice = v;
                    _logger.Information($"Default TTS voice was changed to '{v}'.");
                }
                else
                    _logger.Warning("Failed to update default TTS voice via request.");
            }
            else
            {
                _logger.Warning($"Found unknown request type when acknowledging [type: {message.Request.Type}]");
            }

            if (hermesRequestData != null)
            {
                _logger.Debug($"Callback was found for request [request id: {message.Request.RequestId}][type: {message.Request.Type}]");
                hermesRequestData.Callback?.Invoke(hermesRequestData.Data);
            }
        }
    }

    public class HermesRequestData
    {
        public Action<IDictionary<string, object>?>? Callback { get; set; }
        public IDictionary<string, object>? Data { get; set; }
    }
}