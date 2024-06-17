using System.Text.Json;
using TwitchChatTTS.Helpers;
using Serilog;
using TwitchChatTTS.Seven;
using TwitchChatTTS;

public class SevenApiClient
{
    public static readonly string API_URL = "https://7tv.io/v3";
    public static readonly string WEBSOCKET_URL = "wss://events.7tv.io/v3";

    private WebClientWrap Web { get; }
    private ILogger Logger { get; }


    public SevenApiClient(ILogger logger)
    {
        Logger = logger;
        Web = new WebClientWrap(new JsonSerializerOptions()
        {
            PropertyNameCaseInsensitive = false,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });
    }

    public async Task<EmoteSet?> FetchChannelEmoteSet(string twitchId) {
        try
        {
            var details = await Web.GetJson<UserDetails>($"{API_URL}/users/twitch/" + twitchId);
            return details?.EmoteSet;
        }
        catch (JsonException e)
        {
            Logger.Error(e, "Failed to fetch emotes from 7tv due to improper JSON.");
        }
        catch (Exception e)
        {
            Logger.Error(e, "Failed to fetch emotes from 7tv.");
        }
        return null;
    }

    public async Task<IEnumerable<Emote>?> FetchGlobalSevenEmotes()
    {
        try
        {
            var emoteSet = await Web.GetJson<EmoteSet>($"{API_URL}/emote-sets/6353512c802a0e34bac96dd2");
            return emoteSet?.Emotes;
        }
        catch (JsonException e)
        {
            Logger.Error(e, "Failed to fetch emotes from 7tv due to improper JSON.");
        }
        catch (Exception e)
        {
            Logger.Error(e, "Failed to fetch emotes from 7tv.");
        }
        return null;
    }
}