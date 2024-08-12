using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using NAudio.Wave.SampleProviders;
using org.mariuszgromada.math.mxparser;
using TwitchChatTTS.Hermes.Socket;
using TwitchChatTTS.Seven.Socket;
using TwitchChatTTS.Chat.Emotes;
using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;
using TwitchChatTTS.OBS.Socket;
using TwitchChatTTS.Twitch.Socket.Messages;
using TwitchChatTTS.Twitch.Socket;
using TwitchChatTTS.Chat.Commands;
using System.Text;
using TwitchChatTTS.Chat.Speech;

namespace TwitchChatTTS
{
    public class TTS : IHostedService
    {
        public const int MAJOR_VERSION = 4;
        public const int MINOR_VERSION = 3;

        private readonly User _user;
        private readonly HermesApiClient _hermesApiClient;
        private readonly SevenApiClient _sevenApiClient;
        private readonly HermesSocketClient _hermes;
        private readonly OBSSocketClient _obs;
        private readonly SevenSocketClient _seven;
        private readonly TwitchWebsocketClient _twitch;
        private readonly ICommandFactory _commandFactory;
        private readonly ICommandManager _commandManager;
        private readonly IEmoteDatabase _emotes;
        private readonly TTSPlayer _player;
        private readonly AudioPlaybackEngine _playback;
        private readonly Configuration _configuration;
        private readonly ILogger _logger;

        public TTS(
            User user,
            HermesApiClient hermesApiClient,
            SevenApiClient sevenApiClient,
            [FromKeyedServices("hermes")] SocketClient<WebSocketMessage> hermes,
            [FromKeyedServices("obs")] SocketClient<WebSocketMessage> obs,
            [FromKeyedServices("7tv")] SocketClient<WebSocketMessage> seven,
            [FromKeyedServices("twitch")] SocketClient<TwitchWebsocketMessage> twitch,
            ICommandFactory commandFactory,
            ICommandManager commandManager,
            IEmoteDatabase emotes,
            TTSPlayer player,
            AudioPlaybackEngine playback,
            Configuration configuration,
            ILogger logger
        )
        {
            _user = user;
            _hermesApiClient = hermesApiClient;
            _sevenApiClient = sevenApiClient;
            _hermes = (hermes as HermesSocketClient)!;
            _obs = (obs as OBSSocketClient)!;
            _seven = (seven as SevenSocketClient)!;
            _twitch = (twitch as TwitchWebsocketClient)!;
            _commandFactory = commandFactory;
            _commandManager = commandManager;
            _emotes = emotes;
            _configuration = configuration;
            _player = player;
            _playback = playback;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            Console.Title = "TTS - Twitch Chat";
            Console.OutputEncoding = Encoding.UTF8;
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
                    _logger.Information("Changelog:\n  - " + string.Join("\n  - ", changes) + "\n\n");
                await Task.Delay(15 * 1000);
            }

            await InitializeHermesWebsocket();
            try
            {
                var hermesAccount = await _hermesApiClient.FetchHermesAccountDetails();
                _user.HermesUserId = hermesAccount.Id;
                _user.HermesUsername = hermesAccount.Username;
                _user.TwitchUsername = hermesAccount.Username;
                _user.TwitchUserId = long.Parse(hermesAccount.BroadcasterId);
            }
            catch (ArgumentNullException)
            {
                _logger.Error("Ensure you have your Twitch account linked to TTS.");
                await Task.Delay(TimeSpan.FromSeconds(30));
                return;
            }
            catch (FormatException)
            {
                _logger.Error("Ensure you have your Twitch account linked to TTS.");
                await Task.Delay(TimeSpan.FromSeconds(30));
                return;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to initialize properly. Restart app please.");
                await Task.Delay(TimeSpan.FromSeconds(30));
                return;
            }

            _playback.AddOnMixerInputEnded((object? s, SampleProviderEventArgs e) =>
            {
                if (_player.Playing?.Audio == e.SampleProvider)
                {
                    _player.Playing = null;
                }
            });

            try
            {
                await _twitch.Connect();
            }
            catch (Exception e)
            {
                _logger.Error(e, "Failed to connect to Twitch websocket server.");
                await Task.Delay(TimeSpan.FromSeconds(30));
                return;
            }

            var emoteSet = await _sevenApiClient.FetchChannelEmoteSet(_user.TwitchUserId.ToString());
            if (emoteSet != null)
                _user.SevenEmoteSetId = emoteSet.Id;

            _commandManager.Update(_commandFactory);

            await InitializeEmotes(_sevenApiClient, emoteSet);
            await InitializeSevenTv();
            await InitializeObs();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
                _logger.Warning("Application has stopped due to cancellation token.");
            else
                _logger.Warning("Application has stopped.");
            return Task.CompletedTask;
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
                _seven.Initialize();
                await _seven.Connect();
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
                _obs.Initialize();
                await _obs.Connect();
            }
            catch (Exception)
            {
                _logger.Warning("Connecting to obs failed. Skipping obs websockets.");
            }
        }

        private async Task InitializeEmotes(SevenApiClient sevenapi, EmoteSet? channelEmotes)
        {
            var globalEmotes = await sevenapi.FetchGlobalSevenEmotes();

            if (channelEmotes != null && channelEmotes.Emotes.Any())
            {
                _logger.Information($"Loaded {channelEmotes.Emotes.Count()} 7tv channel emotes.");
                foreach (var entry in channelEmotes.Emotes)
                    _emotes.Add(entry.Name, entry.Id);
            }
            if (globalEmotes != null && globalEmotes.Any())
            {
                _logger.Information($"Loaded {globalEmotes.Count()} 7tv global emotes.");
                foreach (var entry in globalEmotes)
                    _emotes.Add(entry.Name, entry.Id);
            }
        }
    }
}