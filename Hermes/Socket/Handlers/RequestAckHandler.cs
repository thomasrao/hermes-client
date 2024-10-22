using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;
using HermesSocketLibrary.Requests.Callbacks;
using HermesSocketLibrary.Requests.Messages;
using HermesSocketLibrary.Socket.Data;
using HermesSocketServer.Models;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using TwitchChatTTS.Chat.Commands.Limits;
using TwitchChatTTS.Chat.Emotes;
using TwitchChatTTS.Chat.Groups;
using TwitchChatTTS.Chat.Groups.Permissions;
using TwitchChatTTS.Twitch.Redemptions;

namespace TwitchChatTTS.Hermes.Socket.Handlers
{
    public class RequestAckHandler : IWebSocketHandler
    {
        private User _user;
        private readonly ICallbackManager<HermesRequestData> _callbackManager;
        private readonly IChatterGroupManager _groups;
        private readonly IUsagePolicy<long> _policies;
        private readonly TwitchApiClient _twitch;
        private readonly NightbotApiClient _nightbot;
        private readonly IServiceProvider _serviceProvider;
        private readonly JsonSerializerOptions _options;
        private readonly ILogger _logger;

        private readonly object _voicesAvailableLock = new object();

        public int OperationCode { get; } = 4;


        public RequestAckHandler(
            ICallbackManager<HermesRequestData> callbackManager,
            IChatterGroupManager groups,
            IUsagePolicy<long> policies,
            TwitchApiClient twitch,
            NightbotApiClient nightbot,
            IServiceProvider serviceProvider,
            User user,
            JsonSerializerOptions options,
            ILogger logger
        )
        {
            _callbackManager = callbackManager;
            _groups = groups;
            _policies = policies;
            _twitch = twitch;
            _nightbot = nightbot;
            _serviceProvider = serviceProvider;
            _user = user;
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
                string voiceId = message.Request.Data["idd"].ToString();
                string voice = message.Request.Data["voice"].ToString();

                if (!_user.VoicesAvailable.TryGetValue(voiceId, out string? voiceName) || voiceName == null)
                    return;

                _user.VoicesAvailable[voiceId] = voice;
                _logger.Information($"Updated TTS voice [voice: {voice}][id: {voiceId}]");
            }
            else if (message.Request.Type == "get_connections")
            {
                var connections = JsonSerializer.Deserialize<IEnumerable<Connection>>(message.Data?.ToString(), _options);
                if (connections == null)
                {
                    _logger.Error("Null value was given when attempting to fetch connections.");
                    _logger.Debug(message.Data?.ToString());
                    return;
                }

                _user.TwitchConnection = connections.FirstOrDefault(c => c.Type == "twitch" && c.Default);
                _user.NightbotConnection = connections.FirstOrDefault(c => c.Type == "nightbot" && c.Default);

                if (_user.TwitchConnection != null)
                    _twitch.Initialize(_user.TwitchConnection.ClientId, _user.TwitchConnection.AccessToken);
                if (_user.NightbotConnection != null)
                    _nightbot.Initialize(_user.NightbotConnection.ClientId, _user.NightbotConnection.AccessToken);

                _logger.Information($"Fetched connections from TTS account [count: {connections.Count()}][twitch: {_user.TwitchConnection != null}][nightbot: {_user.NightbotConnection != null}]");
            }
            else if (message.Request.Type == "get_tts_users")
            {
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
                var chatters = JsonSerializer.Deserialize<IEnumerable<long>>(message.Data.ToString(), _options);
                if (chatters == null)
                    return;

                _user.Chatters = [.. chatters];
                _logger.Information($"Fetched {chatters.Count()} chatters' id.");
            }
            else if (message.Request.Type == "get_emotes")
            {
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
            else if (message.Request.Type == "get_enabled_tts_voices")
            {
                var enabledTTSVoices = JsonSerializer.Deserialize<IEnumerable<string>>(message.Data.ToString(), _options);
                if (enabledTTSVoices == null)
                {
                    _logger.Error("Failed to load enabled tts voices.");
                    return;
                }

                if (_user.VoicesEnabled == null)
                    _user.VoicesEnabled = enabledTTSVoices.ToHashSet();
                else
                    _user.VoicesEnabled.Clear();
                foreach (var voice in enabledTTSVoices)
                    _user.VoicesEnabled.Add(voice);
                _logger.Information($"TTS voices [count: {_user.VoicesEnabled.Count}] have been enabled.");
            }
            else if (message.Request.Type == "get_permissions")
            {
                var groupInfo = JsonSerializer.Deserialize<GroupInfo>(message.Data.ToString(), _options);
                if (groupInfo == null)
                {
                    _logger.Error("Failed to load groups & permissions.");
                    return;
                }

                var chatterGroupManager = _serviceProvider.GetRequiredService<IChatterGroupManager>();
                var permissionManager = _serviceProvider.GetRequiredService<IGroupPermissionManager>();

                permissionManager.Clear();
                chatterGroupManager.Clear();

                var groupsById = groupInfo.Groups.ToDictionary(g => g.Id, g => g);
                foreach (var group in groupInfo.Groups)
                    chatterGroupManager.Add(group);

                foreach (var permission in groupInfo.GroupPermissions)
                {
                    _logger.Debug($"Adding group permission [permission id: {permission.Id}][group id: {permission.GroupId}][path: {permission.Path}][allow: {permission.Allow?.ToString() ?? "null"}]");
                    if (!groupsById.TryGetValue(permission.GroupId, out var group))
                    {
                        _logger.Warning($"Failed to find group by id [permission id: {permission.Id}][group id: {permission.GroupId}][path: {permission.Path}]");
                        continue;
                    }


                    var path = $"{group.Name}.{permission.Path}";
                    permissionManager.Set(path, permission.Allow);
                    _logger.Debug($"Added group permission [id: {permission.Id}][group id: {permission.GroupId}][path: {permission.Path}]");
                }

                _logger.Information($"Groups [count: {groupInfo.Groups.Count()}] & Permissions [count: {groupInfo.GroupPermissions.Count()}] have been loaded.");

                foreach (var chatter in groupInfo.GroupChatters)
                    if (groupsById.TryGetValue(chatter.GroupId, out var group))
                        chatterGroupManager.Add(chatter.ChatterId, group.Name);
                _logger.Information($"Users in each group [count: {groupInfo.GroupChatters.Count()}] have been loaded.");
            }
            else if (message.Request.Type == "get_tts_word_filters")
            {
                var wordFilters = JsonSerializer.Deserialize<IEnumerable<TTSWordFilter>>(message.Data.ToString(), _options);
                if (wordFilters == null)
                {
                    _logger.Error("Failed to load word filters.");
                    return;
                }

                var filters = wordFilters.Where(f => f.Search != null && f.Replace != null).ToArray();
                foreach (var filter in filters)
                {
                    try
                    {
                        var re = new Regex(filter.Search!, RegexOptions.Compiled);
                        re.Match(string.Empty);
                        filter.Regex = re;
                    }
                    catch (Exception) { }
                }
                _user.RegexFilters = filters;
                _logger.Information($"TTS word filters [count: {_user.RegexFilters.Count()}] have been refreshed.");
            }
            else if (message.Request.Type == "update_tts_voice_state")
            {
                string voiceId = message.Request.Data?["voice"].ToString()!;
                bool state = message.Request.Data?["state"].ToString()!.ToLower() == "true";

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
                IEnumerable<RedeemableAction>? actions = JsonSerializer.Deserialize<IEnumerable<RedeemableAction>>(message.Data!.ToString()!, _options);
                if (actions == null)
                {
                    _logger.Warning("Failed to read the redeemable actions for redemptions.");
                    return;
                }
                if (hermesRequestData?.Data == null || hermesRequestData.Data["redemptions"] is not IEnumerable<Redemption> redemptions)
                {
                    _logger.Warning("Failed to read the redemptions while updating redemption actions.");
                    return;
                }

                _logger.Information($"Redeemable actions [count: {actions.Count()}] loaded.");
                var redemptionManager = _serviceProvider.GetRequiredService<IRedemptionManager>();
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
            else if (message.Request.Type == "get_policies")
            {
                var policies = JsonSerializer.Deserialize<IEnumerable<PolicyMessage>>(message.Data!.ToString()!, _options);
                if (policies == null || !policies.Any())
                {
                    _logger.Information($"Policies have been set to default.");
                    _policies.Set("everyone", "tts", 100, TimeSpan.FromSeconds(15));
                    return;
                }

                foreach (var policy in policies)
                {
                    var group = _groups.Get(policy.GroupId.ToString());
                    if (policy == null)
                    {
                        _logger.Debug($"Policy data failed");
                        continue;
                    }
                    _logger.Debug($"Policy data [policy id: {policy.Id}][path: {policy.Path}][group id: {policy.GroupId}][group name: {group?.Name}]");
                    _policies.Set(group?.Name ?? string.Empty, policy.Path, policy.Usage, TimeSpan.FromMilliseconds(policy.Span));
                }
                _logger.Information($"Policies have been loaded, a total of {policies.Count()} policies.");
            }
            else if (message.Request.Type == "update_policy")
            {
                var policy = JsonSerializer.Deserialize<PolicyMessage>(message.Data!.ToString()!, _options);
                var group = _groups.Get(policy.GroupId.ToString());
                if (policy == null || group == null)
                {
                    _logger.Debug($"Policy data failed");
                    return;
                }
                _logger.Debug($"Policy data [policy id: {policy.Id}][path: {policy.Path}][group id: {policy.GroupId}][group name: {group?.Name}]");
                _policies.Set(group?.Name ?? string.Empty, policy.Path, policy.Usage, TimeSpan.FromMilliseconds(policy.Span));
                _logger.Information($"Policy has been updated [policy id: {policy.Id}]");
            }
            else if (message.Request.Type == "create_policy")
            {
                var policy = JsonSerializer.Deserialize<PolicyMessage>(message.Data!.ToString()!, _options);

                if (policy == null)
                {
                    _logger.Debug($"Policy data failed");
                    return;
                }
                var group = _groups.Get(policy.GroupId.ToString());
                if (group == null)
                {
                    _logger.Debug($"Group data failed");
                    return;
                }
                _logger.Debug($"Policy data [policy id: {policy.Id}][path: {policy.Path}][group id: {policy.GroupId}][group name: {group?.Name}]");
                _policies.Set(group?.Name, policy.Path, policy.Usage, TimeSpan.FromMilliseconds(policy.Span));
                _logger.Information($"Policy has been updated [policy id: {policy.Id}]");
            }
            else if (message.Request.Type == "update_policies")
            {
                var policy = JsonSerializer.Deserialize<PolicyMessage>(message.Data!.ToString()!, _options);
                var group = _groups.Get(policy.GroupId.ToString());
                if (policy == null)
                {
                    _logger.Debug($"Policy data failed");
                    return;
                }
                _logger.Debug($"Policy data [policy id: {policy.Id}][path: {policy.Path}][group id: {policy.GroupId}][group name: {group?.Name}]");
                _policies.Set(group?.Name ?? string.Empty, policy.Path, policy.Usage, TimeSpan.FromMilliseconds(policy.Span));
                _logger.Information($"Policy has been updated [policy id: {policy.Id}]");
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