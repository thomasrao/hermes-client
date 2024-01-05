using TwitchLib.Api;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Events;
using TwitchLib.PubSub;
using TwitchLib.PubSub.Events;

public class TwitchApiClient {
    private TwitchBotToken token;
    private TwitchClient client;
    private TwitchPubSub publisher;
    private WebHelper web;
    private bool initialized;


    public TwitchApiClient(TwitchBotToken token) {
        client = new TwitchClient(new WebSocketClient());
        publisher = new TwitchPubSub();
        web = new WebHelper();
        initialized = false;
        this.token = token;
    }

    public async Task<bool> Authorize() {
        var authorize = await web.Get("https://hermes.goblincaves.com/api/account/reauthorize");
        var status = (int) authorize.StatusCode;
        return status == 200 || status == 201;
    }

    public async Task Connect() {
        client.Connect();
        await publisher.ConnectAsync();
    }

    public void InitializeClient(HermesClient hermes, IEnumerable<string> channels) {
        ConnectionCredentials credentials = new ConnectionCredentials(hermes.Username, token.access_token);
        client.Initialize(credentials, channels.Distinct().ToList());

        if (initialized) {
            return;
        }

        initialized = true;

        client.OnJoinedChannel += async Task (object? s, OnJoinedChannelArgs e) => {
            Console.WriteLine("Joined Channel: " + e.Channel);
        };

        client.OnConnected += async Task (object? s, OnConnectedArgs e) => {
            Console.WriteLine("-----------------------------------------------------------");
        };

        client.OnError += async Task (object? s, OnErrorEventArgs e) => {
            Console.WriteLine("Log: " + e.Exception.Message + " (" + e.Exception.GetType().Name + ")");
        };

        client.OnIncorrectLogin += async Task (object? s, OnIncorrectLoginArgs e) => {
            Console.WriteLine("Incorrect Login: " + e.Exception.Message + " (" + e.Exception.GetType().Name + ")");
        };

        client.OnConnectionError += async Task (object? s, OnConnectionErrorArgs e) => {
            Console.WriteLine("Connection Error: " + e.Error.Message + " (" + e.Error.GetType().Name + ")");
        };

        client.OnError += async Task (object? s, OnErrorEventArgs e) => {
            Console.WriteLine("Error: " + e.Exception.Message + " (" + e.Exception.GetType().Name + ")");
        };
    }

    public void InitializePublisher(TTSPlayer player, IEnumerable<string> redeems) {
        publisher.OnPubSubServiceConnected += async (s, e) => {
            publisher.ListenToChannelPoints(token.broadcaster_id);

            await publisher.SendTopicsAsync(token.access_token);
            Console.WriteLine("Twitch PubSub has been connected.");
        };

        publisher.OnFollow += (s, e) => {
            Console.WriteLine("Follow: " + e.DisplayName);
        };

        publisher.OnChannelPointsRewardRedeemed += (s, e) => {
            Console.WriteLine($"Channel Point Reward Redeemed: {e.RewardRedeemed.Redemption.Reward.Title}  (id: {e.RewardRedeemed.Redemption.Id})");

            if (!redeems.Any(r => r.ToLower() == e.RewardRedeemed.Redemption.Reward.Title.ToLower()))
              return;
            
            player.Add(new TTSMessage() {
                Voice = "Brian",
                Message = e.RewardRedeemed.Redemption.Reward.Title,
                File = $"redeems/{e.RewardRedeemed.Redemption.Reward.Title.ToLower()}.mp3",
                Priority = -50
            });
        };

        /*int psConnectionFailures = 0;
        publisher.OnPubSubServiceError += async (s, e) => {
            Console.WriteLine("PubSub ran into a service error. Attempting to connect again.");
            await Task.Delay(Math.Min(3000 + (1 << psConnectionFailures), 120000));
            var connect = await WebHelper.Get("https://hermes.goblincaves.com/api/account/reauthorize");
            if ((int) connect.StatusCode == 200 || (int) connect.StatusCode == 201) {
                psConnectionFailures = 0;
            } else {
                psConnectionFailures++;
            }

            var twitchBotData2 = await WebHelper.GetJson<TwitchBotToken>("https://hermes.goblincaves.com/api/token/bot");
            if (twitchBotData2 == null) {
                Console.WriteLine("The API is down. Contact the owner.");
                return;
            }
            twitchBotData.access_token = twitchBotData2.access_token;
            await pubsub.ConnectAsync();
        };*/
    }

    public void AddOnNewMessageReceived(AsyncEventHandler<OnMessageReceivedArgs> handler) {
        client.OnMessageReceived += handler;
    }
}