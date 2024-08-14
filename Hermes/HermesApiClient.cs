using TwitchChatTTS.Helpers;
using TwitchChatTTS;
using System.Text.Json;
using TwitchChatTTS.Hermes;
using Serilog;

public class HermesApiClient
{
    private readonly WebClientWrap _web;
    private readonly ILogger _logger;

    public const string BASE_URL = "tomtospeech.com";

    public HermesApiClient(Configuration configuration, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(configuration.Hermes?.Token))
        {
            throw new Exception("Ensure you have written your API key in \".token\" file, in the same folder as this application.");
        }

        _web = new WebClientWrap(new JsonSerializerOptions()
        {
            PropertyNameCaseInsensitive = false,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });
        _web.AddHeader("x-api-key", configuration.Hermes.Token);
        _logger = logger;
    }

    public async Task<TTSVersion?> GetLatestTTSVersion()
    {
        return await _web.GetJson<TTSVersion>($"https://{BASE_URL}/api/info/version");
    }

    public async Task<Account> FetchHermesAccountDetails()
    {
        var account = await _web.GetJson<Account>($"https://{BASE_URL}/api/account", new JsonSerializerOptions()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        if (account == null || account.Id == null || account.Username == null)
            throw new NullReferenceException("Invalid value found while fetching for hermes account data.");
        return account;
    }
}