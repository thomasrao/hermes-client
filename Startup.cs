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
using Serilog.Sinks.SystemConsole.Themes;
using TwitchChatTTS.Twitch.Redemptions;
using TwitchChatTTS.Chat.Groups.Permissions;
using TwitchChatTTS.Chat.Groups;
using TwitchChatTTS.Chat.Emotes;
using HermesSocketLibrary.Requests.Callbacks;
using static TwitchChatTTS.Chat.Commands.TTSCommands;

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
s.AddSingleton<Configuration>(configuration);

var logger = new LoggerConfiguration()
    .MinimumLevel.Verbose()
    //.MinimumLevel.Override("TwitchLib.Communication.Clients.WebSocketClient", LogEventLevel.Warning)
    //.MinimumLevel.Override("TwitchLib.PubSub.TwitchPubSub", LogEventLevel.Warning)
    .MinimumLevel.Override("TwitchLib", LogEventLevel.Warning)
    .MinimumLevel.Override("mariuszgromada", LogEventLevel.Error)
    .Enrich.FromLogContext()
    .WriteTo.File("logs/log-.log", restrictedToMinimumLevel: LogEventLevel.Debug, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 3)
    .WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Information, theme: SystemConsoleTheme.Colored)
    .CreateLogger();

s.AddSerilog(logger);
s.AddSingleton<User>(new User());
s.AddSingleton<ICallbackManager<HermesRequestData>, CallbackManager<HermesRequestData>>();

s.AddSingleton<JsonSerializerOptions>(new JsonSerializerOptions()
{
    PropertyNameCaseInsensitive = false,
    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
});

// Command parameters
s.AddSingleton<IChatCommand, SkipCommand>();
s.AddSingleton<IChatCommand, VoiceCommand>();
s.AddSingleton<IChatCommand, RefreshCommand>();
s.AddSingleton<IChatCommand, OBSCommand>();
s.AddSingleton<IChatCommand, TTSCommand>();
s.AddSingleton<IChatCommand, VersionCommand>();
s.AddSingleton<ICommandBuilder, CommandBuilder>();
s.AddSingleton<IChatterGroupManager, ChatterGroupManager>();
s.AddSingleton<IGroupPermissionManager, GroupPermissionManager>();
s.AddSingleton<CommandManager>();

s.AddSingleton<TTSPlayer>();
s.AddSingleton<ChatMessageHandler>();
s.AddSingleton<RedemptionManager>();
s.AddSingleton<HermesApiClient>();
s.AddSingleton<TwitchBotAuth>();
s.AddTransient<IClient, TwitchLib.Communication.Clients.WebSocketClient>();
s.AddTransient<ITwitchClient, TwitchClient>();
s.AddTransient<ITwitchPubSub, TwitchPubSub>();
s.AddSingleton<TwitchApiClient>();

s.AddSingleton<SevenApiClient>();
s.AddSingleton<IEmoteDatabase, EmoteDatabase>();

// OBS websocket
s.AddKeyedSingleton<IWebSocketHandler, HelloHandler>("obs");
s.AddKeyedSingleton<IWebSocketHandler, IdentifiedHandler>("obs");
s.AddKeyedSingleton<IWebSocketHandler, RequestResponseHandler>("obs");
s.AddKeyedSingleton<IWebSocketHandler, RequestBatchResponseHandler>("obs");
s.AddKeyedSingleton<IWebSocketHandler, EventMessageHandler>("obs");

s.AddKeyedSingleton<MessageTypeManager<IWebSocketHandler>, OBSMessageTypeManager>("obs");
s.AddKeyedSingleton<SocketClient<WebSocketMessage>, OBSSocketClient>("obs");

// 7tv websocket
s.AddKeyedSingleton<IWebSocketHandler, SevenHelloHandler>("7tv");
s.AddKeyedSingleton<IWebSocketHandler, DispatchHandler>("7tv");
s.AddKeyedSingleton<IWebSocketHandler, ReconnectHandler>("7tv");
s.AddKeyedSingleton<IWebSocketHandler, ErrorHandler>("7tv");
s.AddKeyedSingleton<IWebSocketHandler, EndOfStreamHandler>("7tv");

s.AddKeyedSingleton<MessageTypeManager<IWebSocketHandler>, SevenMessageTypeManager>("7tv");
s.AddKeyedSingleton<SocketClient<WebSocketMessage>, SevenSocketClient>("7tv");

// hermes websocket
s.AddKeyedSingleton<IWebSocketHandler, HeartbeatHandler>("hermes");
s.AddKeyedSingleton<IWebSocketHandler, LoginAckHandler>("hermes");
s.AddKeyedSingleton<IWebSocketHandler, RequestAckHandler>("hermes");
//s.AddKeyedSingleton<IWebSocketHandler, HeartbeatHandler>("hermes");

s.AddKeyedSingleton<MessageTypeManager<IWebSocketHandler>, HermesMessageTypeManager>("hermes");
s.AddKeyedSingleton<SocketClient<WebSocketMessage>, HermesSocketClient>("hermes");

s.AddHostedService<TTS>();
using IHost host = builder.Build();
await host.RunAsync();