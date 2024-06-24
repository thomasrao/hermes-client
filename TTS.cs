using System.Runtime.InteropServices;
using System.Web;
using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;
using HermesSocketLibrary.Socket.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using NAudio.Wave.SampleProviders;
using TwitchChatTTS.Seven;
using TwitchLib.Client.Events;
using TwitchChatTTS.Twitch.Redemptions;
using org.mariuszgromada.math.mxparser;

namespace TwitchChatTTS
{
    public class TTS : IHostedService
    {
        public const int MAJOR_VERSION = 3;
        public const int MINOR_VERSION = 3;

        private readonly RedemptionManager _redemptionManager;
        private readonly Configuration _configuration;
        private readonly TTSPlayer _player;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger _logger;

        public TTS(
            User user,
            HermesApiClient hermesApiClient,
            SevenApiClient sevenApiClient,
            RedemptionManager redemptionManager,
            Configuration configuration,
            TTSPlayer player,
            IServiceProvider serviceProvider,
            ILogger logger
        )
        {
            _redemptionManager = redemptionManager;
            _configuration = configuration;
            _player = player;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            Console.Title = "TTS - Twitch Chat";

            var user = _serviceProvider.GetRequiredService<User>();
            var hermes = _serviceProvider.GetRequiredService<HermesApiClient>();
            var seven = _serviceProvider.GetRequiredService<SevenApiClient>();

            var hermesVersion = await hermes.GetTTSVersion();
            if (hermesVersion.MajorVersion > TTS.MAJOR_VERSION || hermesVersion.MajorVersion == TTS.MAJOR_VERSION && hermesVersion.MinorVersion > TTS.MINOR_VERSION)
            {
                _logger.Information($"A new update for TTS is avaiable! Version {hermesVersion.MajorVersion}.{hermesVersion.MinorVersion} is available at {hermesVersion.Download}");
                var changes = hermesVersion.Changelog.Split("\n");
                if (changes != null && changes.Any())
                    _logger.Information("Changelogs:\n  - " + string.Join("\n  - ", changes) + "\n\n");
                await Task.Delay(15 * 1000);
            }

            try
            {
                await FetchUserData(user, hermes, seven);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to initialize properly.");
                await Task.Delay(30 * 1000);
            }

            var twitchapiclient = await InitializeTwitchApiClient(user.TwitchUsername, user.TwitchUserId.ToString());
            if (twitchapiclient == null)
            {
                await Task.Delay(30 * 1000);
                return;
            }

            var emoteSet = await seven.FetchChannelEmoteSet(user.TwitchUserId.ToString());
            user.SevenEmoteSetId = emoteSet?.Id;

            License.iConfirmCommercialUse("abcdef");

            await InitializeEmotes(seven, emoteSet);
            await InitializeHermesWebsocket();
            await InitializeSevenTv(emoteSet.Id);
            await InitializeObs();

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
                            _logger.Warning("TTS Buffer - Cancellation token was canceled.");
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
                            _logger.Warning("TTS Queue - Cancellation token was canceled.");
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
                            _logger.Information("Playing message: " + m.File);
                            AudioPlaybackEngine.Instance.PlaySound(m.File);
                            continue;
                        }

                        _logger.Information("Playing message: " + m.Message);
                        _player.Playing = m.Audio;
                        if (m.Audio != null)
                            AudioPlaybackEngine.Instance.AddMixerInput(m.Audio);
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

        private async Task FetchUserData(User user, HermesApiClient hermes, SevenApiClient seven)
        {
            var hermesAccount = await hermes.FetchHermesAccountDetails();
            if (hermesAccount == null)
                throw new Exception("Cannot connect to Hermes. Ensure your token is valid.");

            user.HermesUserId = hermesAccount.Id;
            user.HermesUsername = hermesAccount.Username;
            user.TwitchUsername = hermesAccount.Username;

            var twitchBotToken = await hermes.FetchTwitchBotToken();
            user.TwitchUserId = long.Parse(twitchBotToken.BroadcasterId);
            _logger.Information($"Username: {user.TwitchUsername} [id: {user.TwitchUserId}]");

            user.DefaultTTSVoice = await hermes.FetchTTSDefaultVoice();
            _logger.Information("Default Voice: " + user.DefaultTTSVoice);

            var wordFilters = await hermes.FetchTTSWordFilters();
            user.RegexFilters = wordFilters.ToList();
            _logger.Information($"{user.RegexFilters.Count()} TTS word filters.");

            var usernameFilters = await hermes.FetchTTSUsernameFilters();
            user.ChatterFilters = usernameFilters.ToDictionary(e => e.Username, e => e);
            _logger.Information($"{user.ChatterFilters.Where(f => f.Value.Tag == "blacklisted").Count()} username(s) have been blocked.");
            _logger.Information($"{user.ChatterFilters.Where(f => f.Value.Tag == "priority").Count()} user(s) have been prioritized.");

            var voicesSelected = await hermes.FetchTTSChatterSelectedVoices();
            user.VoicesSelected = voicesSelected.ToDictionary(s => s.ChatterId, s => s.Voice);
            _logger.Information($"{user.VoicesSelected.Count} TTS voices have been selected for specific chatters.");

            var voicesEnabled = await hermes.FetchTTSEnabledVoices();
            if (voicesEnabled == null || !voicesEnabled.Any())
                user.VoicesEnabled = new HashSet<string>(["Brian"]);
            else
                user.VoicesEnabled = new HashSet<string>(voicesEnabled.Select(v => v));
            _logger.Information($"{user.VoicesEnabled.Count} TTS voices have been enabled.");

            var defaultedChatters = voicesSelected.Where(item => item.Voice == null || !user.VoicesEnabled.Contains(item.Voice));
            if (defaultedChatters.Any())
                _logger.Information($"{defaultedChatters.Count()} chatter(s) will have their TTS voice set to default due to having selected a disabled TTS voice.");

            var redemptionActions = await hermes.FetchRedeemableActions();
            var redemptions = await hermes.FetchRedemptions();
            foreach (var action in redemptionActions)
                _redemptionManager.AddAction(action);
            foreach (var redemption in redemptions)
                _redemptionManager.AddTwitchRedemption(redemption);
            _redemptionManager.Ready();
            _logger.Information($"Redemption Manager is ready with {redemptionActions.Count()} actions & {redemptions.Count()} redemptions.");
        }

        private async Task InitializeHermesWebsocket()
        {
            try
            {
                _logger.Information("Initializing hermes websocket client.");
                var hermesClient = _serviceProvider.GetRequiredKeyedService<SocketClient<WebSocketMessage>>("hermes");
                var url = "wss://hermes-ws.goblincaves.com";
                _logger.Debug($"Attempting to connect to {url}");
                await hermesClient.ConnectAsync(url);
                hermesClient.Connected = true;
                await hermesClient.Send(1, new HermesLoginMessage()
                {
                    ApiKey = _configuration.Hermes.Token
                });
            }
            catch (Exception)
            {
                _logger.Warning("Connecting to hermes failed. Skipping hermes websockets.");
            }
        }

        private async Task InitializeSevenTv(string emoteSetId)
        {
            try
            {
                _logger.Information("Initializing 7tv websocket client.");
                var sevenClient = _serviceProvider.GetRequiredKeyedService<SocketClient<WebSocketMessage>>("7tv");
                if (string.IsNullOrWhiteSpace(emoteSetId))
                {
                    _logger.Warning("Could not fetch 7tv emotes.");
                    return;
                }
                var url = $"{SevenApiClient.WEBSOCKET_URL}@emote_set.*<object_id={emoteSetId}>";
                _logger.Debug($"Attempting to connect to {url}");
                await sevenClient.ConnectAsync($"{url}");
            }
            catch (Exception)
            {
                _logger.Warning("Connecting to 7tv failed. Skipping 7tv websockets.");
            }
        }

        private async Task InitializeObs()
        {
            if (_configuration.Obs == null || string.IsNullOrWhiteSpace(_configuration.Obs.Host) || !_configuration.Obs.Port.HasValue || _configuration.Obs.Port.Value < 0)
            {
                _logger.Warning("Lacking OBS connection info. Skipping OBS websockets.");
                return;
            }

            try
            {
                var obsClient = _serviceProvider.GetRequiredKeyedService<SocketClient<WebSocketMessage>>("obs");
                var url = $"ws://{_configuration.Obs.Host.Trim()}:{_configuration.Obs.Port}";
                _logger.Debug($"Initializing OBS websocket client. Attempting to connect to {url}");
                await obsClient.ConnectAsync(url);
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

            var channels = _configuration.Twitch.Channels ?? [username];
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

                    var ws = _serviceProvider.GetRequiredKeyedService<SocketClient<WebSocketMessage>>("hermes");
                    await ws.Send(8, new EmoteUsageMessage()
                    {
                        MessageId = e.ChatMessage.Id,
                        DateTime = DateTime.UtcNow,
                        BroadcasterId = result.BroadcasterId,
                        ChatterId = result.ChatterId,
                        Emotes = result.Emotes
                    });
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Unable to send emote usage message.");
                }
            });

            return twitchapiclient;
        }

        private async Task InitializeEmotes(SevenApiClient sevenapi, EmoteSet emoteSet)
        {
            var emotes = _serviceProvider.GetRequiredService<EmoteDatabase>();
            var channelEmotes = emoteSet;
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