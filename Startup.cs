using TwitchChatTTS.OBS.Socket.Manager;
using TwitchChatTTS.OBS.Socket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using TwitchChatTTS;
using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using TwitchChatTTS.Seven.Socket;
using TwitchChatTTS.OBS.Socket.Handlers;
using TwitchChatTTS.Seven.Socket.Handlers;
using TwitchChatTTS.Seven.Socket.Context;
using TwitchChatTTS.Seven;
using TwitchChatTTS.OBS.Socket.Context;
using TwitchLib.Client.Interfaces;
using TwitchLib.Client;
using TwitchLib.PubSub.Interfaces;
using TwitchLib.PubSub;
using TwitchLib.Communication.Interfaces;
using TwitchChatTTS.Seven.Socket.Managers;
using TwitchChatTTS.Hermes.Socket.Handlers;
using TwitchChatTTS.Hermes.Socket;
using TwitchChatTTS.Hermes.Socket.Managers;
using TwitchChatTTS.Chat.Commands.Parameters;
using TwitchChatTTS.Chat.Commands;
using System.Text.Json;
using Serilog;
using Serilog.Events;

// dotnet publish -r linux-x64 -p:PublishSingleFile=true --self-contained true
// dotnet publish -r win-x64 -p:PublishSingleFile=true --self-contained true
// SE voices: https://api.streamelements.com/kappa/v2/speech?voice=brian&text=hello

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
var s = builder.Services;

var deserializer = new DeserializerBuilder()
    .WithNamingConvention(HyphenatedNamingConvention.Instance)
    .Build();

var configContent = File.ReadAllText("tts.config.yml");
var configuration = deserializer.Deserialize<Configuration>(configContent);
var redeemKeys = configuration.Twitch?.Redeems?.Keys;
if (redeemKeys != null && redeemKeys.Any())
{
    foreach (var key in redeemKeys)
    {
        if (key != key.ToLower())
            configuration.Twitch.Redeems.Add(key.ToLower(), configuration.Twitch.Redeems[key]);
    }
}
s.AddSingleton<Configuration>(configuration);

var logger = new LoggerConfiguration()
    #if DEBUG
    .MinimumLevel.Debug()
    #else
    .MinimumLevel.Information()
    #endif
    .WriteTo.File("logs/log.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7)
    .WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Information)
    .CreateLogger();

s.AddSerilog(logger);
s.AddSingleton<User>(new User());

s.AddSingleton<JsonSerializerOptions>(new JsonSerializerOptions()
{
    PropertyNameCaseInsensitive = false,
    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
});

// Command parameters
s.AddKeyedSingleton<ChatCommandParameter, TTSVoiceNameParameter>("parameter-ttsvoicename");
s.AddKeyedSingleton<ChatCommandParameter, UnvalidatedParameter>("parameter-unvalidated");
s.AddKeyedSingleton<ChatCommand, SkipAllCommand>("command-skipall");
s.AddKeyedSingleton<ChatCommand, SkipCommand>("command-skip");
s.AddKeyedSingleton<ChatCommand, VoiceCommand>("command-voice");
s.AddKeyedSingleton<ChatCommand, AddTTSVoiceCommand>("command-addttsvoice");
s.AddKeyedSingleton<ChatCommand, RemoveTTSVoiceCommand>("command-removettsvoice");
s.AddKeyedSingleton<ChatCommand, RefreshTTSDataCommand>("command-refreshttsdata");
s.AddKeyedSingleton<ChatCommand, OBSCommand>("command-obs");
s.AddKeyedSingleton<ChatCommand, TTSCommand>("command-tts");
s.AddKeyedSingleton<ChatCommand, VersionCommand>("command-version");
s.AddSingleton<ChatCommandManager>();

s.AddSingleton<TTSPlayer>();
s.AddSingleton<ChatMessageHandler>();
s.AddSingleton<HermesApiClient>();
s.AddSingleton<TwitchBotAuth>(new TwitchBotAuth());
s.AddTransient<IClient, TwitchLib.Communication.Clients.WebSocketClient>();
s.AddTransient<ITwitchClient, TwitchClient>();
s.AddTransient<ITwitchPubSub, TwitchPubSub>();
s.AddSingleton<TwitchApiClient>();

s.AddSingleton<SevenApiClient>();
s.AddSingleton<EmoteDatabase>(new EmoteDatabase());

// OBS websocket
s.AddSingleton<HelloContext>(sp =>
    new HelloContext()
    {
        Host = string.IsNullOrWhiteSpace(configuration.Obs?.Host) ? null : configuration.Obs.Host.Trim(),
        Port = configuration.Obs?.Port,
        Password = string.IsNullOrWhiteSpace(configuration.Obs?.Password) ? null : configuration.Obs.Password.Trim()
    }
);
s.AddSingleton<OBSRequestBatchManager>();
s.AddKeyedSingleton<IWebSocketHandler, HelloHandler>("obs-hello");
s.AddKeyedSingleton<IWebSocketHandler, IdentifiedHandler>("obs-identified");
s.AddKeyedSingleton<IWebSocketHandler, RequestResponseHandler>("obs-requestresponse");
s.AddKeyedSingleton<IWebSocketHandler, RequestBatchResponseHandler>("obs-requestbatchresponse");
s.AddKeyedSingleton<IWebSocketHandler, EventMessageHandler>("obs-eventmessage");

s.AddKeyedSingleton<HandlerManager<WebSocketClient, IWebSocketHandler>, OBSHandlerManager>("obs");
s.AddKeyedSingleton<HandlerTypeManager<WebSocketClient, IWebSocketHandler>, OBSHandlerTypeManager>("obs");
s.AddKeyedSingleton<SocketClient<WebSocketMessage>, OBSSocketClient>("obs");

// 7tv websocket
s.AddTransient(sp =>
{
    var logger = sp.GetRequiredService<ILogger>();
    var client = sp.GetRequiredKeyedService<SocketClient<WebSocketMessage>>("7tv") as SevenSocketClient;
    if (client == null)
    {
        logger.Error("7tv client == null.");
        return new ReconnectContext() { SessionId = null };
    }
    if (client.ConnectionDetails == null)
    {
        logger.Error("Connection details in 7tv client == null.");
        return new ReconnectContext() { SessionId = null };
    }
    return new ReconnectContext() { SessionId = client.ConnectionDetails.SessionId };
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

// hermes websocket
s.AddKeyedSingleton<IWebSocketHandler, HeartbeatHandler>("hermes-heartbeat");
s.AddKeyedSingleton<IWebSocketHandler, LoginAckHandler>("hermes-loginack");
s.AddKeyedSingleton<IWebSocketHandler, RequestAckHandler>("hermes-requestack");
s.AddKeyedSingleton<IWebSocketHandler, HeartbeatHandler>("hermes-error");

s.AddKeyedSingleton<HandlerManager<WebSocketClient, IWebSocketHandler>, HermesHandlerManager>("hermes");
s.AddKeyedSingleton<HandlerTypeManager<WebSocketClient, IWebSocketHandler>, HermesHandlerTypeManager>("hermes");
s.AddKeyedSingleton<SocketClient<WebSocketMessage>, HermesSocketClient>("hermes");

s.AddHostedService<TTS>();
using IHost host = builder.Build();
await host.RunAsync();