using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;
using Serilog;
using TwitchChatTTS.OBS.Socket.Data;
using TwitchChatTTS.OBS.Socket.Manager;

namespace TwitchChatTTS.OBS.Socket.Handlers
{
    public class RequestResponseHandler : IWebSocketHandler
    {
        private readonly OBSManager _manager;
        private readonly ILogger _logger;
        public int OperationCode { get; } = 7;

        public RequestResponseHandler(OBSManager manager, ILogger logger)
        {
            _manager = manager;
            _logger = logger;
        }

        public async Task Execute<Data>(SocketClient<WebSocketMessage> sender, Data data)
        {
            if (data is not RequestResponseMessage message || message == null)
                return;

            _logger.Debug($"Received an OBS request response [response id: {message.RequestId}]");

            var requestData = _manager.Take(message.RequestId);
            if (requestData == null)
            {
                _logger.Warning($"OBS Request Response not being processed: request not stored [response id: {message.RequestId}]");
                return;
            }

            var request = requestData.Message;
            if (request == null)
                return;

            try
            {
                switch (request.RequestType)
                {
                    case "GetOutputStatus":
                        if (sender is not OBSSocketClient client)
                            return;

                        if (message.RequestId == "stream")
                        {
                            client.Live = message.ResponseData["outputActive"].ToString() == "True";
                            _logger.Warning($"Updated stream's live status to {client.Live} [response id: {message.RequestId}]");
                        }
                        break;
                    case "GetSceneItemId":
                        if (!request.RequestData.TryGetValue("sceneName", out object sceneName))
                        {
                            _logger.Warning($"Failed to find the scene name that was requested [response id: {message.RequestId}]");
                            return;
                        }
                        if (!request.RequestData.TryGetValue("sourceName", out object sourceName))
                        {
                            _logger.Warning($"Failed to find the scene item name that was requested [scene: {sceneName}][response id: {message.RequestId}]");
                            return;
                        }
                        if (!message.ResponseData.TryGetValue("sceneItemId", out object sceneItemId)) {
                            _logger.Warning($"Failed to fetch the scene item id [scene: {sceneName}][scene item: {sourceName}][response id: {message.RequestId}]");
                            return;
                        }

                        _logger.Information($"Added scene item id [scene: {sceneName}][source: {sourceName}][id: {sceneItemId}][response id: {message.RequestId}].");
                        _manager.AddSourceId(sceneName.ToString(), sourceName.ToString(), long.Parse(sceneItemId.ToString()));

                        requestData.ResponseValues = new Dictionary<string, object>
                        {
                            { "sceneItemId", sceneItemId }
                        };
                        break;
                    case "GetSceneItemTransform":
                        if (!message.ResponseData.TryGetValue("sceneItemTransform", out object? transformData))
                        {
                            _logger.Warning($"Failed to find the OBS scene item [response id: {message.RequestId}]");
                            return;
                        }

                        _logger.Verbose("Fetching OBS transformation data: " + transformData?.ToString());
                        requestData.ResponseValues = new Dictionary<string, object>
                        {
                            { "sceneItemTransform", transformData }
                        };
                        break;
                    default:
                        _logger.Warning($"OBS Request Response not being processed [type: {request.RequestType}][{string.Join(Environment.NewLine, message.ResponseData?.Select(kvp => kvp.Key + " = " + kvp.Value?.ToString()) ?? new string[0])}]");
                        break;
                }
            }
            finally
            {
                if (requestData.Callback != null)
                    requestData.Callback(requestData.ResponseValues);
            }
        }
    }
}