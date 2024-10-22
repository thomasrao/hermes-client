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
using TwitchChatTTS.Seven.Socket.Managers;
using TwitchChatTTS.Hermes.Socket.Handlers;
using TwitchChatTTS.Hermes.Socket;
using TwitchChatTTS.Hermes.Socket.Managers;
using TwitchChatTTS.Chat.Commands;
using System.Text.Json;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;
using TwitchChatTTS.Twitch.Redemptions;
using TwitchChatTTS.Chat.Groups.Permissions;
using TwitchChatTTS.Chat.Groups;
using TwitchChatTTS.Chat.Emotes;
using HermesSocketLibrary.Requests.Callbacks;
using static TwitchChatTTS.Chat.Commands.TTSCommands;
using TwitchChatTTS.Twitch.Socket;
using TwitchChatTTS.Twitch.Socket.Messages;
using TwitchChatTTS.Twitch.Socket.Handlers;
using CommonSocketLibrary.Backoff;
using TwitchChatTTS.Chat.Speech;
using TwitchChatTTS.Chat.Messaging;
using TwitchChatTTS.Chat.Observers;
using TwitchChatTTS.Chat.Commands.Limits;

// dotnet publish -r linux-x64 -p:PublishSingleFile=true --self-contained true
// dotnet publish -r win-x64 -p:PublishSingleFile=true --self-contained true
// SE voices: https://api.streamelements.com/kappa/v2/speech?voice=brian&text=hello

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
var s = builder.Services;

var deserializer = new DeserializerBuilder()
    .IgnoreUnmatchedProperties()
    .WithNamingConvention(HyphenatedNamingConvention.Instance)
    .Build();

var configContent = File.ReadAllText("tts.config.yml");
var configuration = deserializer.Deserialize<Configuration>(configContent);
s.AddSingleton(configuration);

var logger = new LoggerConfiguration()
    .MinimumLevel.Verbose()
    .MinimumLevel.Override("mariuszgromada", LogEventLevel.Error)
    .Enrich.FromLogContext()
    .WriteTo.File("logs/log-.log", restrictedToMinimumLevel: LogEventLevel.Debug, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 3)
    .WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Information, theme: SystemConsoleTheme.Colored)
    .CreateLogger();

s.AddSerilog(logger);
s.AddSingleton<User>();
s.AddSingleton<AudioPlaybackEngine>();
s.AddSingleton<ICallbackManager<HermesRequestData>, CallbackManager<HermesRequestData>>();

s.AddSingleton(new JsonSerializerOptions()
{
    PropertyNameCaseInsensitive = false,
    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
});

// Command parameters
s.AddSingleton<IChatCommand, SkipCommand>();
s.AddSingleton<IChatCommand, VoiceCommand>();
s.AddSingleton<IChatCommand, RefreshCommand>();
s.AddSingleton<IChatCommand, NightbotCommand>();
s.AddSingleton<IChatCommand, OBSCommand>();
s.AddSingleton<IChatCommand, TTSCommand>();
s.AddSingleton<IChatCommand, VersionCommand>();
s.AddSingleton<ICommandBuilder, CommandBuilder>();
s.AddSingleton<IChatterGroupManager, ChatterGroupManager>();
s.AddSingleton<IGroupPermissionManager, GroupPermissionManager>();
s.AddSingleton<ICommandManager, CommandManager>();
s.AddTransient<ICommandFactory, CommandFactory>();

s.AddSingleton<TTSPlayer>();
s.AddSingleton<IRedemptionManager, RedemptionManager>();
s.AddSingleton<HermesApiClient>();
s.AddSingleton<TwitchApiClient>();

s.AddSingleton<SevenApiClient>();
s.AddSingleton<IEmoteDatabase, EmoteDatabase>();

s.AddSingleton<TTSConsumer>();
s.AddSingleton<TTSPublisher>();

s.AddSingleton<IChatMessageReader, ChatMessageReader>();
s.AddSingleton<IUsagePolicy<long>, UsagePolicy<long>>();

// OBS websocket
s.AddKeyedSingleton<IWebSocketHandler, HelloHandler>("obs");
s.AddKeyedSingleton<IWebSocketHandler, IdentifiedHandler>("obs");
s.AddKeyedSingleton<IWebSocketHandler, RequestResponseHandler>("obs");
s.AddKeyedSingleton<IWebSocketHandler, RequestBatchResponseHandler>("obs");
s.AddKeyedSingleton<IWebSocketHandler, EventMessageHandler>("obs");

s.AddKeyedSingleton<MessageTypeManager<IWebSocketHandler>, OBSMessageTypeManager>("obs");
s.AddKeyedSingleton<SocketClient<WebSocketMessage>, OBSSocketClient>("obs");

// 7tv websocket
s.AddKeyedSingleton<IBackoff>("7tv", new ExponentialBackoff(1000, 30 * 1000));
s.AddKeyedSingleton<IWebSocketHandler, SevenHelloHandler>("7tv");
s.AddKeyedSingleton<IWebSocketHandler, DispatchHandler>("7tv");
s.AddKeyedSingleton<IWebSocketHandler, ReconnectHandler>("7tv");
s.AddKeyedSingleton<IWebSocketHandler, ErrorHandler>("7tv");
s.AddKeyedSingleton<IWebSocketHandler, EndOfStreamHandler>("7tv");

s.AddKeyedSingleton<MessageTypeManager<IWebSocketHandler>, SevenMessageTypeManager>("7tv");
s.AddKeyedSingleton<SocketClient<WebSocketMessage>, SevenSocketClient>("7tv");

// Nightbot
s.AddSingleton<NightbotApiClient>();

// twitch websocket
s.AddKeyedSingleton<IBackoff>("twitch", new ExponentialBackoff(1000, 120 * 1000));
s.AddSingleton<ITwitchConnectionManager, TwitchConnectionManager>();
s.AddKeyedTransient<SocketClient<TwitchWebsocketMessage>, TwitchWebsocketClient>("twitch", (sp, _) =>
{
    var factory = sp.GetRequiredService<ITwitchConnectionManager>();
    var client = factory.GetWorkingClient();
    client.Connect().Wait();
    return client;
});
s.AddKeyedTransient<SocketClient<TwitchWebsocketMessage>, TwitchWebsocketClient>("twitch-create");

s.AddKeyedSingleton<ITwitchSocketHandler, SessionKeepAliveHandler>("twitch");
s.AddKeyedSingleton<ITwitchSocketHandler, SessionWelcomeHandler>("twitch");
s.AddKeyedSingleton<ITwitchSocketHandler, SessionReconnectHandler>("twitch");
s.AddKeyedSingleton<ITwitchSocketHandler, NotificationHandler>("twitch");

s.AddKeyedSingleton<ITwitchSocketHandler, ChannelAdBreakBeginHandler>("twitch-notifications");
s.AddKeyedSingleton<ITwitchSocketHandler, ChannelBanHandler>("twitch-notifications");
s.AddKeyedSingleton<ITwitchSocketHandler, ChannelChatMessageHandler>("twitch-notifications");
s.AddKeyedSingleton<ITwitchSocketHandler, ChannelChatClearHandler>("twitch-notifications");
s.AddKeyedSingleton<ITwitchSocketHandler, ChannelChatClearUserHandler>("twitch-notifications");
s.AddKeyedSingleton<ITwitchSocketHandler, ChannelChatDeleteMessageHandler>("twitch-notifications");
s.AddKeyedSingleton<ITwitchSocketHandler, ChannelCustomRedemptionHandler>("twitch-notifications");
s.AddKeyedSingleton<ITwitchSocketHandler, ChannelFollowHandler>("twitch-notifications");
s.AddKeyedSingleton<ITwitchSocketHandler, ChannelRaidHandler>("twitch-notifications");
s.AddKeyedSingleton<ITwitchSocketHandler, ChannelResubscriptionHandler>("twitch-notifications");
s.AddKeyedSingleton<ITwitchSocketHandler, ChannelSubscriptionHandler>("twitch-notifications");
s.AddKeyedSingleton<ITwitchSocketHandler, ChannelSubscriptionGiftHandler>("twitch-notifications");

// hermes websocket
s.AddKeyedSingleton<IBackoff>("hermes", new ExponentialBackoff(1000, 15 * 1000));
s.AddKeyedSingleton<IWebSocketHandler, HeartbeatHandler>("hermes");
s.AddKeyedSingleton<IWebSocketHandler, LoginAckHandler>("hermes");
s.AddKeyedSingleton<IWebSocketHandler, RequestAckHandler>("hermes");

s.AddKeyedSingleton<MessageTypeManager<IWebSocketHandler>, HermesMessageTypeManager>("hermes");
s.AddKeyedSingleton<SocketClient<WebSocketMessage>, HermesSocketClient>("hermes");

s.AddSingleton<TTSEngine>();

s.AddHostedService<TTSListening>();
s.AddHostedService<TTSEngine>(p => p.GetRequiredService<TTSEngine>());
s.AddHostedService<TTS>();
using IHost host = builder.Build();
await host.RunAsync();