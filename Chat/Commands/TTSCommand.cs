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
        private IServiceProvider _serviceProvider;
        private ILogger _logger;

        public TTSCommand(
            [FromKeyedServices("parameter-ttsvoicename")] ChatCommandParameter ttsVoiceParameter,
            [FromKeyedServices("parameter-unvalidated")] ChatCommandParameter unvalidatedParameter,
            IServiceProvider serviceProvider,
            ILogger logger
        ) : base("tts", "Various tts commands.")
        {
            _serviceProvider = serviceProvider;
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
            var client = _serviceProvider.GetRequiredKeyedService<SocketClient<WebSocketMessage>>("hermes");
            if (client == null)
                return;
            var context = _serviceProvider.GetRequiredService<User>();
            if (context == null || context.VoicesAvailable == null)
                return;

            var voiceName = args[0].ToLower();
            var voiceId = context.VoicesAvailable.FirstOrDefault(v => v.Value.ToLower() == voiceName).Key;
            var action = args[1].ToLower();

            switch (action) {
                case "enable":
                    await client.Send(3, new RequestMessage()
                    {
                        Type = "update_tts_voice_state",
                        Data = new Dictionary<string, object>() { { "voice", voiceId }, { "state", true } }
                    });
                break;
                case "disable":
                    await client.Send(3, new RequestMessage()
                    {
                        Type = "update_tts_voice_state",
                        Data = new Dictionary<string, object>() { { "voice", voiceId }, { "state", false } }
                    });
                break;
                case "remove":
                    await client.Send(3, new RequestMessage()
                    {
                        Type = "delete_tts_voice",
                        Data = new Dictionary<string, object>() { { "voice", voiceId } }
                    });
                break;
            }

            
            _logger.Information($"Added a new TTS voice by {message.Username} (id: {message.UserId}): {voiceName}.");
        }
    }
}