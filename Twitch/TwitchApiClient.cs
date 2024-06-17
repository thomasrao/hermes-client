using System.Text.Json;
using TwitchChatTTS.Helpers;
using Serilog;
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

public class TwitchApiClient
{
    private readonly Configuration _configuration;
    private readonly ILogger _logger;
    private TwitchBotAuth _token;
    private readonly ITwitchClient _client;
    private readonly ITwitchPubSub _publisher;
    private readonly WebClientWrap _web;
    private readonly IServiceProvider _serviceProvider;
    private bool _initialized;
    private string _broadcasterId;


    public TwitchApiClient(
        Configuration configuration,
        TwitchBotAuth token,
        ITwitchClient twitchClient,
        ITwitchPubSub twitchPublisher,
        IServiceProvider serviceProvider,
        ILogger logger
    )
    {
        _configuration = configuration;
        _token = token;
        _client = twitchClient;
        _publisher = twitchPublisher;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _initialized = false;

        _web = new WebClientWrap(new JsonSerializerOptions()
        {
            PropertyNameCaseInsensitive = false,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });
        if (!string.IsNullOrWhiteSpace(_configuration.Hermes?.Token))
            _web.AddHeader("x-api-key", _configuration.Hermes.Token.Trim());
    }

    public async Task<bool> Authorize(string broadcasterId)
    {
        try
        {
            var authorize = await _web.GetJson<TwitchBotAuth>("https://hermes.goblincaves.com/api/account/reauthorize");
            if (authorize != null && broadcasterId == authorize.BroadcasterId)
            {
                _token.AccessToken = authorize.AccessToken;
                _token.RefreshToken = authorize.RefreshToken;
                _token.UserId = authorize.UserId;
                _token.BroadcasterId = authorize.BroadcasterId;
                _logger.Information("Updated Twitch API tokens.");
            }
            else if (authorize != null)
            {
                _logger.Error("Twitch API Authorization failed: " + authorize.AccessToken + " | " + authorize.RefreshToken + " | " + authorize.UserId + " | " + authorize.BroadcasterId);
                return false;
            }
            _broadcasterId = broadcasterId;
            return true;
        }
        catch (HttpResponseException e)
        {
            if (string.IsNullOrWhiteSpace(_configuration.Hermes?.Token))
                _logger.Error("No Hermes API key found. Enter it into the configuration file.");
            else
                _logger.Error("Invalid Hermes API key. Double check the token. HTTP Error Code: " + e.HttpResponse.StatusCode);
        }
        catch (JsonException)
        {
        }
        catch (Exception e)
        {
            _logger.Error(e, "Failed to authorize to Twitch API.");
        }
        return false;
    }

    public async Task Connect()
    {
        _client.Connect();
        await _publisher.ConnectAsync();
    }

    public void InitializeClient(string username, IEnumerable<string> channels)
    {
        ConnectionCredentials credentials = new ConnectionCredentials(username, _token?.AccessToken);
        _client.Initialize(credentials, channels.Distinct().ToList());

        if (_initialized)
        {
            _logger.Debug("Twitch API client has already been initialized.");
            return;
        }

        _initialized = true;

        _client.OnJoinedChannel += async Task (object? s, OnJoinedChannelArgs e) =>
        {
            _logger.Information("Joined channel: " + e.Channel);
        };

        _client.OnConnected += async Task (object? s, OnConnectedArgs e) =>
        {
            _logger.Information("-----------------------------------------------------------");
        };

        _client.OnIncorrectLogin += async Task (object? s, OnIncorrectLoginArgs e) =>
        {
            _logger.Error(e.Exception, "Incorrect Login on Twitch API client.");

            _logger.Information("Attempting to re-authorize.");
            await Authorize(_broadcasterId);
        };

        _client.OnConnectionError += async Task (object? s, OnConnectionErrorArgs e) =>
        {
            _logger.Error("Connection Error: " + e.Error.Message + " (" + e.Error.GetType().Name + ")");

            _logger.Information("Attempting to re-authorize.");
            await Authorize(_broadcasterId);
        };

        _client.OnError += async Task (object? s, OnErrorEventArgs e) =>
        {
            _logger.Error(e.Exception, "Twitch API client error.");
        };
    }

    public void InitializePublisher()
    {
        _publisher.OnPubSubServiceConnected += async (s, e) =>
        {
            _publisher.ListenToChannelPoints(_token.BroadcasterId);
            _publisher.ListenToFollows(_token.BroadcasterId);

            await _publisher.SendTopicsAsync(_token.AccessToken);
            _logger.Information("Twitch PubSub has been connected.");
        };

        _publisher.OnFollow += (s, e) =>
        {
            var client = _serviceProvider.GetRequiredKeyedService<SocketClient<WebSocketMessage>>("obs") as OBSSocketClient;
            if (_configuration.Twitch?.TtsWhenOffline != true && client?.Live == false)
                return;

            _logger.Information("Follow: " + e.DisplayName);
        };

        _publisher.OnChannelPointsRewardRedeemed += (s, e) =>
        {
            var client = _serviceProvider.GetRequiredKeyedService<SocketClient<WebSocketMessage>>("obs") as OBSSocketClient;
            if (_configuration.Twitch?.TtsWhenOffline != true && client?.Live == false)
                return;

            _logger.Information($"Channel Point Reward Redeemed [redeem: {e.RewardRedeemed.Redemption.Reward.Title}][id: {e.RewardRedeemed.Redemption.Id}]");

            if (_configuration.Twitch?.Redeems == null)
                return;

            var redeemName = e.RewardRedeemed.Redemption.Reward.Title.ToLower().Trim().Replace(" ", "-");
            if (!_configuration.Twitch.Redeems.TryGetValue(redeemName, out RedeemConfiguration? redeem))
                return;

            if (redeem == null)
                return;

            // Write or append to file if needed.
            var outputFile = string.IsNullOrWhiteSpace(redeem.OutputFilePath) ? null : redeem.OutputFilePath.Trim();
            if (outputFile == null)
            {
                _logger.Debug($"No output file was provided for redeem [redeem: {e.RewardRedeemed.Redemption.Reward.Title}][id: {e.RewardRedeemed.Redemption.Id}]");
            }
            else
            {
                var outputContent = string.IsNullOrWhiteSpace(redeem.OutputContent) ? null : redeem.OutputContent.Trim().Replace("%USER%", e.RewardRedeemed.Redemption.User.DisplayName).Replace("\\n", "\n");
                if (outputContent == null)
                {
                    _logger.Warning($"No output content was provided for redeem [redeem: {e.RewardRedeemed.Redemption.Reward.Title}][id: {e.RewardRedeemed.Redemption.Id}]");
                }
                else
                {
                    if (redeem.OutputAppend == true)
                    {
                        File.AppendAllText(outputFile, outputContent + "\n");
                    }
                    else
                    {
                        File.WriteAllText(outputFile, outputContent);
                    }
                }
            }

            // Play audio file if needed.
            var audioFile = string.IsNullOrWhiteSpace(redeem.AudioFilePath) ? null : redeem.AudioFilePath.Trim();
            if (audioFile == null)
            {
                _logger.Debug($"No audio file was provided for redeem [redeem: {e.RewardRedeemed.Redemption.Reward.Title}][id: {e.RewardRedeemed.Redemption.Id}]");
            }
            else if (!File.Exists(audioFile))
            {
                _logger.Warning($"Cannot find audio file [location: {audioFile}] for redeem [redeem: {e.RewardRedeemed.Redemption.Reward.Title}][id: {e.RewardRedeemed.Redemption.Id}]");
            }
            else
            {
                AudioPlaybackEngine.Instance.PlaySound(audioFile);
            }
        };
    }

    public void AddOnNewMessageReceived(AsyncEventHandler<OnMessageReceivedArgs> handler)
    {
        _client.OnMessageReceived += handler;
    }
}