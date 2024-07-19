using Serilog;
using TwitchChatTTS.Hermes.Socket;
using TwitchLib.Client.Models;
using static TwitchChatTTS.Chat.Commands.TTSCommands;

namespace TwitchChatTTS.Chat.Commands
{
    public class SkipCommand : IChatCommand
    {
        private readonly TTSPlayer _player;
        private readonly ILogger _logger;

        public SkipCommand(TTSPlayer ttsPlayer, ILogger logger)
        {
            _player = ttsPlayer;
            _logger = logger;
        }

        public string Name => "skip";

        public void Build(ICommandBuilder builder)
        {
            builder.CreateCommandTree(Name, b =>
            {
                b.CreateStaticInputParameter("all", b =>
                {
                    b.CreateCommand(new TTSPlayerSkipAllCommand(_player, _logger));
                }).CreateCommand(new TTSPlayerSkipCommand(_player, _logger));
            });
            builder.CreateCommandTree("skipall", b => {
                b.CreateCommand(new TTSPlayerSkipAllCommand(_player, _logger));
            });
        }


        private sealed class TTSPlayerSkipCommand : IChatPartialCommand
        {
            private readonly TTSPlayer _ttsPlayer;
            private readonly ILogger _logger;

            public bool AcceptCustomPermission { get => true; }

            public TTSPlayerSkipCommand(TTSPlayer ttsPlayer, ILogger logger)
            {
                _ttsPlayer = ttsPlayer;
                _logger = logger;
            }

            public bool CheckDefaultPermissions(ChatMessage message)
            {
                return message.IsModerator || message.IsVip || message.IsBroadcaster;
            }

            public async Task Execute(IDictionary<string, string> values, ChatMessage message, HermesSocketClient client)
            {
                if (_ttsPlayer.Playing == null)
                    return;

                AudioPlaybackEngine.Instance.RemoveMixerInput(_ttsPlayer.Playing);
                _ttsPlayer.Playing = null;

                _logger.Information("Skipped current tts.");
            }
        }

        private sealed class TTSPlayerSkipAllCommand : IChatPartialCommand
        {
            private readonly TTSPlayer _ttsPlayer;
            private readonly ILogger _logger;

            public bool AcceptCustomPermission { get => true; }

            public TTSPlayerSkipAllCommand(TTSPlayer ttsPlayer, ILogger logger)
            {
                _ttsPlayer = ttsPlayer;
                _logger = logger;
            }

            public bool CheckDefaultPermissions(ChatMessage message)
            {
                return message.IsModerator || message.IsVip || message.IsBroadcaster;
            }

            public async Task Execute(IDictionary<string, string> values, ChatMessage message, HermesSocketClient client)
            {
                _ttsPlayer.RemoveAll();

                if (_ttsPlayer.Playing == null)
                    return;

                AudioPlaybackEngine.Instance.RemoveMixerInput(_ttsPlayer.Playing);
                _ttsPlayer.Playing = null;

                _logger.Information("Skipped all queued and playing tts.");
            }
        }
    }
}