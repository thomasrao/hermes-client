using System.Text.Json;
using TwitchChatTTS.Helpers;
using Microsoft.Extensions.Logging;
using TwitchChatTTS;
using TwitchLib.Api.Core.Exceptions;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Events;
using static TwitchChatTTS.Configuration;
using Microsoft.Extensions.DependencyInjection;
using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;
using TwitchLib.PubSub.Interfaces;
using TwitchLib.Client.Interfaces;
using TwitchChatTTS.OBS.Socket;

public class TwitchApiClient {
    private readonly Configuration _configuration;
    private readonly ILogger<TwitchApiClient> _logger;
    private readonly TwitchBotToken _token;
    private readonly ITwitchClient _client;
    private readonly ITwitchPubSub _publisher;
    private readonly WebClientWrap Web;
    private readonly IServiceProvider _serviceProvider;
    private bool Initialized;


    public TwitchApiClient(
        Configuration configuration,
        ILogger<TwitchApiClient> logger,
        TwitchBotToken token,
        ITwitchClient twitchClient,
        ITwitchPubSub twitchPublisher,
        IServiceProvider serviceProvider
    ) {
        _configuration = configuration;
        _logger = logger;
        _token = token;
        _client = twitchClient;
        _publisher = twitchPublisher;
        _serviceProvider = serviceProvider;
        Initialized = false;

        Web = new WebClientWrap(new JsonSerializerOptions() {
            PropertyNameCaseInsensitive = false,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });
        if (!string.IsNullOrWhiteSpace(_configuration.Hermes?.Token))
            Web.AddHeader("x-api-key", _configuration.Hermes.Token.Trim());
    }

    public async Task Authorize() {
        try {
            var authorize = await Web.GetJson<TwitchBotAuth>("https://hermes.goblincaves.com/api/account/reauthorize");
            if (authorize != null && _token.BroadcasterId == authorize.BroadcasterId) {
                _token.AccessToken = authorize.AccessToken;
                _token.RefreshToken = authorize.RefreshToken;
                _logger.LogInformation("Updated Twitch API tokens.");
            } else if (authorize != null) {
                _logger.LogError("Twitch API Authorization failed.");
            }
        } catch (HttpResponseException e) {
            if (string.IsNullOrWhiteSpace(_configuration.Hermes?.Token))
                _logger.LogError("No Hermes API key found. Enter it into the configuration file.");
            else
                _logger.LogError("Invalid Hermes API key. Double check the token. HTTP Error Code: " + e.HttpResponse.StatusCode);
        } catch (JsonException) {
        } catch (Exception e) {
            _logger.LogError(e, "Failed to authorize to Twitch API.");
        }
    }

    public async Task Connect() {
        _client.Connect();
        await _publisher.ConnectAsync();
    }

    public void InitializeClient(string username, IEnumerable<string> channels) {
        ConnectionCredentials credentials = new ConnectionCredentials(username, _token?.AccessToken);
        _client.Initialize(credentials, channels.Distinct().ToList());

        if (Initialized) {
            _logger.LogDebug("Twitch API client has already been initialized.");
            return;
        }

        Initialized = true;

        _client.OnJoinedChannel += async Task (object? s, OnJoinedChannelArgs e) => {
            _logger.LogInformation("Joined channel: " + e.Channel);
        };

        _client.OnConnected += async Task (object? s, OnConnectedArgs e) => {
            _logger.LogInformation("-----------------------------------------------------------");
        };

        _client.OnIncorrectLogin += async Task (object? s, OnIncorrectLoginArgs e) => {
            _logger.LogError(e.Exception, "Incorrect Login on Twitch API client.");

            _logger.LogInformation("Attempting to re-authorize.");
            await Authorize();
        };

        _client.OnConnectionError += async Task (object? s, OnConnectionErrorArgs e) => {
            _logger.LogError("Connection Error: " + e.Error.Message + " (" + e.Error.GetType().Name + ")");

            _logger.LogInformation("Attempting to re-authorize.");
            await Authorize();
        };

        _client.OnError += async Task (object? s, OnErrorEventArgs e) => {
            _logger.LogError(e.Exception, "Twitch API client error.");
        };
    }

    public void InitializePublisher() {
        _publisher.OnPubSubServiceConnected += async (s, e) => {
            _publisher.ListenToChannelPoints(_token.BroadcasterId);
            _publisher.ListenToFollows(_token.BroadcasterId);

            await _publisher.SendTopicsAsync(_token.AccessToken);
            _logger.LogInformation("Twitch PubSub has been connected.");
        };

        _publisher.OnFollow += (s, e) => {
            var client = _serviceProvider.GetRequiredKeyedService<SocketClient<WebSocketMessage>>("obs") as OBSSocketClient;
            if (_configuration.Twitch?.TtsWhenOffline != true && client?.Live == false)
                return;
            
            _logger.LogInformation("Follow: " + e.DisplayName);
        };

        _publisher.OnChannelPointsRewardRedeemed += (s, e) => {
            var client = _serviceProvider.GetRequiredKeyedService<SocketClient<WebSocketMessage>>("obs") as OBSSocketClient;
            if (_configuration.Twitch?.TtsWhenOffline != true && client?.Live == false)
                return;

            _logger.LogInformation($"Channel Point Reward Redeemed: {e.RewardRedeemed.Redemption.Reward.Title} (id: {e.RewardRedeemed.Redemption.Id})");

            if (_configuration.Twitch?.Redeems == null) {
                _logger.LogDebug("No redeems found in the configuration.");
                return;
            }

            var redeemName = e.RewardRedeemed.Redemption.Reward.Title.ToLower().Trim().Replace(" ", "-");
            if (!_configuration.Twitch.Redeems.TryGetValue(redeemName, out RedeemConfiguration? redeem))
                return;

            if (redeem == null)
                return;
            
            // Write or append to file if needed.
            var outputFile = string.IsNullOrWhiteSpace(redeem.OutputFilePath) ? null : redeem.OutputFilePath.Trim();
            if (outputFile == null) {
                _logger.LogDebug($"No output file was provided for redeem '{e.RewardRedeemed.Redemption.Reward.Title}'.");
            } else {
                var outputContent = string.IsNullOrWhiteSpace(redeem.OutputContent) ? null : redeem.OutputContent.Trim().Replace("%USER%", e.RewardRedeemed.Redemption.User.DisplayName).Replace("\\n", "\n");
                if (outputContent == null) {
                    _logger.LogWarning($"No output content was provided for redeem '{e.RewardRedeemed.Redemption.Reward.Title}'.");
                } else {
                    if (redeem.OutputAppend == true) {
                        File.AppendAllText(outputFile, outputContent + "\n");
                    } else {
                        File.WriteAllText(outputFile, outputContent);
                    }
                }
            }

            // Play audio file if needed.
            var audioFile = string.IsNullOrWhiteSpace(redeem.AudioFilePath) ? null : redeem.AudioFilePath.Trim();
            if (audioFile == null) {
                _logger.LogDebug($"No audio file was provided for redeem '{e.RewardRedeemed.Redemption.Reward.Title}'.");
                return;
            }
            if (!File.Exists(audioFile)) {
                _logger.LogWarning($"Cannot find audio file @ {audioFile} for redeem '{e.RewardRedeemed.Redemption.Reward.Title}'.");
                return;
            }
            
            AudioPlaybackEngine.Instance.PlaySound(audioFile);
        };

        /*int psConnectionFailures = 0;
        publisher.OnPubSubServiceError += async (s, e) => {
            Console.WriteLine("PubSub ran into a service error. Attempting to connect again.");
            await Task.Delay(Math.Min(3000 + (1 << psConnectionFailures), 120000));
            var connect = await WebHelper.Get("https://hermes.goblincaves.com/api/account/reauthorize");
            if ((int) connect.StatusCode == 200 || (int) connect.StatusCode == 201) {
                psConnectionFailures = 0;
            } else {
                psConnectionFailures++;
            }

            var twitchBotData2 = await WebHelper.GetJson<TwitchBotToken>("https://hermes.goblincaves.com/api/token/bot");
            if (twitchBotData2 == null) {
                Console.WriteLine("The API is down. Contact the owner.");
                return;
            }
            twitchBotData.access_token = twitchBotData2.access_token;
            await pubsub.ConnectAsync();
        };*/
    }

    public void AddOnNewMessageReceived(AsyncEventHandler<OnMessageReceivedArgs> handler) {
        _client.OnMessageReceived += handler;
    }
}