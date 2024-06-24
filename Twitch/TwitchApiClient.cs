using System.Text.Json;
using TwitchChatTTS.Helpers;
using Serilog;
using TwitchChatTTS;
using TwitchLib.Api.Core.Exceptions;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Events;
using Microsoft.Extensions.DependencyInjection;
using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;
using TwitchLib.PubSub.Interfaces;
using TwitchLib.Client.Interfaces;
using TwitchChatTTS.OBS.Socket;
using TwitchChatTTS.Twitch.Redemptions;

public class TwitchApiClient
{
    private readonly RedemptionManager _redemptionManager;
    private readonly HermesApiClient _hermesApiClient;
    private readonly Configuration _configuration;
    private readonly TwitchBotAuth _token;
    private readonly ITwitchClient _client;
    private readonly ITwitchPubSub _publisher;
    private readonly WebClientWrap _web;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;
    private bool _initialized;
    private string _broadcasterId;


    public TwitchApiClient(
        RedemptionManager redemptionManager,
        HermesApiClient hermesApiClient,
        Configuration configuration,
        TwitchBotAuth token,
        ITwitchClient twitchClient,
        ITwitchPubSub twitchPublisher,
        IServiceProvider serviceProvider,
        ILogger logger
    )
    {
        _redemptionManager = redemptionManager;
        _hermesApiClient = hermesApiClient;
        _configuration = configuration;
        _token = token;
        _client = twitchClient;
        _publisher = twitchPublisher;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _initialized = false;
        _broadcasterId = string.Empty;

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
            await _client.DisconnectAsync();
            await Task.Delay(TimeSpan.FromSeconds(1));
            await _client.ConnectAsync();
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

            _logger.Information($"New Follower [name: {e.DisplayName}][username: {e.Username}]");
        };

        _publisher.OnChannelPointsRewardRedeemed += async (s, e) =>
        {
            var client = _serviceProvider.GetRequiredKeyedService<SocketClient<WebSocketMessage>>("obs") as OBSSocketClient;
            if (_configuration.Twitch?.TtsWhenOffline != true && client?.Live == false)
                return;

            _logger.Information($"Channel Point Reward Redeemed [redeem: {e.RewardRedeemed.Redemption.Reward.Title}][redeem id: {e.RewardRedeemed.Redemption.Reward.Id}][transaction: {e.RewardRedeemed.Redemption.Id}]");

            var actions = _redemptionManager.Get(e.RewardRedeemed.Redemption.Reward.Id);
            if (!actions.Any())
            {
                _logger.Debug($"No redemable actions for this redeem was found [redeem: {e.RewardRedeemed.Redemption.Reward.Title}][redeem id: {e.RewardRedeemed.Redemption.Reward.Id}][transaction: {e.RewardRedeemed.Redemption.Id}]");
                return;
            }
            _logger.Debug($"Found {actions.Count} actions for this Twitch channel point redemption [redeem: {e.RewardRedeemed.Redemption.Reward.Title}][redeem id: {e.RewardRedeemed.Redemption.Reward.Id}][transaction: {e.RewardRedeemed.Redemption.Id}]");

            foreach (var action in actions)
                try
                {
                    await _redemptionManager.Execute(action, e.RewardRedeemed.Redemption.User.DisplayName);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, $"Failed to execute redeeemable action [action: {action.Name}][action type: {action.Type}][redeem: {e.RewardRedeemed.Redemption.Reward.Title}][redeem id: {e.RewardRedeemed.Redemption.Reward.Id}][transaction: {e.RewardRedeemed.Redemption.Id}]");
                }
        };

        _publisher.OnPubSubServiceClosed += async (s, e) =>
        {
            _logger.Warning("Twitch PubSub ran into a service close. Attempting to connect again.");
            //await Task.Delay(Math.Min(3000 + (1 << psConnectionFailures), 120000));
            var authorized = await Authorize(_broadcasterId);

            var twitchBotData = await _hermesApiClient.FetchTwitchBotToken();
            if (twitchBotData == null)
            {
                Console.WriteLine("The API is down. Contact the owner.");
                return;
            }
            await _publisher.ConnectAsync();
        };
    }

    public void AddOnNewMessageReceived(AsyncEventHandler<OnMessageReceivedArgs> handler)
    {
        _client.OnMessageReceived += handler;
    }
}