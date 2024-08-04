using Serilog;
using TwitchChatTTS.Hermes.Socket;
using TwitchChatTTS.Twitch.Socket.Messages;
using static TwitchChatTTS.Chat.Commands.TTSCommands;

namespace TwitchChatTTS.Chat.Commands
{
    public class VoiceCommand : IChatCommand
    {
        private readonly User _user;
        // TODO: get permissions
        // TODO: validated parameter for username by including '@' and regex for username
        private readonly ILogger _logger;

        public VoiceCommand(User user, ILogger logger)
        {
            _user = user;
            _logger = logger;
        }

        public string Name => "voice";

        public void Build(ICommandBuilder builder)
        {
            builder.CreateCommandTree(Name, b =>
            {
                b.CreateVoiceNameParameter("voiceName", true)
                    .CreateCommand(new TTSVoiceSelector(_user, _logger))
                    .CreateUnvalidatedParameter("chatter", optional: true)
                    .AddPermission("tts.command.voice.admin")
                    .CreateCommand(new TTSVoiceSelectorAdmin(_user, _logger));
            });
        }

        private sealed class TTSVoiceSelector : IChatPartialCommand
        {
            private readonly User _user;
            private readonly ILogger _logger;

            public bool AcceptCustomPermission { get => true; }

            public TTSVoiceSelector(User user, ILogger logger)
            {
                _user = user;
                _logger = logger;
            }

            public async Task Execute(IDictionary<string, string> values, ChannelChatMessage message, HermesSocketClient client)
            {
                if (_user == null || _user.VoicesSelected == null)
                    return;

                long chatterId = long.Parse(message.ChatterUserId);
                var voiceName = values["voiceName"];
                var voiceNameLower = voiceName.ToLower();
                var voice = _user.VoicesAvailable.First(v => v.Value.ToLower() == voiceNameLower);

                if (_user.VoicesSelected.ContainsKey(chatterId))
                {
                    await client.UpdateTTSUser(chatterId, voice.Key);
                    _logger.Debug($"Sent request to create chat TTS voice [voice: {voice.Value}][username: {message.ChatterUserLogin}][reason: command]");
                }
                else
                {
                    await client.CreateTTSUser(chatterId, voice.Key);
                    _logger.Debug($"Sent request to update chat TTS voice [voice: {voice.Value}][username: {message.ChatterUserLogin}][reason: command]");
                }
            }
        }

        private sealed class TTSVoiceSelectorAdmin : IChatPartialCommand
        {
            private readonly User _user;
            private readonly ILogger _logger;

            public bool AcceptCustomPermission { get => true; }

            public TTSVoiceSelectorAdmin(User user, ILogger logger)
            {
                _user = user;
                _logger = logger;
            }

            public async Task Execute(IDictionary<string, string> values, ChannelChatMessage message, HermesSocketClient client)
            {
                if (_user == null || _user.VoicesSelected == null)
                    return;

                var chatterLogin = values["chatter"].Substring(1);
                var mention = message.Message.Fragments.FirstOrDefault(f => f.Mention != null && f.Mention.UserLogin == chatterLogin)?.Mention;
                if (mention == null)
                {
                    _logger.Warning("Failed to find the chatter to apply voice command to.");
                    return;
                }

                long chatterId = long.Parse(mention.UserId);
                var voiceName = values["voiceName"];
                var voiceNameLower = voiceName.ToLower();
                var voice = _user.VoicesAvailable.First(v => v.Value.ToLower() == voiceNameLower);

                if (_user.VoicesSelected.ContainsKey(chatterId))
                {
                    await client.UpdateTTSUser(chatterId, voice.Key);
                    _logger.Debug($"Sent request to create chat TTS voice [voice: {voice.Value}][username: {mention.UserLogin}][reason: command]");
                }
                else
                {
                    await client.CreateTTSUser(chatterId, voice.Key);
                    _logger.Debug($"Sent request to update chat TTS voice [voice: {voice.Value}][username: {mention.UserLogin}][reason: command]");
                }
            }
        }
    }
}