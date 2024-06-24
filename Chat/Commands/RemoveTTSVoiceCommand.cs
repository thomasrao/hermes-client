using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;
using HermesSocketLibrary.Socket.Data;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using TwitchChatTTS.Chat.Commands.Parameters;
using TwitchLib.Client.Models;

namespace TwitchChatTTS.Chat.Commands
{
    public class RemoveTTSVoiceCommand : ChatCommand
    {
        private readonly User _user;
        private readonly SocketClient<WebSocketMessage> _hermesClient;
        private ILogger _logger;

        public RemoveTTSVoiceCommand(
            [FromKeyedServices("parameter-unvalidated")] ChatCommandParameter ttsVoiceParameter,
            User user,
            [FromKeyedServices("hermes")] SocketClient<WebSocketMessage> hermesClient,
            ILogger logger
        ) : base("removettsvoice", "Select a TTS voice as the default for that user.")
        {
            _user = user;
            _hermesClient = hermesClient;
            _logger = logger;

            AddParameter(ttsVoiceParameter);
        }

        public override async Task<bool> CheckPermissions(ChatMessage message, long broadcasterId)
        {
            return message.IsBroadcaster;
        }

        public override async Task Execute(IList<string> args, ChatMessage message, long broadcasterId)
        {
            if (_user == null || _user.VoicesAvailable == null)
                return;

            var voiceName = args.First().ToLower();
            var exists = _user.VoicesAvailable.Any(v => v.Value.ToLower() == voiceName);
            if (!exists)
                return;

            var voiceId = _user.VoicesAvailable.FirstOrDefault(v => v.Value.ToLower() == voiceName).Key;
            await _hermesClient.Send(3, new RequestMessage()
            {
                Type = "delete_tts_voice",
                Data = new Dictionary<string, object>() { { "voice", voiceId } }
            });
            _logger.Information($"Deleted a TTS voice [voice: {voiceName}][invoker: {message.Username}][id: {message.UserId}]");
        }
    }
}