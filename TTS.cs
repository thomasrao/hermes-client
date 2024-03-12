using System.Runtime.InteropServices;
using System.Web;
using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using TwitchLib.Client.Events;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TwitchChatTTS
{
    public class TTS : IHostedService
    {
        private ILogger Logger { get; }
        private Configuration Configuration { get; }
        private TTSPlayer Player { get; }
        private IServiceProvider ServiceProvider { get; }
        private ISampleProvider? Playing { get; set; }

        public TTS(ILogger<TTS> logger, Configuration configuration, TTSPlayer player, IServiceProvider serviceProvider) {
            Logger = logger;
            Configuration = configuration;
            Player = player;
            ServiceProvider = serviceProvider;
        }

        public async Task StartAsync(CancellationToken cancellationToken) {
            Console.Title = "TTS - Twitch Chat";
            
            await InitializeSevenTv();
            await InitializeObs();
            
            try {
                var hermes = await InitializeHermes();
                var twitchapiclient = await InitializeTwitchApiClient(hermes);

                AudioPlaybackEngine.Instance.AddOnMixerInputEnded((object? s, SampleProviderEventArgs e) => {
                    if (e.SampleProvider == Playing) {
                        Playing = null;
                    }
                });

                Task.Run(async () => {
                    while (true) {
                        try {
                            if (cancellationToken.IsCancellationRequested) {
                                Logger.LogWarning("TTS Buffer - Cancellation token was canceled.");
                                return;
                            }

                            var m = Player.ReceiveBuffer();
                            if (m == null) {
                                await Task.Delay(200);
                                continue;
                            }

                            string url = $"https://api.streamelements.com/kappa/v2/speech?voice={m.Voice}&text={HttpUtility.UrlEncode(m.Message)}";
                            var sound = new NetworkWavSound(url);
                            var provider = new CachedWavProvider(sound);
                            var data = AudioPlaybackEngine.Instance.ConvertSound(provider);
                            var resampled = new WdlResamplingSampleProvider(data, AudioPlaybackEngine.Instance.SampleRate);
                            Logger.LogDebug("Fetched TTS audio data.");

                            m.Audio = resampled;
                            Player.Ready(m);
                        } catch (COMException e) {
                            Logger.LogError(e, "Failed to send request for TTS (HResult: " + e.HResult + ").");
                        } catch (Exception e) {
                            Logger.LogError(e, "Failed to send request for TTS.");
                        }
                    }
                });

                Task.Run(async () => {
                    while (true) {
                        try {
                            if (cancellationToken.IsCancellationRequested) {
                                Logger.LogWarning("TTS Queue - Cancellation token was canceled.");
                                return;
                            }
                            while (Player.IsEmpty() || Playing != null) {
                                await Task.Delay(200);
                                continue;
                            }
                            var m = Player.ReceiveReady();
                            if (m == null) {
                                continue;
                            }

                            if (!string.IsNullOrWhiteSpace(m.File) && File.Exists(m.File)) {
                                Logger.LogInformation("Playing message: " + m.File);
                                AudioPlaybackEngine.Instance.PlaySound(m.File);
                                continue;
                            }

                            Logger.LogInformation("Playing message: " + m.Message);
                            Playing = m.Audio;
                            if (m.Audio != null)
                                AudioPlaybackEngine.Instance.AddMixerInput(m.Audio);
                        } catch (Exception e) {
                            Logger.LogError(e, "Failed to play a TTS audio message");
                        }
                    }
                });
                
                StartSavingEmoteCounter();

                Logger.LogInformation("Twitch API client connecting...");
                await twitchapiclient.Connect();
            } catch (Exception e) {
                Logger.LogError(e, "Failed to initialize.");
            }
            Console.ReadLine();
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
                Logger.LogWarning("Application has stopped due to cancellation token.");
            else
                Logger.LogWarning("Application has stopped.");
        }

        private async Task InitializeSevenTv() {
            Logger.LogInformation("Initializing 7tv client.");
            var sevenClient = ServiceProvider.GetRequiredKeyedService<SocketClient<WebSocketMessage>>("7tv");
            if (Configuration.Seven is not null && !string.IsNullOrWhiteSpace(Configuration.Seven.Url)) {
                var base_url = "@" + string.Join(",", Configuration.Seven.InitialSubscriptions.Select(sub => sub.Type + "<" + string.Join(",", sub.Condition?.Select(e => e.Key + "=" + e.Value) ?? new string[0]) + ">"));
                Logger.LogDebug($"Attempting to connect to {Configuration.Seven.Protocol?.Trim() ?? "wss"}://{Configuration.Seven.Url.Trim()}{base_url}");
                await sevenClient.ConnectAsync($"{Configuration.Seven.Protocol?.Trim() ?? "wss"}://{Configuration.Seven.Url.Trim()}{base_url}");
            }
        }

        private async Task InitializeObs() {
            Logger.LogInformation("Initializing obs client.");
            var obsClient = ServiceProvider.GetRequiredKeyedService<SocketClient<WebSocketMessage>>("obs");
            if (Configuration.Obs is not null && !string.IsNullOrWhiteSpace(Configuration.Obs.Host) && Configuration.Obs.Port.HasValue && Configuration.Obs.Port.Value >= 0) {
                Logger.LogDebug($"Attempting to connect to ws://{Configuration.Obs.Host.Trim()}:{Configuration.Obs.Port}");
                await obsClient.ConnectAsync($"ws://{Configuration.Obs.Host.Trim()}:{Configuration.Obs.Port}");
                await Task.Delay(500);
            }
        }

        private async Task<HermesClient> InitializeHermes() {
            // Fetch id and username based on api key given.
            Logger.LogInformation("Initializing hermes client.");
            var hermes = ServiceProvider.GetRequiredService<HermesClient>();
            await hermes.FetchHermesAccountDetails();

            if (hermes.Username == null)
                throw new Exception("Username fetched from Hermes is invalid.");

            Logger.LogInformation("Username: " + hermes.Username);
            return hermes;
        }

        private async Task<TwitchApiClient> InitializeTwitchApiClient(HermesClient hermes) {
            Logger.LogInformation("Initializing twitch client.");
            var twitchapiclient = ServiceProvider.GetRequiredService<TwitchApiClient>();
            await twitchapiclient.Authorize();

            var channels = Configuration.Twitch?.Channels ?? [hermes.Username];
            Logger.LogInformation("Twitch channels: " + string.Join(", ", channels));
            twitchapiclient.InitializeClient(hermes, channels);
            twitchapiclient.InitializePublisher();

            var handler = ServiceProvider.GetRequiredService<ChatMessageHandler>();
            twitchapiclient.AddOnNewMessageReceived(async Task (object? s, OnMessageReceivedArgs e) => {
                var result = handler.Handle(e);

                switch (result) {
                    case MessageResult.Skip:
                        if (Playing != null) {
                            AudioPlaybackEngine.Instance.RemoveMixerInput(Playing);
                            Playing = null;
                        }
                        break;
                    case MessageResult.SkipAll:
                        Player.RemoveAll();
                        if (Playing != null) {
                            AudioPlaybackEngine.Instance.RemoveMixerInput(Playing);
                            Playing = null;
                        }
                        break;
                    default:
                        break;
                }
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
                        
                        var chathandler = ServiceProvider.GetRequiredService<ChatMessageHandler>();
                        using (TextWriter writer = File.CreateText(Configuration.Emotes.CounterFilePath.Trim()))
                        {
                            await writer.WriteAsync(serializer.Serialize(chathandler.EmoteCounter));
                        }
                    } catch (Exception e) {
                        Logger.LogError(e, "Failed to save the emote counter.");
                    }
                }
            });
        }
    }
}