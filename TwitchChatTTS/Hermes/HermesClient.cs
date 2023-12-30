using System;

public class HermesClient {
    private Account account;
    private string key;

    public string Id { get => account?.id; }
    public string Username { get => account?.username; }


    public HermesClient() {
        // Read API Key from file.
        if (!File.Exists(".token")) {
            throw new Exception("Ensure you have written your API key in \".token\" file, in the same folder as this application.");
        }

        key = File.ReadAllText(".token")?.Trim();
        WebHelper.AddHeader("x-api-key", key);
    }

    public async Task UpdateHermesAccount() {
        account = await WebHelper.GetJson<Account>("https://hermes.goblincaves.com/api/account");
    }

    public async Task<TwitchBotToken> FetchTwitchBotToken() {
        if (string.IsNullOrWhiteSpace(key)) {
            throw new InvalidOperationException("Hermes API key not provided.");
        }

        var token = await WebHelper.GetJson<TwitchBotToken>("https://hermes.goblincaves.com/api/token/bot");
        if (token == null) {
            throw new Exception("Failed to fetch Twitch API token from Hermes.");
        }

        return token;
    }
}