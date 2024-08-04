using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using TwitchChatTTS.Hermes.Socket;
using TwitchChatTTS.OBS.Socket;
using TwitchChatTTS.Twitch.Socket.Messages;
using static TwitchChatTTS.Chat.Commands.TTSCommands;

namespace TwitchChatTTS.Chat.Commands
{
    public class RefreshCommand : IChatCommand
    {
        private readonly OBSSocketClient _obs;
        private readonly ILogger _logger;

        public string Name => "refresh";

        public RefreshCommand(
            [FromKeyedServices("obs")] SocketClient<WebSocketMessage> obs,
            ILogger logger
        )
        {
            _obs = (obs as OBSSocketClient)!;
            _logger = logger;
        }


        public void Build(ICommandBuilder builder)
        {
            builder.CreateCommandTree(Name, b =>
            {
                b.CreateStaticInputParameter("tts_voice_enabled", b => b.CreateCommand(new RefreshTTSVoicesEnabled()))
                    .CreateStaticInputParameter("word_filters", b => b.CreateCommand(new RefreshTTSWordFilters()))
                    .CreateStaticInputParameter("selected_voices", b => b.CreateCommand(new RefreshTTSChatterVoices()))
                    .CreateStaticInputParameter("default_voice", b => b.CreateCommand(new RefreshTTSDefaultVoice()))
                    .CreateStaticInputParameter("redemptions", b => b.CreateCommand(new RefreshRedemptions()))
                    .CreateStaticInputParameter("obs_cache", b => b.CreateCommand(new RefreshObs(_obs, _logger)))
                    .CreateStaticInputParameter("permissions", b => b.CreateCommand(new RefreshPermissions()));
            });
        }

        private sealed class RefreshTTSVoicesEnabled : IChatPartialCommand
        {
            public bool AcceptCustomPermission { get => true; }

            public async Task Execute(IDictionary<string, string> values, ChannelChatMessage message, HermesSocketClient client)
            {
                await client.FetchEnabledTTSVoices();
            }
        }

        private sealed class RefreshTTSWordFilters : IChatPartialCommand
        {
            public bool AcceptCustomPermission { get => true; }

            public async Task Execute(IDictionary<string, string> values, ChannelChatMessage message, HermesSocketClient client)
            {
                await client.FetchTTSWordFilters();
            }
        }

        private sealed class RefreshTTSChatterVoices : IChatPartialCommand
        {
            public bool AcceptCustomPermission { get => true; }

            public async Task Execute(IDictionary<string, string> values, ChannelChatMessage message, HermesSocketClient client)
            {
                await client.FetchTTSChatterVoices();
            }
        }

        private sealed class RefreshTTSDefaultVoice : IChatPartialCommand
        {
            public bool AcceptCustomPermission { get => true; }

            public async Task Execute(IDictionary<string, string> values, ChannelChatMessage message, HermesSocketClient client)
            {
                await client.FetchDefaultTTSVoice();
            }
        }

        private sealed class RefreshRedemptions : IChatPartialCommand
        {
            public bool AcceptCustomPermission { get => true; }

            public async Task Execute(IDictionary<string, string> values, ChannelChatMessage message, HermesSocketClient client)
            {
                await client.FetchRedemptions();
            }
        }

        private sealed class RefreshObs : IChatPartialCommand
        {
            private readonly OBSSocketClient _obsManager;
            private readonly ILogger _logger;

            public bool AcceptCustomPermission { get => true; }

            public RefreshObs(OBSSocketClient obsManager, ILogger logger) {
                _obsManager = obsManager;
                _logger = logger;
            }

            public async Task Execute(IDictionary<string, string> values, ChannelChatMessage message, HermesSocketClient client)
            {
                _obsManager.ClearCache();
                _logger.Information("Cleared the cache used for OBS.");
            }
        }

        private sealed class RefreshPermissions : IChatPartialCommand
        {

            public bool AcceptCustomPermission { get => true; }

            public async Task Execute(IDictionary<string, string> values, ChannelChatMessage message, HermesSocketClient client)
            {
                await client.FetchPermissions();
            }
        }
    }
}