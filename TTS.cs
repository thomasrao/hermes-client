using System.Runtime.InteropServices;
using System.Web;
using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;
using HermesSocketLibrary.Socket.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NAudio.Wave.SampleProviders;
using TwitchChatTTS.Hermes.Socket;
using TwitchLib.Client.Events;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TwitchChatTTS
{
    public class TTS : IHostedService
    {
        private readonly ILogger _logger;
        private readonly Configuration _configuration;
        private readonly TTSPlayer _player;
        private readonly IServiceProvider _serviceProvider;

        public TTS(ILogger<TTS> logger, Configuration configuration, TTSPlayer player, IServiceProvider serviceProvider) {
            _logger = logger;
            _configuration = configuration;
            _player = player;
            _serviceProvider = serviceProvider;
        }

        public async Task StartAsync(CancellationToken cancellationToken) {
            Console.Title = "TTS - Twitch Chat";
            
            var user = _serviceProvider.GetRequiredService<User>();
            var hermes = await InitializeHermes();
            
            var hermesAccount = await hermes.FetchHermesAccountDetails();
            user.HermesUserId = hermesAccount.Id;
            user.TwitchUsername = hermesAccount.Username;

            var twitchBotToken = await hermes.FetchTwitchBotToken();
            user.TwitchUserId = long.Parse(twitchBotToken.BroadcasterId);
            _logger.LogInformation($"Username: {user.TwitchUsername} (id: {user.TwitchUserId})");
            
            user.DefaultTTSVoice = await hermes.FetchTTSDefaultVoice();
            _logger.LogInformation("Default Voice: " + user.DefaultTTSVoice);

            var wordFilters = await hermes.FetchTTSWordFilters();
            user.RegexFilters = wordFilters.ToList();
            _logger.LogInformation($"{user.RegexFilters.Count()} TTS word filters.");

            var usernameFilters = await hermes.FetchTTSUsernameFilters();
            user.ChatterFilters = usernameFilters.ToDictionary(e => e.Username, e => e);
            _logger.LogInformation($"{user.ChatterFilters.Where(f => f.Value.Tag == "blacklisted").Count()} username(s) have been blocked.");
            _logger.LogInformation($"{user.ChatterFilters.Where(f => f.Value.Tag == "priority").Count()} user(s) have been prioritized.");

            var twitchapiclient = await InitializeTwitchApiClient(user.TwitchUsername);

            await InitializeHermesWebsocket(user);
            await InitializeSevenTv();
            await InitializeObs();
            
            try {
                AudioPlaybackEngine.Instance.AddOnMixerInputEnded((object? s, SampleProviderEventArgs e) => {
                    if (e.SampleProvider == _player.Playing) {
                        _player.Playing = null;
                    }
                });

                Task.Run(async () => {
                    while (true) {
                        try {
                            if (cancellationToken.IsCancellationRequested) {
                                _logger.LogWarning("TTS Buffer - Cancellation token was canceled.");
                                return;
                            }

                            var m = _player.ReceiveBuffer();
                            if (m == null) {
                                await Task.Delay(200);
                                continue;
                            }

                            string url = $"https://api.streamelements.com/kappa/v2/speech?voice={m.Voice}&text={HttpUtility.UrlEncode(m.Message)}";
                            var sound = new NetworkWavSound(url);
                            var provider = new CachedWavProvider(sound);
                            var data = AudioPlaybackEngine.Instance.ConvertSound(provider);
                            var resampled = new WdlResamplingSampleProvider(data, AudioPlaybackEngine.Instance.SampleRate);
                            _logger.LogDebug("Fetched TTS audio data.");

                            m.Audio = resampled;
                            _player.Ready(m);
                        } catch (COMException e) {
                            _logger.LogError(e, "Failed to send request for TTS (HResult: " + e.HResult + ").");
                        } catch (Exception e) {
                            _logger.LogError(e, "Failed to send request for TTS.");
                        }
                    }
                });

                Task.Run(async () => {
                    while (true) {
                        try {
                            if (cancellationToken.IsCancellationRequested) {
                                _logger.LogWarning("TTS Queue - Cancellation token was canceled.");
                                return;
                            }
                            while (_player.IsEmpty() || _player.Playing != null) {
                                await Task.Delay(200);
                                continue;
                            }
                            var m = _player.ReceiveReady();
                            if (m == null) {
                                continue;
                            }

                            if (!string.IsNullOrWhiteSpace(m.File) && File.Exists(m.File)) {
                                _logger.LogInformation("Playing message: " + m.File);
                                AudioPlaybackEngine.Instance.PlaySound(m.File);
                                continue;
                            }

                            _logger.LogInformation("Playing message: " + m.Message);
                            _player.Playing = m.Audio;
                            if (m.Audio != null)
                                AudioPlaybackEngine.Instance.AddMixerInput(m.Audio);
                        } catch (Exception e) {
                            _logger.LogError(e, "Failed to play a TTS audio message");
                        }
                    }
                });
                
                StartSavingEmoteCounter();

                _logger.LogInformation("Twitch API client connecting...");
                await twitchapiclient.Connect();
            } catch (Exception e) {
                _logger.LogError(e, "Failed to initialize.");
            }
            Console.ReadLine();
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
                _logger.LogWarning("Application has stopped due to cancellation token.");
            else
                _logger.LogWarning("Application has stopped.");
        }

        private async Task InitializeHermesWebsocket(User user) {
            if (_configuration.Hermes?.Token == null) {
                _logger.LogDebug("No api token given to hermes. Skipping hermes websockets.");
                return;
            }

            try {
                _logger.LogInformation("Initializing hermes websocket client.");
                var hermesClient = _serviceProvider.GetRequiredKeyedService<SocketClient<WebSocketMessage>>("hermes") as HermesSocketClient;
                var url = "wss://hermes-ws.goblincaves.com";
                _logger.LogDebug($"Attempting to connect to {url}");
                await hermesClient.ConnectAsync(url);
                await hermesClient.Send(1, new HermesLoginMessage() {
                    ApiKey = _configuration.Hermes.Token
                });

                while (hermesClient.UserId == null)
                    await Task.Delay(TimeSpan.FromMilliseconds(200));
                
                await hermesClient.Send(3, new RequestMessage() {
                    Type = "get_tts_voices",
                    Data = null
                });
                var token = _serviceProvider.GetRequiredService<TwitchBotToken>();
                await hermesClient.Send(3, new RequestMessage() {
                    Type = "get_tts_users",
                    Data = new Dictionary<string, string>() { { "@broadcaster", token.BroadcasterId } }
                });
            } catch (Exception) {
                _logger.LogWarning("Connecting to hermes failed. Skipping hermes websockets.");
            }
        }

        private async Task InitializeSevenTv() {
            if (_configuration.Seven?.UserId == null) {
                _logger.LogDebug("No user id given to 7tv. Skipping 7tv websockets.");
                return;
            }

            try {
                _logger.LogInformation("Initializing 7tv websocket client.");
                var sevenClient = _serviceProvider.GetRequiredKeyedService<SocketClient<WebSocketMessage>>("7tv");
                //var base_url = "@" + string.Join(",", Configuration.Seven.InitialSubscriptions.Select(sub => sub.Type + "<" + string.Join(",", sub.Condition?.Select(e => e.Key + "=" + e.Value) ?? new string[0]) + ">"));
                var url = $"{SevenApiClient.WEBSOCKET_URL}@emote_set.*<object_id={_configuration.Seven.UserId.Trim()}>";
                _logger.LogDebug($"Attempting to connect to {url}");
                await sevenClient.ConnectAsync($"{url}");
            } catch (Exception) {
                _logger.LogWarning("Connecting to 7tv failed. Skipping 7tv websockets.");
            }
        }

        private async Task InitializeObs() {
            if (_configuration.Obs == null || string.IsNullOrWhiteSpace(_configuration.Obs.Host) || !_configuration.Obs.Port.HasValue || _configuration.Obs.Port.Value < 0) {
                _logger.LogDebug("Lacking obs connection info. Skipping obs websockets.");
                return;
            }

            try {
                _logger.LogInformation("Initializing obs websocket client.");
                var obsClient = _serviceProvider.GetRequiredKeyedService<SocketClient<WebSocketMessage>>("obs");
                var url = $"ws://{_configuration.Obs.Host.Trim()}:{_configuration.Obs.Port}";
                _logger.LogDebug($"Attempting to connect to {url}");
                await obsClient.ConnectAsync(url);
            } catch (Exception) {
                _logger.LogWarning("Connecting to obs failed. Skipping obs websockets.");
            }
        }

        private async Task<HermesClient> InitializeHermes() {
            // Fetch id and username based on api key given.
            _logger.LogInformation("Initializing hermes client.");
            var hermes = _serviceProvider.GetRequiredService<HermesClient>();
            await hermes.FetchHermesAccountDetails();
            return hermes;
        }

        private async Task<TwitchApiClient> InitializeTwitchApiClient(string username) {
            _logger.LogInformation("Initializing twitch client.");
            var twitchapiclient = _serviceProvider.GetRequiredService<TwitchApiClient>();
            await twitchapiclient.Authorize();

            var channels = _configuration.Twitch.Channels ?? [username];
            _logger.LogInformation("Twitch channels: " + string.Join(", ", channels));
            twitchapiclient.InitializeClient(username, channels);
            twitchapiclient.InitializePublisher();

            var handler = _serviceProvider.GetRequiredService<ChatMessageHandler>();
            twitchapiclient.AddOnNewMessageReceived(async Task (object? s, OnMessageReceivedArgs e) => {
                var result = await handler.Handle(e);
            });

            return twitchapiclient;
        }
        
        private async Task StartSavingEmoteCounter() {
            Task.Run(async () => {
                while (true) {
                    try {
                        await Task.Delay(TimeSpan.FromSeconds(300));

                        var serializer = new SerializerBuilder()
                            .WithNamingConvention(HyphenatedNamingConvention.Instance)
                            .Build();
                        
                        var chathandler = _serviceProvider.GetRequiredService<ChatMessageHandler>();
                        using (TextWriter writer = File.CreateText(_configuration.Emotes.CounterFilePath.Trim()))
                        {
                            await writer.WriteAsync(serializer.Serialize(chathandler._emoteCounter));
                        }
                    } catch (Exception e) {
                        _logger.LogError(e, "Failed to save the emote counter.");
                    }
                }
            });
        }
    }
}