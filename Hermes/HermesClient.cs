using TwitchChatTTS.Helpers;
using TwitchChatTTS;
using System.Text.Json;
using HermesSocketLibrary.Requests.Messages;
using TwitchChatTTS.Hermes;
using TwitchChatTTS.Twitch.Redemptions;

public class HermesApiClient
{
    private readonly WebClientWrap _web;

    public HermesApiClient(Configuration configuration)
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
    }

    public async Task<TTSVersion> GetTTSVersion()
    {
        var version = await _web.GetJson<TTSVersion>("https://hermes.goblincaves.com/api/info/version");
        return version;
    }

    public async Task<Account> FetchHermesAccountDetails()
    {
        var account = await _web.GetJson<Account>("https://hermes.goblincaves.com/api/account");
        if (account == null || account.Id == null || account.Username == null)
            throw new NullReferenceException("Invalid value found while fetching for hermes account data.");
        return account;
    }

    public async Task<TwitchBotToken> FetchTwitchBotToken()
    {
        var token = await _web.GetJson<TwitchBotToken>("https://hermes.goblincaves.com/api/token/bot");
        if (token == null || token.ClientId == null || token.AccessToken == null || token.RefreshToken == null || token.ClientSecret == null)
            throw new Exception("Failed to fetch Twitch API token from Hermes.");

        return token;
    }

    public async Task<IEnumerable<TTSUsernameFilter>> FetchTTSUsernameFilters()
    {
        var filters = await _web.GetJson<IEnumerable<TTSUsernameFilter>>("https://hermes.goblincaves.com/api/settings/tts/filter/users");
        if (filters == null)
            throw new Exception("Failed to fetch TTS username filters from Hermes.");

        return filters;
    }

    public async Task<string> FetchTTSDefaultVoice()
    {
        var data = await _web.GetJson<string>("https://hermes.goblincaves.com/api/settings/tts/default");
        if (data == null)
            throw new Exception("Failed to fetch TTS default voice from Hermes.");

        return data;
    }

    public async Task<IEnumerable<TTSChatterSelectedVoice>> FetchTTSChatterSelectedVoices()
    {
        var voices = await _web.GetJson<IEnumerable<TTSChatterSelectedVoice>>("https://hermes.goblincaves.com/api/settings/tts/selected");
        if (voices == null)
            throw new Exception("Failed to fetch TTS chatter selected voices from Hermes.");

        return voices;
    }

    public async Task<IEnumerable<string>> FetchTTSEnabledVoices()
    {
        var voices = await _web.GetJson<IEnumerable<string>>("https://hermes.goblincaves.com/api/settings/tts");
        if (voices == null)
            throw new Exception("Failed to fetch TTS enabled voices from Hermes.");

        return voices;
    }

    public async Task<IEnumerable<TTSWordFilter>> FetchTTSWordFilters()
    {
        var filters = await _web.GetJson<IEnumerable<TTSWordFilter>>("https://hermes.goblincaves.com/api/settings/tts/filter/words");
        if (filters == null)
            throw new Exception("Failed to fetch TTS word filters from Hermes.");

        return filters;
    }

    public async Task<IEnumerable<Redemption>> FetchRedemptions()
    {
        var redemptions = await _web.GetJson<IEnumerable<Redemption>>("https://hermes.goblincaves.com/api/settings/redemptions", new JsonSerializerOptions()
        {
            PropertyNameCaseInsensitive = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        if (redemptions == null)
            throw new Exception("Failed to redemptions from Hermes.");

        return redemptions;
    }

    public async Task<IEnumerable<RedeemableAction>> FetchRedeemableActions()
    {
        var actions = await _web.GetJson<IEnumerable<RedeemableAction>>("https://hermes.goblincaves.com/api/settings/redemptions/actions");
        if (actions == null)
            throw new Exception("Failed to fetch redeemable actions from Hermes.");

        return actions;
    }
}