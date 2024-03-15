using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;
using HermesSocketLibrary.Socket.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TwitchChatTTS.Chat.Commands.Parameters;
using TwitchLib.Client.Models;

namespace TwitchChatTTS.Chat.Commands
{
    public class VoiceCommand : ChatCommand
    {
        private IServiceProvider _serviceProvider;
        private ILogger<VoiceCommand> _logger;

        public VoiceCommand(
            [FromKeyedServices("parameter-ttsvoicename")] ChatCommandParameter ttsVoiceParameter,
            IServiceProvider serviceProvider,
            ILogger<VoiceCommand> logger
        ) : base("voice", "Select a TTS voice as the default for that user.") {
            _serviceProvider = serviceProvider;
            _logger = logger;

            AddParameter(ttsVoiceParameter);
        }

        public override async Task<bool> CheckPermissions(ChatMessage message, long broadcasterId)
        {
            return message.IsModerator || message.IsBroadcaster || message.IsSubscriber || message.Bits >= 100 || message.UserId == "126224566";
        }

        public override async Task Execute(IList<string> args, ChatMessage message, long broadcasterId)
        {
            var client = _serviceProvider.GetRequiredKeyedService<SocketClient<WebSocketMessage>>("hermes");
            if (client == null)
                return;
            var context = _serviceProvider.GetRequiredService<User>();
            if (context == null || context.VoicesSelected == null || context.VoicesAvailable == null)
                return;

            long chatterId = long.Parse(message.UserId);
            var voiceName = args.First().ToLower();
            var voice = context.VoicesAvailable.First(v => v.Value.ToLower() == voiceName);

            if (context.VoicesSelected.ContainsKey(chatterId)) {
                await client.Send(3, new RequestMessage() {
                    Type = "update_tts_user",
                    Data = new Dictionary<string, string>() { { "@user", message.UserId }, { "@broadcaster", broadcasterId.ToString() }, { "@voice", voice.Key } }
                });
                _logger.LogInformation($"Updated {message.Username}'s (id: {message.UserId}) tts voice to {voice.Value} (id: {voice.Key}).");
            } else {
                await client.Send(3, new RequestMessage() {
                    Type = "create_tts_user",
                    Data = new Dictionary<string, string>() { { "@user", message.UserId }, { "@broadcaster", broadcasterId.ToString() }, { "@voice", voice.Key } }
                });
                _logger.LogInformation($"Added {message.Username}'s (id: {message.UserId}) tts voice as {voice.Value} (id: {voice.Key}).");
            }
        }
    }
}