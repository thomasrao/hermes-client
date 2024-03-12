using System.Text.Json;
using TwitchChatTTS.Helpers;
using Microsoft.Extensions.Logging;
using TwitchChatTTS;
using TwitchLib.Api.Core.Exceptions;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Events;
using TwitchLib.PubSub;
using static TwitchChatTTS.Configuration;

public class TwitchApiClient {
    private TwitchBotToken Token { get; }
    private TwitchClient Client { get; }
    private TwitchPubSub Publisher { get; }
    private WebClientWrap Web { get; }
    private Configuration Configuration { get; }
    private ILogger<TwitchApiClient> Logger { get; }
    private bool Initialized { get; set; }


    public TwitchApiClient(Configuration configuration, ILogger<TwitchApiClient> logger, TwitchBotToken token) {
        Configuration = configuration;
        Logger = logger;
        Client = new TwitchClient(new WebSocketClient());
        Publisher = new TwitchPubSub();
        Initialized = false;
        Token = token;

        Web = new WebClientWrap(new JsonSerializerOptions() {
            PropertyNameCaseInsensitive = false,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });
        if (!string.IsNullOrWhiteSpace(Configuration.Hermes?.Token))
            Web.AddHeader("x-api-key", Configuration.Hermes?.Token);
    }

    public async Task Authorize() {
        try {
            var authorize = await Web.GetJson<TwitchBotAuth>("https://hermes.goblincaves.com/api/account/reauthorize");
            if (authorize != null && Token.BroadcasterId == authorize.BroadcasterId) {
                Token.AccessToken = authorize.AccessToken;
                Token.RefreshToken = authorize.RefreshToken;
                Logger.LogInformation("Updated Twitch API tokens.");
            } else if (authorize != null) {
                Logger.LogError("Twitch API Authorization failed.");
            }
        } catch (HttpResponseException e) {
            if (string.IsNullOrWhiteSpace(Configuration.Hermes?.Token))
                Logger.LogError("No Hermes API key found. Enter it into the configuration file.");
            else
                Logger.LogError("Invalid Hermes API key. Double check the token. HTTP Error Code: " + e.HttpResponse.StatusCode);
        } catch (JsonException) {
        } catch (Exception e) {
            Logger.LogError(e, "Failed to authorize to Twitch API.");
        }
    }

    public async Task Connect() {
        Client.Connect();
        await Publisher.ConnectAsync();
    }

    public void InitializeClient(HermesClient hermes, IEnumerable<string> channels) {
        ConnectionCredentials credentials = new ConnectionCredentials(hermes.Username, Token?.AccessToken);
        Client.Initialize(credentials, channels.Distinct().ToList());

        if (Initialized) {
            Logger.LogDebug("Twitch API client has already been initialized.");
            return;
        }

        Initialized = true;

        Client.OnJoinedChannel += async Task (object? s, OnJoinedChannelArgs e) => {
            Logger.LogInformation("Joined channel: " + e.Channel);
        };

        Client.OnConnected += async Task (object? s, OnConnectedArgs e) => {
            Logger.LogInformation("-----------------------------------------------------------");
        };

        Client.OnIncorrectLogin += async Task (object? s, OnIncorrectLoginArgs e) => {
            Logger.LogError(e.Exception, "Incorrect Login on Twitch API client.");

            Logger.LogInformation("Attempting to re-authorize.");
            await Authorize();
        };

        Client.OnConnectionError += async Task (object? s, OnConnectionErrorArgs e) => {
            Logger.LogError("Connection Error: " + e.Error.Message + " (" + e.Error.GetType().Name + ")");
        };

        Client.OnError += async Task (object? s, OnErrorEventArgs e) => {
            Logger.LogError(e.Exception, "Twitch API client error.");
        };
    }

    public void InitializePublisher() {
        Publisher.OnPubSubServiceConnected += async (s, e) => {
            Publisher.ListenToChannelPoints(Token.BroadcasterId);
            Publisher.ListenToFollows(Token.BroadcasterId);

            await Publisher.SendTopicsAsync(Token.AccessToken);
            Logger.LogInformation("Twitch PubSub has been connected.");
        };

        Publisher.OnFollow += (s, e) => {
            Logger.LogInformation("Follow: " + e.DisplayName);
        };

        Publisher.OnChannelPointsRewardRedeemed += (s, e) => {
            Logger.LogInformation($"Channel Point Reward Redeemed: {e.RewardRedeemed.Redemption.Reward.Title} (id: {e.RewardRedeemed.Redemption.Id})");

            if (Configuration.Twitch?.Redeems is null) {
                Logger.LogDebug("No redeems found in the configuration.");
                return;
            }

            var redeemName = e.RewardRedeemed.Redemption.Reward.Title.ToLower().Trim().Replace(" ", "-");
            if (!Configuration.Twitch.Redeems.TryGetValue(redeemName, out RedeemConfiguration? redeem))
                return;

            if (redeem is null)
                return;
            
            // Write or append to file if needed.
            var outputFile = string.IsNullOrWhiteSpace(redeem.OutputFilePath) ? null : redeem.OutputFilePath.Trim();
            if (outputFile is null) {
                Logger.LogDebug($"No output file was provided for redeem '{e.RewardRedeemed.Redemption.Reward.Title}'.");
            } else {
                var outputContent = string.IsNullOrWhiteSpace(redeem.OutputContent) ? null : redeem.OutputContent.Trim().Replace("%USER%", e.RewardRedeemed.Redemption.User.DisplayName).Replace("\\n", "\n");
                if (outputContent is null) {
                    Logger.LogWarning($"No output content was provided for redeem '{e.RewardRedeemed.Redemption.Reward.Title}'.");
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
            if (audioFile is null) {
                Logger.LogDebug($"No audio file was provided for redeem '{e.RewardRedeemed.Redemption.Reward.Title}'.");
                return;
            }
            if (!File.Exists(audioFile)) {
                Logger.LogWarning($"Cannot find audio file @ {audioFile} for redeem '{e.RewardRedeemed.Redemption.Reward.Title}'.");
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
        Client.OnMessageReceived += handler;
    }
}