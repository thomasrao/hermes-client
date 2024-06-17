using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;
using HermesSocketLibrary.Socket.Data;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using TwitchChatTTS.Chat.Commands.Parameters;
using TwitchLib.Client.Models;

namespace TwitchChatTTS.Chat.Commands
{
    public class OBSCommand : ChatCommand
    {
        private IServiceProvider _serviceProvider;
        private ILogger _logger;

        public OBSCommand(
            [FromKeyedServices("parameter-unvalidated")] ChatCommandParameter unvalidatedParameter,
            IServiceProvider serviceProvider,
            ILogger logger
        ) : base("obs", "Various obs commands.")
        {
            _serviceProvider = serviceProvider;
            _logger = logger;

            AddParameter(unvalidatedParameter);
        }

        public override async Task<bool> CheckPermissions(ChatMessage message, long broadcasterId)
        {
            return message.IsModerator || message.IsBroadcaster;
        }

        public override async Task Execute(IList<string> args, ChatMessage message, long broadcasterId)
        {
            var client = _serviceProvider.GetRequiredKeyedService<SocketClient<WebSocketMessage>>("obs");
            if (client == null)
                return;
            var context = _serviceProvider.GetRequiredService<User>();
            if (context == null || context.VoicesAvailable == null)
                return;

            var voiceName = args[0].ToLower();
            var voiceId = context.VoicesAvailable.FirstOrDefault(v => v.Value.ToLower() == voiceName).Key;
            var action = args[1].ToLower();

            switch (action) {
                case "sleep":
                    await client.Send(8, new RequestMessage()
                    {
                        Type = "Sleep",
                        Data = new Dictionary<string, object>() { { "requestId", "siduhsidasd" }, { "sleepMillis", 10000 } }
                    });
                break;
                case "get_scene_item_id":
                    await client.Send(6, new RequestMessage()
                    {
                        Type = "GetSceneItemId",
                        Data = new Dictionary<string, object>() { { "sceneName", "Generic" }, { "sourceName", "ABCDEF" }, { "rotation", 90 } }
                    });
                break;
                case "transform":
                    await client.Send(6, new RequestMessage()
                    {
                        Type = "Transform",
                        Data = new Dictionary<string, object>() { { "sceneName", "Generic" }, { "sceneItemId", 90 }, { "rotation", 90 } }
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