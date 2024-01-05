using System;
using TwitchChatTTS.Hermes;

public class HermesClient {
    private Account account;
    private string key;
    private WebHelper _web;

    public string Id { get => account?.id; }
    public string Username { get => account?.username; }


    public HermesClient() {
        // Read API Key from file.
        if (!File.Exists(".token")) {
            throw new Exception("Ensure you have written your API key in \".token\" file, in the same folder as this application.");
        }

        key = File.ReadAllText(".token")?.Trim();
        _web = new WebHelper();
        _web.AddHeader("x-api-key", key);
    }

    public async Task UpdateHermesAccount() {
        ValidateKey();
        account = await _web.GetJson<Account>("https://hermes.goblincaves.com/api/account");
    }

    public async Task<TwitchBotToken> FetchTwitchBotToken() {
        ValidateKey();

        var token = await _web.GetJson<TwitchBotToken>("https://hermes.goblincaves.com/api/token/bot");
        if (token == null) {
            throw new Exception("Failed to fetch Twitch API token from Hermes.");
        }

        return token;
    }

    public async Task<IEnumerable<TTSUsernameFilter>> FetchTTSUsernameFilters() {
        ValidateKey();

        var filters = await _web.GetJson<IEnumerable<TTSUsernameFilter>>("https://hermes.goblincaves.com/api/settings/tts/filter/users");
        if (filters == null) {
            throw new Exception("Failed to fetch TTS username filters from Hermes.");
        }

        return filters;
    }

    public async Task<string> FetchTTSDefaultVoice() {
        ValidateKey();

        var data = await _web.GetJson<TTSVoice>("https://hermes.goblincaves.com/api/settings/tts/default");
        if (data == null) {
            throw new Exception("Failed to fetch TTS default voice from Hermes.");
        }

        return data.label;
    }

    public async Task<IEnumerable<TTSVoice>> FetchTTSEnabledVoices() {
        ValidateKey();

        var voices = await _web.GetJson<IEnumerable<TTSVoice>>("https://hermes.goblincaves.com/api/settings/tts");
        if (voices == null) {
            throw new Exception("Failed to fetch TTS enabled voices from Hermes.");
        }

        return voices;
    }

    public async Task<IEnumerable<TTSWordFilter>> FetchTTSWordFilters() {
        ValidateKey();

        var filters = await _web.GetJson<IEnumerable<TTSWordFilter>>("https://hermes.goblincaves.com/api/settings/tts/filter/words");
        if (filters == null) {
            throw new Exception("Failed to fetch TTS word filters from Hermes.");
        }

        return filters;
    }

    private void ValidateKey() {
        if (string.IsNullOrWhiteSpace(key)) {
            throw new InvalidOperationException("Hermes API key not provided.");
        }
    }
}