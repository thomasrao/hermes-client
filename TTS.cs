using System.Runtime.InteropServices;
using System.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using NAudio.Wave.SampleProviders;
using TwitchLib.Client.Events;
using TwitchChatTTS.Twitch.Redemptions;
using org.mariuszgromada.math.mxparser;
using TwitchChatTTS.Hermes.Socket;
using TwitchChatTTS.Chat.Groups.Permissions;
using TwitchChatTTS.Chat.Groups;
using TwitchChatTTS.OBS.Socket.Manager;
using TwitchChatTTS.Seven.Socket;
using TwitchChatTTS.Chat.Emotes;
using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;

namespace TwitchChatTTS
{
    public class TTS : IHostedService
    {
        public const int MAJOR_VERSION = 3;
        public const int MINOR_VERSION = 9;

        private readonly User _user;
        private readonly HermesApiClient _hermesApiClient;
        private readonly SevenApiClient _sevenApiClient;
        private readonly OBSManager _obsManager;
        private readonly SevenManager _sevenManager;
        private readonly HermesSocketClient _hermes;
        private readonly RedemptionManager _redemptionManager;
        private readonly IChatterGroupManager _chatterGroupManager;
        private readonly IGroupPermissionManager _permissionManager;
        private readonly Configuration _configuration;
        private readonly TTSPlayer _player;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger _logger;

        public TTS(
            User user,
            HermesApiClient hermesApiClient,
            SevenApiClient sevenApiClient,
            OBSManager obsManager,
            SevenManager sevenManager,
            [FromKeyedServices("hermes")] SocketClient<WebSocketMessage> hermes,
            RedemptionManager redemptionManager,
            IChatterGroupManager chatterGroupManager,
            IGroupPermissionManager permissionManager,
            Configuration configuration,
            TTSPlayer player,
            IServiceProvider serviceProvider,
            ILogger logger
        )
        {
            _user = user;
            _hermesApiClient = hermesApiClient;
            _sevenApiClient = sevenApiClient;
            _obsManager = obsManager;
            _sevenManager = sevenManager;
            _hermes = (hermes as HermesSocketClient)!;
            _redemptionManager = redemptionManager;
            _chatterGroupManager = chatterGroupManager;
            _permissionManager = permissionManager;
            _configuration = configuration;
            _player = player;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            Console.Title = "TTS - Twitch Chat";
            License.iConfirmCommercialUse("abcdef");

            if (string.IsNullOrWhiteSpace(_configuration.Hermes?.Token))
            {
                _logger.Error("Hermes API token not set in the configuration file.");
                return;
            }

            var hermesVersion = await _hermesApiClient.GetLatestTTSVersion();
            if (hermesVersion == null)
            {
                _logger.Warning("Failed to fetch latest TTS version. Skipping version check.");
            }
            else if (hermesVersion.MajorVersion > TTS.MAJOR_VERSION || hermesVersion.MajorVersion == TTS.MAJOR_VERSION && hermesVersion.MinorVersion > TTS.MINOR_VERSION)
            {
                _logger.Information($"A new update for TTS is avaiable! Version {hermesVersion.MajorVersion}.{hermesVersion.MinorVersion} is available at {hermesVersion.Download}");
                var changes = hermesVersion.Changelog.Split("\n");
                if (changes != null && changes.Any())
                    _logger.Information("Changelogs:\n  - " + string.Join("\n  - ", changes) + "\n\n");
                await Task.Delay(15 * 1000);
            }

            await InitializeHermesWebsocket();
            try
            {
                await FetchUserData(_user, _hermesApiClient);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to initialize properly. Restart app please.");
                await Task.Delay(30 * 1000);
            }

            var twitchapiclient = await InitializeTwitchApiClient(_user.TwitchUsername, _user.TwitchUserId.ToString());
            if (twitchapiclient == null)
            {
                await Task.Delay(30 * 1000);
                return;
            }

            var emoteSet = await _sevenApiClient.FetchChannelEmoteSet(_user.TwitchUserId.ToString());
            if (emoteSet != null)
                _user.SevenEmoteSetId = emoteSet.Id;

            await InitializeEmotes(_sevenApiClient, emoteSet);
            await InitializeSevenTv();
            await InitializeObs();

            // _logger.Information("Sending a request to server...");
            // await _hermesManager.Send(3, new RequestMessage() {
            //     Type = "get_redeemable_actions",
            //     Data = new Dictionary<string, object>()
            // });
            // _logger.Warning("OS VERSION: " + Environment.OSVersion + " | " + Environment.OSVersion.Platform);
            // return;

            AudioPlaybackEngine.Instance.AddOnMixerInputEnded((object? s, SampleProviderEventArgs e) =>
            {
                if (e.SampleProvider == _player.Playing)
                {
                    _player.Playing = null;
                }
            });

            Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            _logger.Warning("TTS Buffer - Cancellation requested.");
                            return;
                        }

                        var m = _player.ReceiveBuffer();
                        if (m == null)
                        {
                            await Task.Delay(200);
                            continue;
                        }

                        string url = $"https://api.streamelements.com/kappa/v2/speech?voice={m.Voice}&text={HttpUtility.UrlEncode(m.Message)}";
                        var sound = new NetworkWavSound(url);
                        var provider = new CachedWavProvider(sound);
                        var data = AudioPlaybackEngine.Instance.ConvertSound(provider);
                        var resampled = new WdlResamplingSampleProvider(data, AudioPlaybackEngine.Instance.SampleRate);
                        _logger.Verbose("Fetched TTS audio data.");

                        m.Audio = resampled;
                        _player.Ready(m);
                    }
                    catch (COMException e)
                    {
                        _logger.Error(e, "Failed to send request for TTS [HResult: " + e.HResult + "]");
                    }
                    catch (Exception e)
                    {
                        _logger.Error(e, "Failed to send request for TTS.");
                    }
                }
            });

            Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            _logger.Warning("TTS Queue - Cancellation requested.");
                            return;
                        }
                        while (_player.IsEmpty() || _player.Playing != null)
                        {
                            await Task.Delay(200);
                            continue;
                        }
                        var m = _player.ReceiveReady();
                        if (m == null)
                        {
                            continue;
                        }

                        if (!string.IsNullOrWhiteSpace(m.File) && File.Exists(m.File))
                        {
                            _logger.Debug("Playing audio file via TTS: " + m.File);
                            AudioPlaybackEngine.Instance.PlaySound(m.File);
                            continue;
                        }

                        _logger.Debug("Playing message via TTS: " + m.Message);

                        if (m.Audio != null)
                        {
                            _player.Playing = m.Audio;
                            AudioPlaybackEngine.Instance.AddMixerInput(m.Audio);
                        }
                    }
                    catch (Exception e)
                    {
                        _logger.Error(e, "Failed to play a TTS audio message");
                    }
                }
            });

            _logger.Information("Twitch websocket client connecting...");
            await twitchapiclient.Connect();
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
                _logger.Warning("Application has stopped due to cancellation token.");
            else
                _logger.Warning("Application has stopped.");
        }

        private async Task FetchUserData(User user, HermesApiClient hermes)
        {
            var hermesAccount = await hermes.FetchHermesAccountDetails();
            user.HermesUserId = hermesAccount.Id;
            user.HermesUsername = hermesAccount.Username;
            user.TwitchUsername = hermesAccount.Username;

            var twitchBotToken = await hermes.FetchTwitchBotToken();
            user.TwitchUserId = long.Parse(twitchBotToken.BroadcasterId);
            _logger.Information($"Username: {user.TwitchUsername} [id: {user.TwitchUserId}]");

            // user.DefaultTTSVoice = await hermes.FetchTTSDefaultVoice();
            // _logger.Information("TTS Default Voice: " + user.DefaultTTSVoice);

            // var wordFilters = await hermes.FetchTTSWordFilters();
            // user.RegexFilters = wordFilters.ToList();
            // _logger.Information($"{user.RegexFilters.Count()} TTS word filters.");

            var voicesSelected = await hermes.FetchTTSChatterSelectedVoices();
            user.VoicesSelected = voicesSelected.ToDictionary(s => s.ChatterId, s => s.Voice);
            _logger.Information($"{user.VoicesSelected.Count} chatters have selected a specific TTS voice, among {user.VoicesSelected.Values.Distinct().Count()} distinct TTS voices.");

            var voicesEnabled = await hermes.FetchTTSEnabledVoices();
            if (voicesEnabled == null || !voicesEnabled.Any())
                user.VoicesEnabled = new HashSet<string>([user.DefaultTTSVoice]);
            else
                user.VoicesEnabled = new HashSet<string>(voicesEnabled.Select(v => v));
            _logger.Information($"{user.VoicesEnabled.Count} TTS voices have been enabled.");

            var defaultedChatters = voicesSelected.Where(item => item.Voice == null || !user.VoicesEnabled.Contains(item.Voice));
            if (defaultedChatters.Any())
                _logger.Information($"{defaultedChatters.Count()} chatter(s) will have their TTS voice set to default due to having selected a disabled TTS voice.");

            // var redemptionActions = await hermes.FetchRedeemableActions();
            // var redemptions = await hermes.FetchRedemptions();
            // _redemptionManager.Initialize(redemptions, redemptionActions.ToDictionary(a => a.Name, a => a));
            // _logger.Information($"Redemption Manager has been initialized with {redemptionActions.Count()} actions & {redemptions.Count()} redemptions.");

            var groups = await hermes.FetchGroups();
            var groupsById = groups.ToDictionary(g => g.Id, g => g);
            foreach (var group in groups)
                _chatterGroupManager.Add(group);
            _logger.Information($"{groups.Count()} groups have been loaded.");

            var groupChatters = await hermes.FetchGroupChatters();
            _logger.Debug($"{groupChatters.Count()} group users have been fetched.");

            var permissions = await hermes.FetchGroupPermissions();
            foreach (var permission in permissions)
            {
                _logger.Debug($"Adding group permission [permission id: {permission.Id}][group id: {permission.GroupId}][path: {permission.Path}][allow: {permission.Allow?.ToString() ?? "null"}]");
                if (!groupsById.TryGetValue(permission.GroupId, out var group))
                {
                    _logger.Warning($"Failed to find group by id [permission id: {permission.Id}][group id: {permission.GroupId}][path: {permission.Path}]");
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
        }

        private async Task InitializeHermesWebsocket()
        {
            try
            {
                _hermes.Initialize();
                await _hermes.Connect();
            }
            catch (Exception e)
            {
                _logger.Error(e, "Connecting to hermes failed. Skipping hermes websockets.");
            }
        }

        private async Task InitializeSevenTv()
        {
            try
            {
                _sevenManager.Initialize();
                await _sevenManager.Connect();
            }
            catch (Exception e)
            {
                _logger.Error(e, "Connecting to 7tv failed. Skipping 7tv websockets.");
            }
        }

        private async Task InitializeObs()
        {
            try
            {
                _obsManager.Initialize();
                await _obsManager.Connect();
            }
            catch (Exception)
            {
                _logger.Warning("Connecting to obs failed. Skipping obs websockets.");
            }
        }

        private async Task<TwitchApiClient?> InitializeTwitchApiClient(string username, string broadcasterId)
        {
            _logger.Debug("Initializing twitch client.");
            var twitchapiclient = _serviceProvider.GetRequiredService<TwitchApiClient>();
            if (!await twitchapiclient.Authorize(broadcasterId))
            {
                _logger.Error("Cannot connect to Twitch API.");
                return null;
            }

            var channels = _configuration.Twitch?.Channels ?? [username];
            _logger.Information("Twitch channels: " + string.Join(", ", channels));
            twitchapiclient.InitializeClient(username, channels);
            twitchapiclient.InitializePublisher();

            var handler = _serviceProvider.GetRequiredService<ChatMessageHandler>();
            twitchapiclient.AddOnNewMessageReceived(async (object? s, OnMessageReceivedArgs e) =>
            {
                try
                {
                    var result = await handler.Handle(e);
                    if (result.Status != MessageStatus.None || result.Emotes == null || !result.Emotes.Any())
                        return;

                    await _hermes.SendEmoteUsage(e.ChatMessage.Id, result.ChatterId, result.Emotes);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Unable to either execute a command or to send emote usage message.");
                }
            });

            return twitchapiclient;
        }

        private async Task InitializeEmotes(SevenApiClient sevenapi, EmoteSet? channelEmotes)
        {
            var emotes = _serviceProvider.GetRequiredService<IEmoteDatabase>();
            var globalEmotes = await sevenapi.FetchGlobalSevenEmotes();

            if (channelEmotes != null && channelEmotes.Emotes.Any())
            {
                _logger.Information($"Loaded {channelEmotes.Emotes.Count()} 7tv channel emotes.");
                foreach (var entry in channelEmotes.Emotes)
                    emotes.Add(entry.Name, entry.Id);
            }
            if (globalEmotes != null && globalEmotes.Any())
            {
                _logger.Information($"Loaded {globalEmotes.Count()} 7tv global emotes.");
                foreach (var entry in globalEmotes)
                    emotes.Add(entry.Name, entry.Id);
            }
        }
    }
}