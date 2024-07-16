using Microsoft.Extensions.DependencyInjection;
using Serilog;
using TwitchChatTTS.Chat.Commands.Parameters;
using TwitchChatTTS.Hermes.Socket;
using TwitchLib.Client.Models;

namespace TwitchChatTTS.Chat.Commands
{
    public class RemoveTTSVoiceCommand : ChatCommand
    {
        private readonly User _user;
        private ILogger _logger;

        public new bool DefaultPermissionsOverwrite { get => true; }

        public RemoveTTSVoiceCommand(
            [FromKeyedServices("parameter-unvalidated")] ChatCommandParameter ttsVoiceParameter,
            User user,
            ILogger logger
        ) : base("removettsvoice", "Select a TTS voice as the default for that user.")
        {
            _user = user;
            _logger = logger;

            AddParameter(ttsVoiceParameter);
        }

        public override async Task<bool> CheckDefaultPermissions(ChatMessage message)
        {
            return false;
        }

        public override async Task Execute(IList<string> args, ChatMessage message, HermesSocketClient client)
        {
            if (_user == null || _user.VoicesAvailable == null)
            {
                _logger.Debug($"Voices available are not loaded [chatter: {message.Username}][chatter id: {message.UserId}]");
                return;
            }

            var voiceName = args.First().ToLower();
            var exists = _user.VoicesAvailable.Any(v => v.Value.ToLower() == voiceName);
            if (!exists)
            {
                _logger.Debug($"Voice does not exist [voice: {voiceName}][chatter: {message.Username}][chatter id: {message.UserId}]");
                return;
            }

            var voiceId = _user.VoicesAvailable.FirstOrDefault(v => v.Value.ToLower() == voiceName).Key;
            await client.DeleteTTSVoice(voiceId);
            _logger.Information($"Deleted a TTS voice [voice: {voiceName}][chatter: {message.Username}][chatter id: {message.UserId}]");
        }
    }
}