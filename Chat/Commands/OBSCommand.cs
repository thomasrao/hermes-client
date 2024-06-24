using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using TwitchChatTTS.Chat.Commands.Parameters;
using TwitchChatTTS.OBS.Socket.Data;
using TwitchChatTTS.OBS.Socket.Manager;
using TwitchLib.Client.Models;

namespace TwitchChatTTS.Chat.Commands
{
    public class OBSCommand : ChatCommand
    {
        private readonly User _user;
        private readonly OBSManager _manager;
        private readonly ILogger _logger;

        public OBSCommand(
            [FromKeyedServices("parameter-unvalidated")] ChatCommandParameter unvalidatedParameter,
            User user,
            OBSManager manager,
            [FromKeyedServices("obs")] SocketClient<WebSocketMessage> hermesClient,
            ILogger logger
        ) : base("obs", "Various obs commands.")
        {
            _user = user;
            _manager = manager;
            _logger = logger;

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
                case "sleep":
                    await _manager.Send(new RequestMessage("Sleep", string.Empty, new Dictionary<string, object>() { { "sleepMillis", 10000 } }));
                    break;
                case "get_scene_item_id":
                    await _manager.Send(new RequestMessage("GetSceneItemId", string.Empty, new Dictionary<string, object>() { { "sceneName", "Generic" }, { "sourceName", "ABCDEF" }, { "rotation", 90 } }));
                    break;
                case "transform":
                    await _manager.UpdateTransformation(args[1], args[2], (d) =>
                    {

                    });
                    await _manager.Send(new RequestMessage("Transform", string.Empty, new Dictionary<string, object>() { { "sceneName", "Generic" }, { "sceneItemId", 90 }, { "rotation", 90 } }));
                    break;
                case "remove":
                    await _manager.Send(new RequestMessage("Sleep", string.Empty, new Dictionary<string, object>() { { "sleepMillis", 10000 } }));
                    break;
                default:
                    break;
            }
        }
    }
}