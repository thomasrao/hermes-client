using Serilog;
using TwitchChatTTS.Chat.Soeech;
using TwitchChatTTS.Hermes.Socket;
using TwitchChatTTS.Twitch.Socket;
using TwitchChatTTS.Twitch.Socket.Messages;
using static TwitchChatTTS.Chat.Commands.TTSCommands;

namespace TwitchChatTTS.Chat.Commands
{
    public class SkipCommand : IChatCommand
    {
        private readonly TTSPlayer _player;
        private readonly AudioPlaybackEngine _playback;
        private readonly ILogger _logger;

        public SkipCommand(TTSPlayer player, AudioPlaybackEngine playback, ILogger logger)
        {
            _player = player;
            _playback = playback;
            _logger = logger;
        }

        public string Name => "skip";

        public void Build(ICommandBuilder builder)
        {
            builder.CreateCommandTree(Name, b =>
            {
                b.CreateStaticInputParameter("all", b =>
                {
                    b.CreateCommand(new TTSPlayerSkipAllCommand(_player, _playback, _logger));
                }).CreateCommand(new TTSPlayerSkipCommand(_player, _playback, _logger));
            });
            builder.CreateCommandTree("skipall", b =>
            {
                b.CreateCommand(new TTSPlayerSkipAllCommand(_player, _playback, _logger));
            });
        }


        private sealed class TTSPlayerSkipCommand : IChatPartialCommand
        {
            private readonly TTSPlayer _player;
            private readonly AudioPlaybackEngine _playback;
            private readonly ILogger _logger;

            public bool AcceptCustomPermission { get => true; }

            public TTSPlayerSkipCommand(TTSPlayer player, AudioPlaybackEngine playback, ILogger logger)
            {
                _player = player;
                _playback = playback;
                _logger = logger;
            }

            public async Task Execute(IDictionary<string, string> values, ChannelChatMessage message, HermesSocketClient hermes)
            {
                if (_player.Playing == null)
                    return;

                _playback.RemoveMixerInput(_player.Playing.Audio!);
                _player.Playing = null;

                _logger.Information("Skipped current tts.");
            }
        }

        private sealed class TTSPlayerSkipAllCommand : IChatPartialCommand
        {
            private readonly TTSPlayer _player;
            private readonly AudioPlaybackEngine _playback;
            private readonly ILogger _logger;

            public bool AcceptCustomPermission { get => true; }

            public TTSPlayerSkipAllCommand(TTSPlayer player, AudioPlaybackEngine playback, ILogger logger)
            {
                _player = player;
                _playback = playback;
                _logger = logger;
            }

            public async Task Execute(IDictionary<string, string> values, ChannelChatMessage message, HermesSocketClient hermes)
            {
                _player.RemoveAll();

                if (_player.Playing == null)
                    return;

                _playback.RemoveMixerInput(_player.Playing.Audio!);
                _player.Playing = null;

                _logger.Information("Skipped all queued and playing tts.");
            }
        }
    }
}