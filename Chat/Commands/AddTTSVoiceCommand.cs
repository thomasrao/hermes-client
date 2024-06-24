using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;
using HermesSocketLibrary.Socket.Data;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using TwitchChatTTS.Chat.Commands.Parameters;
using TwitchLib.Client.Models;

namespace TwitchChatTTS.Chat.Commands
{
    public class AddTTSVoiceCommand : ChatCommand
    {
        private readonly User _user;
        private readonly SocketClient<WebSocketMessage> _hermesClient;
        private readonly ILogger _logger;

        public AddTTSVoiceCommand(
            User user,
            [FromKeyedServices("parameter-unvalidated")] ChatCommandParameter ttsVoiceParameter,
            [FromKeyedServices("hermes")] SocketClient<WebSocketMessage> hermesClient,
            ILogger logger
        ) : base("addttsvoice", "Select a TTS voice as the default for that user.")
        {
            _user = user;
            _hermesClient = hermesClient;
            _logger = logger;

            AddParameter(ttsVoiceParameter);
        }

        public override async Task<bool> CheckPermissions(ChatMessage message, long broadcasterId)
        {
            return message.IsModerator || message.IsBroadcaster;
        }

        public override async Task Execute(IList<string> args, ChatMessage message, long broadcasterId)
        {
            //var HermesClient = _serviceProvider.GetRequiredKeyedService<SocketClient<WebSocketMessage>>("hermes");
            if (_hermesClient == null)
                return;
            if (_user == null || _user.VoicesAvailable == null)
                return;

            var voiceName = args.First();
            var voiceNameLower = voiceName.ToLower();
            var exists = _user.VoicesAvailable.Any(v => v.Value.ToLower() == voiceNameLower);
            if (exists)
                return;

            await _hermesClient.Send(3, new RequestMessage()
            {
                Type = "create_tts_voice",
                Data = new Dictionary<string, object>() { { "voice", voiceName } }
            });
            _logger.Information($"Added a new TTS voice by {message.Username} [voice: {voiceName}][id: {message.UserId}]");
        }
    }
}