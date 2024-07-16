using Microsoft.Extensions.DependencyInjection;
using Serilog;
using TwitchChatTTS.Chat.Commands.Parameters;
using TwitchChatTTS.Chat.Groups;
using TwitchChatTTS.Chat.Groups.Permissions;
using TwitchChatTTS.Hermes.Socket;
using TwitchChatTTS.OBS.Socket.Manager;
using TwitchChatTTS.Twitch.Redemptions;
using TwitchLib.Client.Models;

namespace TwitchChatTTS.Chat.Commands
{
    public class RefreshTTSDataCommand : ChatCommand
    {
        private readonly User _user;
        private readonly RedemptionManager _redemptionManager;
        private readonly IGroupPermissionManager _permissionManager;
        private readonly IChatterGroupManager _chatterGroupManager;
        private readonly OBSManager _obsManager;
        private readonly HermesApiClient _hermesApi;
        private readonly ILogger _logger;

        public RefreshTTSDataCommand(
            User user,
            RedemptionManager redemptionManager,
            IGroupPermissionManager permissionManager,
            IChatterGroupManager chatterGroupManager,
            OBSManager obsManager,
            HermesApiClient hermesApi,
            ILogger logger
        ) : base("refresh", "Refreshes certain TTS related data on the client.")
        {
            _user = user;
            _redemptionManager = redemptionManager;
            _permissionManager = permissionManager;
            _chatterGroupManager = chatterGroupManager;
            _obsManager = obsManager;
            _hermesApi = hermesApi;
            _logger = logger;

            AddParameter(new SimpleListedParameter([
                "tts_voice_enabled",
                "word_filters",
                "selected_voices",
                "default_voice",
                "redemptions",
                "obs_cache",
                "permissions"
            ]));
        }

        public override async Task<bool> CheckDefaultPermissions(ChatMessage message)
        {
            return message.IsModerator || message.IsBroadcaster;
        }

        public override async Task Execute(IList<string> args, ChatMessage message, HermesSocketClient client)
        {
            var value = args.First().ToLower();

            switch (value)
            {
                case "tts_voice_enabled":
                    var voicesEnabled = await _hermesApi.FetchTTSEnabledVoices();
                    if (voicesEnabled == null || !voicesEnabled.Any())
                        _user.VoicesEnabled = new HashSet<string>(["Brian"]);
                    else
                        _user.VoicesEnabled = new HashSet<string>(voicesEnabled.Select(v => v));
                    _logger.Information($"{_user.VoicesEnabled.Count} TTS voices have been enabled.");
                    break;
                case "word_filters":
                    var wordFilters = await _hermesApi.FetchTTSWordFilters();
                    _user.RegexFilters = wordFilters.ToList();
                    _logger.Information($"{_user.RegexFilters.Count()} TTS word filters.");
                    break;
                case "selected_voices":
                    {
                        var voicesSelected = await _hermesApi.FetchTTSChatterSelectedVoices();
                        _user.VoicesSelected = voicesSelected.ToDictionary(s => s.ChatterId, s => s.Voice);
                        _logger.Information($"{_user.VoicesSelected.Count} TTS voices have been selected for specific chatters.");
                        break;
                    }
                case "default_voice":
                    _user.DefaultTTSVoice = await _hermesApi.FetchTTSDefaultVoice();
                    _logger.Information("TTS Default Voice: " + _user.DefaultTTSVoice);
                    break;
                case "redemptions":
                    var redemptionActions = await _hermesApi.FetchRedeemableActions();
                    var redemptions = await _hermesApi.FetchRedemptions();
                    _redemptionManager.Initialize(redemptions, redemptionActions.ToDictionary(a => a.Name, a => a));
                    _logger.Information($"Redemption Manager has been refreshed with {redemptionActions.Count()} actions & {redemptions.Count()} redemptions.");
                    break;
                case "obs_cache":
                    {
                        _obsManager.ClearCache();
                        await _obsManager.GetGroupList(async groups => await _obsManager.GetGroupSceneItemList(groups));
                        break;
                    }
                case "permissions":
                    {
                        _chatterGroupManager.Clear();
                        _permissionManager.Clear();

                        var groups = await _hermesApi.FetchGroups();
                        var groupsById = groups.ToDictionary(g => g.Id, g => g);
                        foreach (var group in groups)
                            _chatterGroupManager.Add(group);
                        _logger.Information($"{groups.Count()} groups have been loaded.");

                        var groupChatters = await _hermesApi.FetchGroupChatters();
                        _logger.Debug($"{groupChatters.Count()} group users have been fetched.");

                        var permissions = await _hermesApi.FetchGroupPermissions();
                        foreach (var permission in permissions)
                        {
                            _logger.Debug($"Adding group permission [id: {permission.Id}][group id: {permission.GroupId}][path: {permission.Path}][allow: {permission.Allow?.ToString() ?? "null"}]");
                            if (groupsById.TryGetValue(permission.GroupId, out var group))
                            {
                                _logger.Warning($"Failed to find group by id [id: {permission.Id}][group id: {permission.GroupId}][path: {permission.Path}]");
                                continue;
                            }

                            var path = $"{group.Name}.{permission.Path}";
                            _permissionManager.Set(path, permission.Allow);
                            _logger.Debug($"Added group permission [id: {permission.Id}][group id: {permission.GroupId}][path: {permission.Path}]");
                        }
                        _logger.Information($"{permissions.Count()} group permissions have been loaded.");

                        foreach (var chatter in groupChatters)
                            if (groupsById.TryGetValue(chatter.GroupId, out var group))
                                _chatterGroupManager.Add(chatter.ChatterId, group.Name);
                        _logger.Information($"Users in each group have been loaded.");
                        break;
                    }
                default:
                    _logger.Warning($"Unknown refresh value given [value: {value}]");
                    break;
            }
        }
    }
}