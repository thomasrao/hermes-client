using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;
using HermesSocketLibrary.Socket.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TwitchChatTTS.Chat.Commands.Parameters;
using TwitchLib.Client.Models;

namespace TwitchChatTTS.Chat.Commands
{
    public class AddTTSVoiceCommand : ChatCommand
    {
        private IServiceProvider _serviceProvider;
        private ILogger<AddTTSVoiceCommand> _logger;

        public AddTTSVoiceCommand(
            [FromKeyedServices("parameter-unvalidated")] ChatCommandParameter ttsVoiceParameter,
            IServiceProvider serviceProvider,
            ILogger<AddTTSVoiceCommand> logger
        ) : base("addttsvoice", "Select a TTS voice as the default for that user.") {
            _serviceProvider = serviceProvider;
            _logger = logger;

            AddParameter(ttsVoiceParameter);
        }

        public override async Task<bool> CheckPermissions(ChatMessage message, long broadcasterId)
        {
            return message.IsModerator || message.IsBroadcaster || message.UserId == "126224566";
        }

        public override async Task Execute(IList<string> args, ChatMessage message, long broadcasterId)
        {
            var client = _serviceProvider.GetRequiredKeyedService<SocketClient<WebSocketMessage>>("hermes");
            if (client == null)
                return;
            var context = _serviceProvider.GetRequiredService<User>();
            if (context == null || context.VoicesAvailable == null)
                return;

            var voiceName = args.First();
            var voiceNameLower = voiceName.ToLower();
            var exists = context.VoicesAvailable.Any(v => v.Value.ToLower() == voiceNameLower);
            if (exists)
                return;
            
            await client.Send(3, new RequestMessage() {
                Type = "create_tts_voice",
                Data = new Dictionary<string, string>() { { "@voice", voiceName } }
            });
            _logger.LogInformation($"Added a new TTS voice by {message.Username} (id: {message.UserId}): {voiceName}.");
        }
    }
}