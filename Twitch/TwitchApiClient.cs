using System.Text.Json;
using TwitchChatTTS.Helpers;
using Serilog;
using TwitchChatTTS.Twitch.Socket.Messages;
using System.Net.Http.Json;
using System.Net;
using TwitchChatTTS;

public class TwitchApiClient
{
    private readonly Configuration _configuration;
    private readonly ILogger _logger;
    private readonly WebClientWrap _web;


    public TwitchApiClient(
        Configuration configuration,
        ILogger logger
    )
    {
        _configuration = configuration;
        _logger = logger;

        _web = new WebClientWrap(new JsonSerializerOptions()
        {
            PropertyNameCaseInsensitive = false,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });
    }

    private async Task<EventResponse<NotificationInfo>?> CreateEventSubscription(string type, string version, string sessionId, IDictionary<string, string> conditions)
    {
        var subscriptionData = new EventSubscriptionMessage(type, version, sessionId, conditions);
        var base_url = _configuration.Environment == "PROD" || string.IsNullOrWhiteSpace(_configuration.Twitch?.ApiUrl)
            ? "https://api.twitch.tv/helix" : _configuration.Twitch.ApiUrl;
        var response = await _web.Post($"{base_url}/eventsub/subscriptions", subscriptionData);
        if (response.StatusCode == HttpStatusCode.Accepted)
        {
            _logger.Debug($"Twitch API call [type: create event subscription][subscription type: {type}][response: {await response.Content.ReadAsStringAsync()}]");
            return await response.Content.ReadFromJsonAsync(typeof(EventResponse<NotificationInfo>), _web.Options) as EventResponse<NotificationInfo>;
        }
        _logger.Error($"Twitch API call failed [type: create event subscription][subscription type: {type}][response: {await response.Content.ReadAsStringAsync()}]");
        return null;
    }

    public async Task<EventResponse<NotificationInfo>?> CreateEventSubscription(string type, string version, string sessionId, string userId, string? broadcasterId = null)
    {
        var conditions = new Dictionary<string, string>() { { "user_id", userId }, { "broadcaster_user_id", broadcasterId ?? userId }, { "moderator_user_id", broadcasterId ?? userId } };
        return await CreateEventSubscription(type, version, sessionId, conditions);
    }

    public async Task<EventResponse<NotificationInfo>?> CreateChannelRaidEventSubscription(string version, string sessionId, string? from = null, string? to = null)
    {
        var conditions = new Dictionary<string, string>();
        if (from == null && to == null)
            throw new InvalidOperationException("Either or both from and to values must be non-null.");
        if (from != null)
            conditions.Add("from_broadcaster_user_id", from);
        if (to != null)
            conditions.Add("to_broadcaster_user_id", to);

        return await CreateEventSubscription("channel.raid", version, sessionId, conditions);
    }

    public async Task DeleteEventSubscription(string subscriptionId)
    {
        var base_url = _configuration.Environment == "PROD" || string.IsNullOrWhiteSpace(_configuration.Twitch?.ApiUrl)
            ? "https://api.twitch.tv/helix" : _configuration.Twitch.ApiUrl;
        await _web.Delete($"{base_url}/eventsub/subscriptions?id=" + subscriptionId);
    }

    public async Task<EventResponse<ChatterMessage>?> GetChatters(string broadcasterId, string? moderatorId = null)
    {
        moderatorId ??= broadcasterId;
        var response = await _web.Get($"https://api.twitch.tv/helix/chat/chatters?broadcaster_id={broadcasterId}&moderator_id={moderatorId}");
        if (response.StatusCode == HttpStatusCode.OK)
        {
            _logger.Debug($"Twitch API call [type: get chatters][response: {await response.Content.ReadAsStringAsync()}]");
            return await response.Content.ReadFromJsonAsync(typeof(EventResponse<ChatterMessage>), _web.Options) as EventResponse<ChatterMessage>;
        }
        _logger.Error($"Twitch API call failed [type: get chatters][response: {await response.Content.ReadAsStringAsync()}]");
        return null;
    }

    public async Task<EventResponse<NotificationInfo>?> GetSubscriptions(string? status = null, string? broadcasterId = null, string? after = null)
    {
        List<string> queryParams = new List<string>();
        if (!string.IsNullOrWhiteSpace(status))
            queryParams.Add("status=" + status);
        if (!string.IsNullOrWhiteSpace(broadcasterId))
            queryParams.Add("user_id=" + broadcasterId);
        if (!string.IsNullOrWhiteSpace(after))
            queryParams.Add("after=" + after);
        var query = queryParams.Any() ? '?' + string.Join('&', queryParams) : string.Empty;
        return await _web.GetJson<EventResponse<NotificationInfo>>("https://api.twitch.tv/helix/eventsub/subscriptions" + query);
    }

    public void Initialize(string clientId, string accessToken)
    {
        _web.AddHeader("Authorization", "Bearer " + accessToken);
        _web.AddHeader("Client-Id", clientId);
    }
}