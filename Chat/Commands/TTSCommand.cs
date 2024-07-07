using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;
using HermesSocketLibrary.Socket.Data;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using TwitchChatTTS.Chat.Commands.Parameters;
using TwitchLib.Client.Models;

namespace TwitchChatTTS.Chat.Commands
{
    public class TTSCommand : ChatCommand
    {
        private readonly User _user;
        private readonly SocketClient<WebSocketMessage> _hermesClient;
        private readonly ILogger _logger;

        public TTSCommand(
            [FromKeyedServices("parameter-ttsvoicename")] ChatCommandParameter ttsVoiceParameter,
            [FromKeyedServices("parameter-unvalidated")] ChatCommandParameter unvalidatedParameter,
            User user,
            [FromKeyedServices("hermes")] SocketClient<WebSocketMessage> hermesClient,
            ILogger logger
        ) : base("tts", "Various tts commands.")
        {
            _user = user;
            _hermesClient = hermesClient;
            _logger = logger;

            AddParameter(ttsVoiceParameter);
            AddParameter(unvalidatedParameter);
        }

        public override async Task<bool> CheckPermissions(ChatMessage message, long broadcasterId)
        {
            return message.IsModerator || message.IsBroadcaster;
        }

        public override async Task Execute(IList<string> args, ChatMessage message, long broadcasterId)
        {
            if (_user == null || _user.VoicesAvailable == null)
                return;

            var voiceName = args[0].ToLower();
            var voiceId = _user.VoicesAvailable.FirstOrDefault(v => v.Value.ToLower() == voiceName).Key;
            var action = args[1].ToLower();

            switch (action)
            {
                case "enable":
                    await _hermesClient.Send(3, new RequestMessage()
                    {
                        Type = "update_tts_voice_state",
                        Data = new Dictionary<string, object>() { { "voice", voiceId }, { "state", true } }
                    });
                    _logger.Information($"Enabled a TTS voice [voice: {voiceName}][invoker: {message.Username}][id: {message.UserId}]");
                    break;
                case "disable":
                    await _hermesClient.Send(3, new RequestMessage()
                    {
                        Type = "update_tts_voice_state",
                        Data = new Dictionary<string, object>() { { "voice", voiceId }, { "state", false } }
                    });
                    _logger.Information($"Disabled a TTS voice [voice: {voiceName}][invoker: {message.Username}][id: {message.UserId}]");
                    break;
            }
        }
    }
}