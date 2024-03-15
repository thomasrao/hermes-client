using System.Text.Json;
using TwitchChatTTS.Helpers;
using Microsoft.Extensions.Logging;
using TwitchChatTTS.Seven;

public class SevenApiClient {
    public static readonly string API_URL = "https://7tv.io/v3";
    public static readonly string WEBSOCKET_URL = "wss://events.7tv.io/v3";

    private WebClientWrap Web { get; }
    private ILogger<SevenApiClient> Logger { get; }
    private long? Id { get; }


    public SevenApiClient(ILogger<SevenApiClient> logger, TwitchBotToken token) {
        Logger = logger;
        Id = long.TryParse(token?.BroadcasterId, out long id) ? id : -1;

        Web = new WebClientWrap(new JsonSerializerOptions() {
            PropertyNameCaseInsensitive = false,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });
    }

    public async Task<EmoteDatabase?> GetSevenEmotes() {
        if (Id == null)
            throw new NullReferenceException(nameof(Id));
        
        try {
            var details = await Web.GetJson<UserDetails>($"{API_URL}/users/twitch/" + Id);
            if (details == null)
                return null;
            
            var emotes = new EmoteDatabase();
            if (details.EmoteSet != null)
                foreach (var emote in details.EmoteSet.Emotes)
                    emotes.Add(emote.Name, emote.Id);
            Logger.LogInformation($"Loaded {details.EmoteSet?.Emotes.Count() ?? 0} emotes from 7tv.");
            return emotes;
        } catch (JsonException e) {
            Logger.LogError(e, "Failed to fetch emotes from 7tv. 2");
        } catch (Exception e) {
            Logger.LogError(e, "Failed to fetch emotes from 7tv.");
        }
        return null;
    }
}