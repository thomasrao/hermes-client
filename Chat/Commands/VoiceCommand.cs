using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;
using HermesSocketLibrary.Socket.Data;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using TwitchChatTTS.Chat.Commands.Parameters;
using TwitchLib.Client.Models;

namespace TwitchChatTTS.Chat.Commands
{
    public class VoiceCommand : ChatCommand
    {
        private IServiceProvider _serviceProvider;
        private ILogger _logger;

        public VoiceCommand(
            [FromKeyedServices("parameter-ttsvoicename")] ChatCommandParameter ttsVoiceParameter,
            IServiceProvider serviceProvider,
            ILogger logger
        ) : base("voice", "Select a TTS voice as the default for that user.")
        {
            _serviceProvider = serviceProvider;
            _logger = logger;

            AddParameter(ttsVoiceParameter);
        }

        public override async Task<bool> CheckPermissions(ChatMessage message, long broadcasterId)
        {
            return message.IsModerator || message.IsBroadcaster || message.IsSubscriber || message.Bits >= 100;
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

            await client.Send(3, new RequestMessage()
            {
                Type = context.VoicesSelected.ContainsKey(chatterId) ? "update_tts_user" : "create_tts_user",
                Data = new Dictionary<string, object>() { { "chatter", chatterId }, { "voice", voice.Key } }
            });
            _logger.Information($"Updated {message.Username}'s [id: {chatterId}] tts voice to {voice.Value} (id: {voice.Key}).");
        }
    }
}