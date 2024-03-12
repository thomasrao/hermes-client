using TwitchChatTTS.OBS.Socket.Manager;
using TwitchChatTTS.OBS.Socket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using TwitchChatTTS;
using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using TwitchChatTTS.Twitch;
using Microsoft.Extensions.Logging;
using TwitchChatTTS.Seven.Socket.Manager;
using TwitchChatTTS.Seven.Socket;
using TwitchChatTTS.OBS.Socket.Handlers;
using TwitchChatTTS.Seven.Socket.Handlers;
using TwitchChatTTS.Seven.Socket.Context;
using TwitchChatTTS.Seven;
using TwitchChatTTS.OBS.Socket.Context;

/**
Future handshake/connection procedure:
- GET all tts config data
- Continuous connection to server to receive commands from tom & send logs/errors (med priority, though tough task)

Ideas:
- Filter messages by badges.
- Speed up TTS based on message queue size?
- Cut TTS off shortly after raid (based on size of raid)?
- Limit duration of TTS
**/

// dotnet publish -r linux-x64 -p:PublishSingleFile=true --self-contained true
// dotnet publish -r win-x64 -p:PublishSingleFile=true --self-contained true
// SE voices: https://api.streamelements.com/kappa/v2/speech?voice=brian&text=hello

// TODO:
// Fix OBS/7tv websocket connections when not available.
// Make it possible to do things at end of streams.
// Update emote database with twitch emotes.
// Event Subscription for emote usage?

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
var s = builder.Services;

var deserializer = new DeserializerBuilder()
    .WithNamingConvention(HyphenatedNamingConvention.Instance)
    .Build();

var configContent = File.ReadAllText("tts.config.yml");
var configuration = deserializer.Deserialize<Configuration>(configContent);
var redeemKeys = configuration.Twitch?.Redeems?.Keys;
if (redeemKeys is not null) {
    foreach (var key in redeemKeys) {
        if (key != key.ToLower() && configuration.Twitch?.Redeems != null)
            configuration.Twitch.Redeems.Add(key.ToLower(), configuration.Twitch.Redeems[key]);
    }
}
s.AddSingleton<Configuration>(configuration);

s.AddLogging();

s.AddSingleton<TTSContext>(sp => {
    var context = new TTSContext();
    var logger = sp.GetRequiredService<ILogger<TTSContext>>();
    var hermes = sp.GetRequiredService<HermesClient>();

    logger.LogInformation("Fetching TTS username filters...");
    var usernameFiltersList = hermes.FetchTTSUsernameFilters();
    usernameFiltersList.Wait();
    context.UsernameFilters = usernameFiltersList.Result.Where(x => x.Username != null).ToDictionary(x => x.Username ?? "", x => x);
    logger.LogInformation($"{context.UsernameFilters.Where(f => f.Value.Tag == "blacklisted").Count()} username(s) have been blocked.");
    logger.LogInformation($"{context.UsernameFilters.Where(f => f.Value.Tag == "priority").Count()} user(s) have been prioritized.");

    var enabledVoices = hermes.FetchTTSEnabledVoices();
    enabledVoices.Wait();
    context.EnabledVoices = enabledVoices.Result;
    logger.LogInformation($"{context.EnabledVoices.Count()} TTS voices enabled.");

    var wordFilters = hermes.FetchTTSWordFilters();
    wordFilters.Wait();
    context.WordFilters = wordFilters.Result;
    logger.LogInformation($"{context.WordFilters.Count()} TTS word filters.");

    var defaultVoice = hermes.FetchTTSDefaultVoice();
    defaultVoice.Wait();
    context.DefaultVoice = defaultVoice.Result ?? "Brian";
    logger.LogInformation("Default Voice: " + context.DefaultVoice);

    return context;
});
s.AddSingleton<TTSPlayer>();
s.AddSingleton<ChatMessageHandler>();
s.AddSingleton<HermesClient>();
s.AddTransient<TwitchBotToken>(sp => {
    var hermes = sp.GetRequiredService<HermesClient>();
    var task = hermes.FetchTwitchBotToken();
    task.Wait();
    return task.Result;
});
s.AddSingleton<TwitchApiClient>();

s.AddSingleton<SevenApiClient>();
s.AddSingleton<EmoteDatabase>(sp => {
    var api = sp.GetRequiredService<SevenApiClient>();
    var task = api.GetSevenEmotes();
    task.Wait();
    return task.Result;
});
var emoteCounter = new EmoteCounter();
if (!string.IsNullOrWhiteSpace(configuration.Emotes?.CounterFilePath) && File.Exists(configuration.Emotes.CounterFilePath.Trim())) {
    var d = new DeserializerBuilder()
        .WithNamingConvention(HyphenatedNamingConvention.Instance)
        .Build();
    emoteCounter = deserializer.Deserialize<EmoteCounter>(File.ReadAllText(configuration.Emotes.CounterFilePath.Trim()));
}
s.AddSingleton<EmoteCounter>(emoteCounter);

// OBS websocket
s.AddSingleton<HelloContext>(sp =>
    new HelloContext() {
        Host = string.IsNullOrWhiteSpace(configuration.Obs?.Host) ? null : configuration.Obs.Host.Trim(),
        Port = configuration.Obs?.Port,
        Password = string.IsNullOrWhiteSpace(configuration.Obs?.Password) ? null : configuration.Obs.Password.Trim()
    }
);
s.AddKeyedSingleton<IWebSocketHandler, HelloHandler>("obs-hello");
s.AddKeyedSingleton<IWebSocketHandler, IdentifiedHandler>("obs-identified");
s.AddKeyedSingleton<IWebSocketHandler, RequestResponseHandler>("obs-requestresponse");
s.AddKeyedSingleton<IWebSocketHandler, EventMessageHandler>("obs-eventmessage");

s.AddKeyedSingleton<HandlerManager<WebSocketClient, IWebSocketHandler>, OBSHandlerManager>("obs");
s.AddKeyedSingleton<HandlerTypeManager<WebSocketClient, IWebSocketHandler>, OBSHandlerTypeManager>("obs");
s.AddKeyedSingleton<SocketClient<WebSocketMessage>, OBSSocketClient>("obs");

// 7tv websocket
s.AddTransient(sp => {
    var logger = sp.GetRequiredService<ILogger<ReconnectContext>>();
    var configuration = sp.GetRequiredService<Configuration>();
    var client = sp.GetRequiredKeyedService<SocketClient<WebSocketMessage>>("7tv") as SevenSocketClient;
    if (client == null) {
        logger.LogError("7tv client is null.");
        return new ReconnectContext() {
            Protocol = configuration.Seven?.Protocol,
            Url = configuration.Seven?.Url,
            SessionId = null
        };
    }
    if (client.ConnectionDetails == null) {
        logger.LogError("Connection details in 7tv client is null.");
        return new ReconnectContext() {
            Protocol = configuration.Seven?.Protocol,
            Url = configuration.Seven?.Url,
            SessionId = null
        };
    }
    return new ReconnectContext() {
        Protocol = configuration.Seven?.Protocol,
        Url = configuration.Seven?.Url,
        SessionId = client.ConnectionDetails.SessionId
    };
});
s.AddSingleton<SevenHelloContext>(sp => {
    return new SevenHelloContext() {
        Subscriptions = configuration.Seven?.InitialSubscriptions
    };
});
s.AddKeyedSingleton<IWebSocketHandler, SevenHelloHandler>("7tv-sevenhello");
s.AddKeyedSingleton<IWebSocketHandler, HelloHandler>("7tv-hello");
s.AddKeyedSingleton<IWebSocketHandler, DispatchHandler>("7tv-dispatch");
s.AddKeyedSingleton<IWebSocketHandler, ReconnectHandler>("7tv-reconnect");
s.AddKeyedSingleton<IWebSocketHandler, ErrorHandler>("7tv-error");
s.AddKeyedSingleton<IWebSocketHandler, EndOfStreamHandler>("7tv-endofstream");

s.AddKeyedSingleton<HandlerManager<WebSocketClient, IWebSocketHandler>, SevenHandlerManager>("7tv");
s.AddKeyedSingleton<HandlerTypeManager<WebSocketClient, IWebSocketHandler>, SevenHandlerTypeManager>("7tv");
s.AddKeyedSingleton<SocketClient<WebSocketMessage>, SevenSocketClient>("7tv");

s.AddHostedService<TTS>();

using IHost host = builder.Build();
using IServiceScope scope = host.Services.CreateAsyncScope();
IServiceProvider provider = scope.ServiceProvider;
await host.RunAsync();