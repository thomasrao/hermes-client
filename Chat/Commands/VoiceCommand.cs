using Microsoft.Extensions.DependencyInjection;
using Serilog;
using TwitchChatTTS.Chat.Commands.Parameters;
using TwitchChatTTS.Hermes.Socket;
using TwitchLib.Client.Models;

namespace TwitchChatTTS.Chat.Commands
{
    public class VoiceCommand : ChatCommand
    {
        private readonly User _user;
        private readonly ILogger _logger;

        public VoiceCommand(
            [FromKeyedServices("parameter-ttsvoicename")] ChatCommandParameter ttsVoiceParameter,
            User user,
            ILogger logger
        ) : base("voice", "Select a TTS voice as the default for that user.")
        {
            _user = user;
            _logger = logger;

            AddParameter(ttsVoiceParameter);
        }

        public override async Task<bool> CheckDefaultPermissions(ChatMessage message)
        {
            return message.IsModerator || message.IsBroadcaster || message.IsSubscriber || message.Bits >= 100;
        }

        public override async Task Execute(IList<string> args, ChatMessage message, HermesSocketClient client)
        {
            if (_user == null || _user.VoicesSelected == null || _user.VoicesEnabled == null)
                return;

            long chatterId = long.Parse(message.UserId);
            var voiceName = args.First().ToLower();
            var voice = _user.VoicesAvailable.First(v => v.Value.ToLower() == voiceName);
            var enabled = _user.VoicesEnabled.Contains(voice.Value);

            if (!enabled)
            {
                _logger.Information($"Voice is disabled. Cannot switch to that voice [voice: {voice.Value}][username: {message.Username}]");
                return;
            }

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