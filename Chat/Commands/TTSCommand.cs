using Serilog;
using TwitchChatTTS.Hermes.Socket;
using TwitchChatTTS.Twitch.Socket.Messages;
using static TwitchChatTTS.Chat.Commands.TTSCommands;

namespace TwitchChatTTS.Chat.Commands
{
    public class TTSCommand : IChatCommand
    {
        private readonly User _user;
        private readonly ILogger _logger;


        public TTSCommand(User user, ILogger logger)
        {
            _user = user;
            _logger = logger;
        }

        public string Name => "tts";

        public void Build(ICommandBuilder builder)
        {
            builder.CreateCommandTree(Name, b =>
            {
                b.CreateStaticInputParameter("add", b =>
                {
                    b.CreateVoiceNameParameter("voiceName", false)
                        .CreateCommand(new AddTTSVoiceCommand(_user, _logger));
                })
                .AddAlias("insert", "add")
                .CreateStaticInputParameter("delete", b =>
                {
                    b.CreateVoiceNameParameter("voiceName", true)
                        .CreateCommand(new DeleteTTSVoiceCommand(_user, _logger));
                })
                .AddAlias("del", "delete")
                .AddAlias("remove", "delete")
                .CreateStaticInputParameter("enable", b =>
                {
                    b.CreateVoiceNameParameter("voiceName", false)
                        .CreateCommand(new SetTTSVoiceStateCommand(true, _user, _logger));
                })
                .AddAlias("on", "enable")
                .AddAlias("enabled", "enable")
                .AddAlias("true", "enable")
                .CreateStaticInputParameter("disable", b =>
                {
                    b.CreateVoiceNameParameter("voiceName", true)
                        .CreateCommand(new SetTTSVoiceStateCommand(false, _user, _logger));
                })
                .AddAlias("off", "disable")
                .AddAlias("disabled", "disable")
                .AddAlias("false", "disable");
            });
        }

        private sealed class AddTTSVoiceCommand : IChatPartialCommand
        {
            private readonly User _user;
            private readonly ILogger _logger;

            public bool AcceptCustomPermission { get => false; }


            public AddTTSVoiceCommand(User user, ILogger logger)
            {
                _user = user;
                _logger = logger;
            }

            public async Task Execute(IDictionary<string, string> values, ChannelChatMessage message, HermesSocketClient client)
            {
                if (_user == null || _user.VoicesAvailable == null)
                    return;

                var voiceName = values["voiceName"];
                var voiceNameLower = voiceName.ToLower();
                var exists = _user.VoicesAvailable.Any(v => v.Value.ToLower() == voiceNameLower);
                if (exists)
                {
                    _logger.Warning($"Voice already exists [voice: {voiceName}][id: {message.ChatterUserId}]");
                    return;
                }

                await client.CreateTTSVoice(voiceName);
                _logger.Information($"Added a new TTS voice [voice: {voiceName}][creator: {message.ChatterUserLogin}][creator id: {message.ChatterUserId}]");
            }
        }

        private sealed class DeleteTTSVoiceCommand : IChatPartialCommand
        {
            private readonly User _user;
            private ILogger _logger;

            public bool AcceptCustomPermission { get => false; }

            public DeleteTTSVoiceCommand(User user, ILogger logger)
            {
                _user = user;
                _logger = logger;
            }

            public async Task Execute(IDictionary<string, string> values, ChannelChatMessage message, HermesSocketClient client)
            {
                if (_user == null || _user.VoicesAvailable == null)
                {
                    _logger.Warning($"Voices available are not loaded [chatter: {message.ChatterUserLogin}][chatter id: {message.ChatterUserId}]");
                    return;
                }

                var voiceName = values["voiceName"];
                var voiceNameLower = voiceName.ToLower();
                var exists = _user.VoicesAvailable.Any(v => v.Value.ToLower() == voiceNameLower);
                if (!exists)
                {
                    _logger.Warning($"Voice does not exist [voice: {voiceName}][chatter: {message.ChatterUserLogin}][chatter id: {message.ChatterUserId}]");
                    return;
                }

                var voiceId = _user.VoicesAvailable.FirstOrDefault(v => v.Value.ToLower() == voiceNameLower).Key;
                if (voiceId == null) {
                    _logger.Warning($"Could not find the identifier for the tts voice [voice name: {voiceName}]");
                    return;
                }

                await client.DeleteTTSVoice(voiceId);
                _logger.Information($"Deleted a TTS voice [voice: {voiceName}][chatter: {message.ChatterUserLogin}][chatter id: {message.ChatterUserId}]");
            }
        }

        private sealed class SetTTSVoiceStateCommand : IChatPartialCommand
        {
            private bool _state;
            private readonly User _user;
            private ILogger _logger;

            public bool AcceptCustomPermission { get => true; }

            public SetTTSVoiceStateCommand(bool state, User user, ILogger logger)
            {
                _state = state;
                _user = user;
                _logger = logger;
            }

            public async Task Execute(IDictionary<string, string> values, ChannelChatMessage message, HermesSocketClient client)
            {
                if (_user == null || _user.VoicesAvailable == null)
                    return;

                var voiceName = values["voiceName"];
                var voiceNameLower = voiceName.ToLower();
                var voiceId = _user.VoicesAvailable.FirstOrDefault(v => v.Value.ToLower() == voiceNameLower).Key;

                await client.UpdateTTSVoiceState(voiceId, _state);
                _logger.Information($"Changed state for TTS voice [voice: {voiceName}][state: {_state}][invoker: {message.ChatterUserLogin}][id: {message.ChatterUserId}]");
            }
        }
    }
}