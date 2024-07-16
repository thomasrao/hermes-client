using Microsoft.Extensions.DependencyInjection;
using Serilog;
using TwitchChatTTS.Chat.Commands.Parameters;
using TwitchChatTTS.Hermes.Socket;
using TwitchLib.Client.Models;

namespace TwitchChatTTS.Chat.Commands
{
    public class TTSCommand : ChatCommand
    {
        private readonly User _user;
        private readonly ILogger _logger;

        public TTSCommand(
            [FromKeyedServices("parameter-ttsvoicename")] ChatCommandParameter ttsVoiceParameter,
            User user,
            ILogger logger
        ) : base("tts", "Various tts commands.")
        {
            _user = user;
            _logger = logger;

            AddParameter(ttsVoiceParameter);
            AddParameter(new SimpleListedParameter(["enable", "disable"]));
        }

        public override async Task<bool> CheckDefaultPermissions(ChatMessage message)
        {
            return message.IsModerator || message.IsBroadcaster;
        }

        public override async Task Execute(IList<string> args, ChatMessage message, HermesSocketClient client)
        {
            if (_user == null || _user.VoicesAvailable == null)
                return;

            var voiceName = args[0].ToLower();
            var voiceId = _user.VoicesAvailable.FirstOrDefault(v => v.Value.ToLower() == voiceName).Key;
            var action = args[1].ToLower();

            bool state = action == "enable";
            await client.UpdateTTSVoiceState(voiceId, state);
            _logger.Information($"Changed state for TTS voice [voice: {voiceName}][state: {state}][invoker: {message.Username}][id: {message.UserId}]");
        }
    }
}