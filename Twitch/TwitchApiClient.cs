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

    public async Task<EventResponse<NotificationInfo>?> CreateEventSubscription(string type, string version, string sessionId, string userId, string? broadcasterId = null)
    {
        var conditions = new Dictionary<string, string>() { { "user_id", userId }, { "broadcaster_user_id", broadcasterId ?? userId }, { "moderator_user_id", broadcasterId ?? userId } };
        var subscriptionData = new EventSubscriptionMessage(type, version, sessionId, conditions);
        var response = await _web.Post("https://api.twitch.tv/helix/eventsub/subscriptions", subscriptionData);
        if (response.StatusCode == HttpStatusCode.Accepted)
        {
            _logger.Debug("Twitch API call [type: create event subscription]: " + await response.Content.ReadAsStringAsync());
            return await response.Content.ReadFromJsonAsync(typeof(EventResponse<NotificationInfo>)) as EventResponse<NotificationInfo>;
        }
        _logger.Error("Twitch api failed to create event subscription for websocket: " + await response.Content.ReadAsStringAsync());
        return null;
    }

    public async Task DeleteEventSubscription(string subscriptionId)
    {
        await _web.Delete("https://api.twitch.tv/helix/eventsub/subscriptions?id=" + subscriptionId);
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

    public void Initialize(TwitchBotToken token)
    {
        _web.AddHeader("Authorization", "Bearer " + token.AccessToken);
        _web.AddHeader("Client-Id", token.ClientId);
    }
}