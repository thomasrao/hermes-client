using TwitchChatTTS.Helpers;
using TwitchChatTTS;
using TwitchChatTTS.Hermes;
using System.Text.Json;

public class HermesClient {
    private WebClientWrap _web;

    public HermesClient(Configuration configuration) {
        if (string.IsNullOrWhiteSpace(configuration.Hermes?.Token)) {
            throw new Exception("Ensure you have written your API key in \".token\" file, in the same folder as this application.");
        }

        _web = new WebClientWrap(new JsonSerializerOptions() {
            PropertyNameCaseInsensitive = false,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });
        _web.AddHeader("x-api-key", configuration.Hermes.Token);
    }

    public async Task<Account> FetchHermesAccountDetails() {
        var account = await _web.GetJson<Account>("https://hermes.goblincaves.com/api/account");
        if (account == null || account.Id == null || account.Username == null)
            throw new NullReferenceException("Invalid value found while fetching for hermes account data.");
        return account;
    }

    public async Task<TwitchBotToken> FetchTwitchBotToken() {
        var token = await _web.GetJson<TwitchBotToken>("https://hermes.goblincaves.com/api/token/bot");
        if (token == null || token.ClientId == null || token.AccessToken == null || token.RefreshToken == null || token.ClientSecret == null)
            throw new Exception("Failed to fetch Twitch API token from Hermes.");

        return token;
    }

    public async Task<IEnumerable<TTSUsernameFilter>> FetchTTSUsernameFilters() {
        var filters = await _web.GetJson<IEnumerable<TTSUsernameFilter>>("https://hermes.goblincaves.com/api/settings/tts/filter/users");
        if (filters == null)
            throw new Exception("Failed to fetch TTS username filters from Hermes.");

        return filters;
    }

    public async Task<string> FetchTTSDefaultVoice() {
        var data = await _web.GetJson<TTSVoice>("https://hermes.goblincaves.com/api/settings/tts/default");
        if (data == null)
            throw new Exception("Failed to fetch TTS default voice from Hermes.");

        return data.Label;
    }

    public async Task<IEnumerable<TTSVoice>> FetchTTSEnabledVoices() {
        var voices = await _web.GetJson<IEnumerable<TTSVoice>>("https://hermes.goblincaves.com/api/settings/tts");
        if (voices == null)
            throw new Exception("Failed to fetch TTS enabled voices from Hermes.");

        return voices;
    }

    public async Task<IEnumerable<TTSWordFilter>> FetchTTSWordFilters() {
        var filters = await _web.GetJson<IEnumerable<TTSWordFilter>>("https://hermes.goblincaves.com/api/settings/tts/filter/words");
        if (filters == null)
            throw new Exception("Failed to fetch TTS word filters from Hermes.");

        return filters;
    }
}