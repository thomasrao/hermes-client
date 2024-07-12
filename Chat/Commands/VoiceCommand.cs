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
        private readonly User _user;
        private readonly SocketClient<WebSocketMessage> _hermesClient;
        private readonly ILogger _logger;

        public VoiceCommand(
            [FromKeyedServices("parameter-ttsvoicename")] ChatCommandParameter ttsVoiceParameter,
            User user,
            [FromKeyedServices("hermes")] SocketClient<WebSocketMessage> hermesClient,
            ILogger logger
        ) : base("voice", "Select a TTS voice as the default for that user.")
        {
            _user = user;
            _hermesClient = hermesClient;
            _logger = logger;

            AddParameter(ttsVoiceParameter);
        }

        public override async Task<bool> CheckDefaultPermissions(ChatMessage message, long broadcasterId)
        {
            return message.IsModerator || message.IsBroadcaster || message.IsSubscriber || message.Bits >= 100;
        }

        public override async Task Execute(IList<string> args, ChatMessage message, long broadcasterId)
        {
            if (_user == null || _user.VoicesSelected == null || _user.VoicesEnabled == null)
                return;

            long chatterId = long.Parse(message.UserId);
            var voiceName = args.First().ToLower();
            var voice = _user.VoicesAvailable.First(v => v.Value.ToLower() == voiceName);
            var enabled = _user.VoicesEnabled.Contains(voice.Value);

            if (enabled)
            {
                await _hermesClient.Send(3, new RequestMessage()
                {
                    Type = _user.VoicesSelected.ContainsKey(chatterId) ? "update_tts_user" : "create_tts_user",
                    Data = new Dictionary<string, object>() { { "chatter", chatterId }, { "voice", voice.Key } }
                });
                _logger.Debug($"Sent request to update chat TTS voice [voice: {voice.Value}][username: {message.Username}].");
            }
        }
    }
}