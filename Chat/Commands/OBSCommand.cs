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

        public override async Task<bool> CheckDefaultPermissions(ChatMessage message, long broadcasterId)
        {
            return message.IsModerator || message.IsBroadcaster;
        }

        public override async Task Execute(IList<string> args, ChatMessage message, long broadcasterId)
        {
            if (_user == null || _user.VoicesAvailable == null)
                return;

            var action = args[0].ToLower();

            switch (action)
            {
                case "get_scene_item_id":
                    if (args.Count < 3)
                        return;

                    _logger.Debug($"Getting scene item id via chat command [args: {string.Join(" ", args)}]");
                    await _manager.Send(new RequestMessage("GetSceneItemId", string.Empty, new Dictionary<string, object>() { { "sceneName", args[1] }, { "sourceName", args[2] } }));
                    break;
                case "transform":
                    if (args.Count < 5)
                        return;

                    _logger.Debug($"Getting scene item transformation data via chat command [args: {string.Join(" ", args)}]");
                    await _manager.UpdateTransformation(args[1], args[2], (d) =>
                    {
                        if (args[3].ToLower() == "rotation")
                            d.Rotation = int.Parse(args[4]);
                        else if (args[3].ToLower() == "x")
                            d.Rotation = int.Parse(args[4]);
                        else if (args[3].ToLower() == "y")
                            d.PositionY = int.Parse(args[4]);
                    });
                    break;
                case "sleep":
                    if (args.Count < 2)
                        return;

                    _logger.Debug($"Sending OBS to sleep via chat command [args: {string.Join(" ", args)}]");
                    await _manager.Send(new RequestMessage("Sleep", string.Empty, new Dictionary<string, object>() { { "sleepMillis", int.Parse(args[1]) } }));
                    break;
                case "visibility":
                    if (args.Count < 4)
                        return;
                        
                    _logger.Debug($"Updating scene item visibility via chat command [args: {string.Join(" ", args)}]");
                    await _manager.UpdateSceneItemVisibility(args[1], args[2], args[3].ToLower() == "true");
                    break;
                default:
                    break;
            }
        }
    }
}