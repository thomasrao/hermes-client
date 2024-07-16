using Microsoft.Extensions.DependencyInjection;
using Serilog;
using TwitchChatTTS.Chat.Commands.Parameters;
using TwitchChatTTS.Hermes.Socket;
using TwitchLib.Client.Models;

namespace TwitchChatTTS.Chat.Commands
{
    public class AddTTSVoiceCommand : ChatCommand
    {
        private readonly User _user;
        private readonly ILogger _logger;

        public new bool DefaultPermissionsOverwrite { get => true; }

        public AddTTSVoiceCommand(
            User user,
            [FromKeyedServices("parameter-unvalidated")] ChatCommandParameter unvalidatedParameter,
            ILogger logger
        ) : base("addttsvoice", "Select a TTS voice as the default for that user.")
        {
            _user = user;
            _logger = logger;

            AddParameter(unvalidatedParameter);
        }

        public override async Task<bool> CheckDefaultPermissions(ChatMessage message)
        {
            return false;
        }

        public override async Task Execute(IList<string> args, ChatMessage message, HermesSocketClient client)
        {
            if (_user == null || _user.VoicesAvailable == null)
                return;

            var voiceName = args.First();
            var voiceNameLower = voiceName.ToLower();
            var exists = _user.VoicesAvailable.Any(v => v.Value.ToLower() == voiceNameLower);
            if (exists) {
                _logger.Information("Voice already exists.");
                return;
            }

            await client.CreateTTSVoice(voiceName);
            _logger.Information($"Added a new TTS voice by {message.Username} [voice: {voiceName}][id: {message.UserId}]");
        }
    }
}