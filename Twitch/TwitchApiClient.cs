using System.Text.Json;
using TwitchChatTTS.Helpers;
using Serilog;
using TwitchChatTTS.Twitch.Socket.Messages;
using System.Net.Http.Json;
using System.Net;

public class TwitchApiClient
{
    private readonly ILogger _logger;
    private readonly WebClientWrap _web;


    public TwitchApiClient(
        ILogger logger
    )
    {
        _logger = logger;

        _web = new WebClientWrap(new JsonSerializerOptions()
        {
            PropertyNameCaseInsensitive = false,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });
    }

    public async Task<EventResponse<EventSubscriptionMessage>?> CreateEventSubscription(string type, string version, string userId)
    {
        var conditions = new Dictionary<string, string>() { { "user_id", userId }, { "broadcaster_user_id", userId }, { "moderator_user_id", userId } };
        var subscriptionData = new EventSubscriptionMessage(type, version, "https://hermes.goblincaves.com/api/account/authorize", "isdnmjfopsdfmsf4390", conditions);
        var response = await _web.Post("https://api.twitch.tv/helix/eventsub/subscriptions", subscriptionData);
        if (response.StatusCode == HttpStatusCode.Accepted)
        {
            _logger.Debug("Twitch API call [type: create event subscription]: " + await response.Content.ReadAsStringAsync());
            return await response.Content.ReadFromJsonAsync(typeof(EventResponse<EventSubscriptionMessage>)) as EventResponse<EventSubscriptionMessage>;
        }
        _logger.Warning("Twitch api failed to create event subscription: " + await response.Content.ReadAsStringAsync());
        return null;
    }

    public async Task<EventResponse<EventSubscriptionMessage>?> CreateEventSubscription(string type, string version, string sessionId, string userId)
    {
        var conditions = new Dictionary<string, string>() { { "user_id", userId }, { "broadcaster_user_id", userId }, { "moderator_user_id", userId } };
        var subscriptionData = new EventSubscriptionMessage(type, version, sessionId, conditions);
        var response = await _web.Post("https://api.twitch.tv/helix/eventsub/subscriptions", subscriptionData);
        if (response.StatusCode == HttpStatusCode.Accepted)
        {
            _logger.Debug("Twitch API call [type: create event subscription]: " + await response.Content.ReadAsStringAsync());
            return await response.Content.ReadFromJsonAsync(typeof(EventResponse<EventSubscriptionMessage>)) as EventResponse<EventSubscriptionMessage>;
        }
        _logger.Error("Twitch api failed to create event subscription: " + await response.Content.ReadAsStringAsync());
        return null;
    }

    public void Initialize(TwitchBotToken token) {
        _web.AddHeader("Authorization", "Bearer " + token.AccessToken);
        _web.AddHeader("Client-Id", token.ClientId);
    }
}