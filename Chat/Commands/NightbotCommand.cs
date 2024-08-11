using Serilog;
using TwitchChatTTS.Hermes.Socket;
using TwitchChatTTS.Twitch.Socket.Messages;
using static TwitchChatTTS.Chat.Commands.TTSCommands;

namespace TwitchChatTTS.Chat.Commands
{
    public class NightbotCommand : IChatCommand
    {
        private readonly NightbotApiClient _api;
        private readonly ILogger _logger;

        public NightbotCommand(NightbotApiClient api, ILogger logger)
        {
            _api = api;
            _logger = logger;
        }

        public string Name => "nightbot";

        public void Build(ICommandBuilder builder)
        {
            builder.CreateCommandTree(Name, b =>
            {
                b.CreateStaticInputParameter("play", b =>
                {
                    b.CreateCommand(new NightbotSongQueueCommand(_api, "play", _logger));
                })
                .CreateStaticInputParameter("pause", b =>
                {
                    b.CreateCommand(new NightbotSongQueueCommand(_api, "pause", _logger));
                })
                .CreateStaticInputParameter("skip", b =>
                {
                    b.CreateCommand(new NightbotSongQueueCommand(_api, "skip", _logger));
                })
                .CreateStaticInputParameter("volume", b =>
                {
                    b.CreateUnvalidatedParameter("volume")
                        .CreateCommand(new NightbotSongQueueCommand(_api, "volume", _logger));
                })
                .CreateStaticInputParameter("clear_playlist", b =>
                {
                    b.CreateCommand(new NightbotSongQueueCommand(_api, "clear_playlist", _logger));
                })
                .CreateStaticInputParameter("clear_queue", b =>
                {
                    b.CreateCommand(new NightbotSongQueueCommand(_api, "volume", _logger));
                })
                .CreateStaticInputParameter("clear", b =>
                {
                    b.CreateStaticInputParameter("playlist", b => b.CreateCommand(new NightbotSongQueueCommand(_api, "clear_playlist", _logger)))
                        .CreateStaticInputParameter("queue", b => b.CreateCommand(new NightbotSongQueueCommand(_api, "clear_queue", _logger)));
                });
            });
        }

        private sealed class NightbotSongQueueCommand : IChatPartialCommand
        {
            private readonly NightbotApiClient _api;
            private readonly string _command;
            private readonly ILogger _logger;

            public bool AcceptCustomPermission { get => true; }


            public NightbotSongQueueCommand(NightbotApiClient api, string command, ILogger logger)
            {
                _api = api;
                _command = command.ToLower();
                _logger = logger;
            }

            public async Task Execute(IDictionary<string, string> values, ChannelChatMessage message, HermesSocketClient hermes)
            {
                try
                {
                    if (_command == "play")
                    {
                        await _api.Play();
                        _logger.Information("Playing Nightbot song queue.");
                    }
                    else if (_command == "pause")
                    {
                        await _api.Pause();
                        _logger.Information("Playing Nightbot song queue.");
                    }
                    else if (_command == "skip")
                    {
                        await _api.Skip();
                        _logger.Information("Skipping Nightbot song queue.");
                    }
                    else if (_command == "volume")
                    {
                        int volume = int.Parse(values["volume"]);
                        await _api.Volume(volume);
                        _logger.Information($"Changed Nightbot volume to {volume}.");
                    }
                    else if (_command == "clear_playlist")
                    {
                        await _api.ClearPlaylist();
                        _logger.Information("Cleared Nightbot playlist.");
                    }
                    else if (_command == "clear_queue")
                    {
                        await _api.ClearQueue();
                        _logger.Information("Cleared Nightbot queue.");
                    }
                }
                catch (HttpRequestException e)
                {
                    _logger.Warning("Ensure your Nightbot account is linked to your TTS account.");
                }
                catch (FormatException)
                {
                }
                catch (OverflowException)
                {
                }
            }
        }
    }
}