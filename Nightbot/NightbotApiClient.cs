using System.Text.Json;
using TwitchChatTTS.Helpers;
using Serilog;
using TwitchChatTTS;

public class NightbotApiClient
{
    private readonly ILogger _logger;
    private readonly WebClientWrap _web;


    public NightbotApiClient(
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

    public async Task Play()
    {
        await _web.Post("https://api.nightbot.tv/1/song_requests/queue/play");
    }

    public async Task Pause()
    {
        await _web.Post("https://api.nightbot.tv/1/song_requests/queue/pause");
    }

    public async Task Skip()
    {
        await _web.Post("https://api.nightbot.tv/1/song_requests/queue/skip");
    }

    public async Task Volume(int volume)
    {
        await _web.Put("https://api.nightbot.tv/1/song_requests", new Dictionary<string, object>() { { "volume", volume } });
    }

    public async Task ClearPlaylist()
    {
        await _web.Delete("https://api.nightbot.tv/1/song_requests/playlist");
    }

    public async Task ClearQueue()
    {
        await _web.Delete("https://api.nightbot.tv/1/song_requests/queue");
    }

    public void Initialize(string clientId, string accessToken)
    {
        _web.AddHeader("Authorization", "Bearer " + accessToken);
        _web.AddHeader("Client-Id", clientId);
    }
}