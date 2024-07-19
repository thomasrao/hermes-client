using Serilog;
using TwitchChatTTS.Hermes.Socket;
using TwitchLib.Client.Models;
using static TwitchChatTTS.Chat.Commands.TTSCommands;

namespace TwitchChatTTS.Chat.Commands
{
    public class VoiceCommand : IChatCommand
    {
        private readonly User _user;
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
                    .CreateCommand(new TTSVoiceSelector(_user, _logger));
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


            public bool CheckDefaultPermissions(ChatMessage message)
            {
                return message.IsModerator || message.IsBroadcaster || message.IsSubscriber || message.Bits >= 100;
            }

            public async Task Execute(IDictionary<string, string> values, ChatMessage message, HermesSocketClient client)
            {
                if (_user == null || _user.VoicesSelected == null)
                    return;

                long chatterId = long.Parse(message.UserId);
                var voiceName = values["voiceName"];
                var voiceNameLower = voiceName.ToLower();
                var voice = _user.VoicesAvailable.First(v => v.Value.ToLower() == voiceNameLower);

                if (_user.VoicesSelected.ContainsKey(chatterId))
                {
                    await client.UpdateTTSUser(chatterId, voice.Key);
                    _logger.Debug($"Sent request to create chat TTS voice [voice: {voice.Value}][username: {message.Username}][reason: command]");
                }
                else
                {
                    await client.CreateTTSUser(chatterId, voice.Key);
                    _logger.Debug($"Sent request to update chat TTS voice [voice: {voice.Value}][username: {message.Username}][reason: command]");
                }
            }
        }
    }
}