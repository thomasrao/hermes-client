using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using TwitchChatTTS.Hermes.Socket;
using TwitchChatTTS.OBS.Socket;
using TwitchChatTTS.OBS.Socket.Data;
using TwitchLib.Client.Models;
using static TwitchChatTTS.Chat.Commands.TTSCommands;

namespace TwitchChatTTS.Chat.Commands
{
    public class OBSCommand : IChatCommand
    {
        private readonly OBSSocketClient _obs;
        private readonly ILogger _logger;

        public string Name => "obs";

        public OBSCommand(
            [FromKeyedServices("obs")] SocketClient<WebSocketMessage> obs,
            ILogger logger
        )
        {
            _obs = (obs as OBSSocketClient)!;
            _logger = logger;
        }


        public void Build(ICommandBuilder builder)
        {
            builder.CreateCommandTree(Name, b =>
            {
                b.CreateStaticInputParameter("get_scene_item_id", b =>
                {
                    b.CreateUnvalidatedParameter("sceneName")
                        .CreateCommand(new OBSGetSceneItemId(_obs, _logger));
                })
                .CreateStaticInputParameter("transform", b =>
                {
                    b.CreateUnvalidatedParameter("sceneName")
                        .CreateUnvalidatedParameter("sourceName")
                        .CreateObsTransformationParameter("propertyName")
                        .CreateUnvalidatedParameter("value")
                        .CreateCommand(new OBSTransform(_obs, _logger));
                })
                .CreateStaticInputParameter("visibility", b =>
                {
                    b.CreateUnvalidatedParameter("sceneName")
                        .CreateUnvalidatedParameter("sourceName")
                        .CreateStateParameter("state")
                        .CreateCommand(new OBSVisibility(_obs, _logger));
                });
            });
        }

        private sealed class OBSGetSceneItemId : IChatPartialCommand
        {
            private readonly OBSSocketClient _obs;
            private readonly ILogger _logger;

            public string Name => "obs";
            public bool AcceptCustomPermission { get => true; }

            public OBSGetSceneItemId(
                [FromKeyedServices("obs")] SocketClient<WebSocketMessage> obs,
                ILogger logger
            )
            {
                _obs = (obs as OBSSocketClient)!;
                _logger = logger;
            }

            public bool CheckDefaultPermissions(ChatMessage message)
            {
                return message.IsModerator || message.IsBroadcaster;
            }

            public async Task Execute(IDictionary<string, string> values, ChatMessage message, HermesSocketClient client)
            {
                string sceneName = values["sceneName"];
                string sourceName = values["sourceName"];
                _logger.Debug($"Getting scene item id via chat command [scene name: {sceneName}][source name: {sourceName}]");
                await _obs.Send(new RequestMessage("GetSceneItemId", string.Empty, new Dictionary<string, object>() { { "sceneName", sceneName }, { "sourceName", sourceName } }));
            }
        }

        private sealed class OBSTransform : IChatPartialCommand
        {
            private readonly OBSSocketClient _obs;
            private readonly ILogger _logger;

            public string Name => "obs";
            public bool AcceptCustomPermission { get => true; }

            public OBSTransform(
                [FromKeyedServices("obs")] SocketClient<WebSocketMessage> obs,
                ILogger logger
            )
            {
                _obs = (obs as OBSSocketClient)!;
                _logger = logger;
            }

            public bool CheckDefaultPermissions(ChatMessage message)
            {
                return message.IsModerator || message.IsBroadcaster;
            }

            public async Task Execute(IDictionary<string, string> values, ChatMessage message, HermesSocketClient client)
            {
                string sceneName = values["sceneName"];
                string sourceName = values["sourceName"];
                string propertyName = values["propertyName"];
                string value = values["value"];
                _logger.Debug($"Getting scene item transformation data via chat command [scene name: {sceneName}][source name: {sourceName}][property: {propertyName}][value: {value}]");
                await _obs.UpdateTransformation(sceneName, sourceName, (d) =>
                {
                    if (propertyName.ToLower() == "rotation" || propertyName.ToLower() == "rotate")
                        d.Rotation = int.Parse(value);
                    else if (propertyName.ToLower() == "x")
                        d.PositionX = int.Parse(value);
                    else if (propertyName.ToLower() == "y")
                        d.PositionY = int.Parse(value);
                });
            }
        }

        private sealed class OBSVisibility : IChatPartialCommand
        {
            private readonly OBSSocketClient _obs;
            private readonly ILogger _logger;

            public string Name => "obs";
            public bool AcceptCustomPermission { get => true; }

            public OBSVisibility(
                [FromKeyedServices("obs")] SocketClient<WebSocketMessage> obs,
                ILogger logger
            )
            {
                _obs = (obs as OBSSocketClient)!;
                _logger = logger;
            }

            public bool CheckDefaultPermissions(ChatMessage message)
            {
                return message.IsModerator || message.IsBroadcaster;
            }

            public async Task Execute(IDictionary<string, string> values, ChatMessage message, HermesSocketClient client)
            {
                string sceneName = values["sceneName"];
                string sourceName = values["sourceName"];
                string state = values["state"];
                _logger.Debug($"Updating scene item visibility via chat command [scene name: {sceneName}][source name: {sourceName}][state: {state}]");
                string stateLower = state.ToLower();
                bool stateBool = stateLower == "true" || stateLower == "enable" || stateLower == "enabled" || stateLower == "yes";
                await _obs.UpdateSceneItemVisibility(sceneName, sourceName, stateBool);
            }
        }
    }
}