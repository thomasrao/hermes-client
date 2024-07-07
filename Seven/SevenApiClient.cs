using System.Text.Json;
using TwitchChatTTS.Helpers;
using Serilog;
using TwitchChatTTS.Seven;

public class SevenApiClient
{
    public const string API_URL = "https://7tv.io/v3";
    public const string WEBSOCKET_URL = "wss://events.7tv.io/v3";

    private readonly WebClientWrap _web;
    private readonly ILogger _logger;


    public SevenApiClient(ILogger logger)
    {
        _logger = logger;
        _web = new WebClientWrap(new JsonSerializerOptions()
        {
            PropertyNameCaseInsensitive = false,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });
    }

    public async Task<EmoteSet?> FetchChannelEmoteSet(string twitchId)
    {
        try
        {
            var details = await _web.GetJson<UserDetails>($"{API_URL}/users/twitch/" + twitchId);
            return details?.EmoteSet;
        }
        catch (JsonException e)
        {
            _logger.Error(e, "Failed to fetch channel emotes from 7tv due to improper JSON.");
        }
        catch (Exception e)
        {
            _logger.Error(e, "Failed to fetch channel emotes from 7tv.");
        }
        return null;
    }

    public async Task<IEnumerable<Emote>?> FetchGlobalSevenEmotes()
    {
        try
        {
            var emoteSet = await _web.GetJson<EmoteSet>($"{API_URL}/emote-sets/6353512c802a0e34bac96dd2");
            return emoteSet?.Emotes;
        }
        catch (JsonException e)
        {
            _logger.Error(e, "Failed to fetch global emotes from 7tv due to improper JSON.");
        }
        catch (Exception e)
        {
            _logger.Error(e, "Failed to fetch global emotes from 7tv.");
        }
        return null;
    }
}