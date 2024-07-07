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

            _logger.Debug($"Received an OBS request response [obs request id: {message.RequestId}]");

            var requestData = _manager.Take(message.RequestId);
            if (requestData == null)
            {
                _logger.Warning($"OBS Request Response not being processed: request not stored [obs request id: {message.RequestId}]");
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

                        _logger.Debug($"Fetched stream's live status [live: {client.Live}][obs request id: {message.RequestId}]");
                        break;
                    case "GetSceneItemId":
                        {
                            if (!request.RequestData.TryGetValue("sceneName", out object? sceneName) || sceneName == null)
                            {
                                _logger.Warning($"Failed to find the scene name that was requested [obs request id: {message.RequestId}]");
                                return;
                            }
                            if (!request.RequestData.TryGetValue("sourceName", out object? sourceName) || sourceName == null)
                            {
                                _logger.Warning($"Failed to find the scene item name that was requested [scene: {sceneName}][obs request id: {message.RequestId}]");
                                return;
                            }
                            if (message.ResponseData == null)
                            {
                                _logger.Warning($"OBS Response is null [scene: {sceneName}][scene item: {sourceName}][obs request id: {message.RequestId}]");
                                return;
                            }
                            if (!message.ResponseData.TryGetValue("sceneItemId", out object? sceneItemId) || sceneItemId == null)
                            {
                                _logger.Warning($"Failed to fetch the scene item id [scene: {sceneName}][scene item: {sourceName}][obs request id: {message.RequestId}]");
                                return;
                            }

                            _logger.Debug($"Found the scene item by name [scene: {sceneName}][source: {sourceName}][id: {sceneItemId}][obs request id: {message.RequestId}].");
                            //_manager.AddSourceId(sceneName.ToString(), sourceName.ToString(), (long) sceneItemId);

                            requestData.ResponseValues = new Dictionary<string, object>
                            {
                                { "sceneItemId", sceneItemId }
                            };
                            break;
                        }
                    case "GetSceneItemTransform":
                        {
                            if (!request.RequestData.TryGetValue("sceneName", out object? sceneName) || sceneName == null)
                            {
                                _logger.Warning($"Failed to find the scene name that was requested [obs request id: {message.RequestId}]");
                                return;
                            }
                            if (!request.RequestData.TryGetValue("sceneItemId", out object? sceneItemId) || sceneItemId == null)
                            {
                                _logger.Warning($"Failed to find the scene item name that was requested [scene: {sceneName}][obs request id: {message.RequestId}]");
                                return;
                            }
                            if (message.ResponseData == null)
                            {
                                _logger.Warning($"OBS Response is null [scene: {sceneName}][scene item id: {sceneItemId}][obs request id: {message.RequestId}]");
                                return;
                            }
                            if (!message.ResponseData.TryGetValue("sceneItemTransform", out object? transformData) || transformData == null)
                            {
                                _logger.Warning($"Failed to fetch the OBS transformation data [obs request id: {message.RequestId}]");
                                return;
                            }

                            _logger.Debug($"Fetched OBS transformation data [scene: {sceneName}][scene item id: {sceneItemId}][transformation: {transformData}][obs request id: {message.RequestId}]");
                            requestData.ResponseValues = new Dictionary<string, object>
                            {
                                { "sceneItemTransform", transformData }
                            };
                            break;
                        }
                    case "GetSceneItemEnabled":
                        {
                            if (!request.RequestData.TryGetValue("sceneName", out object? sceneName) || sceneName == null)
                            {
                                _logger.Warning($"Failed to find the scene name that was requested [obs request id: {message.RequestId}]");
                                return;
                            }
                            if (!request.RequestData.TryGetValue("sceneItemId", out object? sceneItemId) || sceneItemId == null)
                            {
                                _logger.Warning($"Failed to find the scene item name that was requested [scene: {sceneName}][obs request id: {message.RequestId}]");
                                return;
                            }
                            if (message.ResponseData == null)
                            {
                                _logger.Warning($"OBS Response is null [scene: {sceneName}][scene item id: {sceneItemId}][obs request id: {message.RequestId}]");
                                return;
                            }
                            if (!message.ResponseData.TryGetValue("sceneItemEnabled", out object? sceneItemVisibility) || sceneItemVisibility == null)
                            {
                                _logger.Warning($"Failed to fetch the scene item visibility [scene: {sceneName}][scene item id: {sceneItemId}][obs request id: {message.RequestId}]");
                                return;
                            }

                            _logger.Debug($"Fetched OBS scene item visibility [scene: {sceneName}][scene item id: {sceneItemId}][visibility: {sceneItemVisibility}][obs request id: {message.RequestId}]");
                            requestData.ResponseValues = new Dictionary<string, object>
                            {
                                { "sceneItemEnabled", sceneItemVisibility }
                            };
                            break;
                        }
                    case "SetSceneItemTransform":
                        {
                            if (!request.RequestData.TryGetValue("sceneName", out object? sceneName) || sceneName == null)
                            {
                                _logger.Warning($"Failed to find the scene name that was requested [obs request id: {message.RequestId}]");
                                return;
                            }
                            if (!request.RequestData.TryGetValue("sceneItemId", out object? sceneItemId) || sceneItemId == null)
                            {
                                _logger.Warning($"Failed to find the scene item name that was requested [scene: {sceneName}][obs request id: {message.RequestId}]");
                                return;
                            }
                            _logger.Debug($"Received response from OBS for updating scene item transformation [scene: {sceneName}][scene item id: {sceneItemId}][obs request id: {message.RequestId}]");
                            break;
                        }
                    case "SetSceneItemEnabled":
                        {
                            if (!request.RequestData.TryGetValue("sceneName", out object? sceneName) || sceneName == null)
                            {
                                _logger.Warning($"Failed to find the scene name that was requested [obs request id: {message.RequestId}]");
                                return;
                            }
                            if (!request.RequestData.TryGetValue("sceneItemId", out object? sceneItemId) || sceneItemId == null)
                            {
                                _logger.Warning($"Failed to find the scene item name that was requested [scene: {sceneName}][obs request id: {message.RequestId}]");
                                return;
                            }
                            _logger.Debug($"Received response from OBS for updating scene item visibility [scene: {sceneName}][scene item id: {sceneItemId}][obs request id: {message.RequestId}]");
                            break;
                        }
                    case "Sleep":
                        {
                            if (!request.RequestData.TryGetValue("sleepMillis", out object? sleepMillis) || sleepMillis == null)
                            {
                                _logger.Warning($"Failed to find the scene name that was requested [obs request id: {message.RequestId}]");
                                return;
                            }
                            _logger.Debug($"Received response from OBS for sleeping [sleep: {sleepMillis}][obs request id: {message.RequestId}]");
                            break;
                        }
                    default:
                        _logger.Warning($"OBS Request Response not being processed [type: {request.RequestType}][{string.Join(Environment.NewLine, message.ResponseData?.Select(kvp => kvp.Key + " = " + kvp.Value?.ToString()) ?? [])}]");
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to process the response from OBS for a request [type: {request.RequestType}]");
            }
            finally
            {
                if (requestData.Callback != null)
                    requestData.Callback(requestData.ResponseValues);
            }
        }
    }
}