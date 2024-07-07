using System.Collections.Concurrent;
using System.Text.Json;
using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using TwitchChatTTS.OBS.Socket.Data;

namespace TwitchChatTTS.OBS.Socket.Manager
{
    public class OBSManager
    {
        private IDictionary<string, RequestData> _requests;
        private IDictionary<string, IDictionary<string, long>> _sourceIds;
        private IServiceProvider _serviceProvider;
        private ILogger _logger;

        public OBSManager(IServiceProvider serviceProvider, ILogger logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;

            _requests = new ConcurrentDictionary<string, RequestData>();
            _sourceIds = new Dictionary<string, IDictionary<string, long>>();
        }


        public void AddSourceId(string sceneName, string sourceName, long sourceId)
        {
            if (!_sourceIds.TryGetValue(sceneName, out var scene))
            {
                scene = new Dictionary<string, long>();
                _sourceIds.Add(sceneName, scene);
            }

            if (scene.ContainsKey(sourceName))
                scene[sourceName] = sourceId;
            else
                scene.Add(sourceName, sourceId);

        }

        public async Task Send(IEnumerable<RequestMessage> messages)
        {
            string uid = GenerateUniqueIdentifier();
            _logger.Debug($"Sending OBS request batch of {messages.Count()} messages [obs request id: {uid}].");

            // Keep track of requests to know what we requested.
            foreach (var message in messages)
            {
                message.RequestId = GenerateUniqueIdentifier();
                var data = new RequestData(message, uid);
                _requests.Add(message.RequestId, data);
            }
            _logger.Debug($"Generated uid for all OBS request messages in batch [obs request id: {uid}][obs request ids: {string.Join(", ", messages.Select(m => m.RequestType + "=" + m.RequestId))}]");

            var client = _serviceProvider.GetRequiredKeyedService<SocketClient<WebSocketMessage>>("obs");
            await client.Send(8, new RequestBatchMessage(uid, messages));
        }

        public async Task Send(RequestMessage message, Action<Dictionary<string, object>>? callback = null)
        {
            string uid = GenerateUniqueIdentifier();
            _logger.Debug($"Sending an OBS request [type: {message.RequestType}][obs request id: {uid}]");

            // Keep track of requests to know what we requested.
            message.RequestId = GenerateUniqueIdentifier();
            var data = new RequestData(message, uid)
            {
                Callback = callback
            };
            _requests.Add(message.RequestId, data);

            var client = _serviceProvider.GetRequiredKeyedService<SocketClient<WebSocketMessage>>("obs");
            await client.Send(6, message);
        }

        public RequestData? Take(string id)
        {
            if (id != null && _requests.TryGetValue(id, out var request))
            {
                _requests.Remove(id);
                return request;
            }
            return null;
        }

        public async Task UpdateTransformation(string sceneName, string sceneItemName, Action<OBSTransformationData> action)
        {
            if (action == null)
                return;

            await GetSceneItemById(sceneName, sceneItemName, async (sceneItemId) =>
            {
                var m2 = new RequestMessage("GetSceneItemTransform", string.Empty, new Dictionary<string, object>() { { "sceneName", sceneName }, { "sceneItemId", sceneItemId } });
                await Send(m2, async (d) =>
                {
                    if (d == null || !d.TryGetValue("sceneItemTransform", out object? transformData) || transformData == null)
                        return;

                    _logger.Verbose($"Current transformation data [scene: {sceneName}][sceneItemName: {sceneItemName}][sceneItemId: {sceneItemId}][transform: {transformData}][obs request id: {m2.RequestId}]");
                    var transform = JsonSerializer.Deserialize<OBSTransformationData>(transformData.ToString(), new JsonSerializerOptions()
                    {
                        PropertyNameCaseInsensitive = false,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });
                    if (transform == null)
                    {
                        _logger.Warning($"Could not deserialize the transformation data received by OBS [scene: {sceneName}][sceneItemName: {sceneItemName}][sceneItemId: {sceneItemId}][obs request id: {m2.RequestId}].");
                        return;
                    }

                    double w = transform.Width;
                    double h = transform.Height;
                    int a = transform.Alignment;
                    bool hasBounds = transform.BoundsType != "OBS_BOUNDS_NONE";

                    if (a != (int)OBSAlignment.Center)
                    {
                        if (hasBounds)
                            transform.BoundsAlignment = a = (int)OBSAlignment.Center;
                        else
                            transform.Alignment = a = (int)OBSAlignment.Center;
                        
                        transform.PositionX = transform.PositionX + w / 2;
                        transform.PositionY = transform.PositionY + h / 2;
                    }

                    // if (hasBounds)
                    // {
                    //     // Take care of bounds, for most cases.
                    //     // 'Crop to Bounding Box' might be unsupported.
                    //     w = transform.BoundsWidth;
                    //     h = transform.BoundsHeight;
                    //     a = transform.BoundsAlignment;
                    // }
                    // else if (transform.CropBottom + transform.CropLeft + transform.CropRight + transform.CropTop > 0)
                    // {
                    //     w -= transform.CropLeft + transform.CropRight;
                    //     h -= transform.CropTop + transform.CropBottom;
                    // }

                    action?.Invoke(transform);

                    var m3 = new RequestMessage("SetSceneItemTransform", string.Empty, new Dictionary<string, object>() { { "sceneName", sceneName }, { "sceneItemId", sceneItemId }, { "sceneItemTransform", transform } });
                    await Send(m3);
                    _logger.Debug($"New transformation data [scene: {sceneName}][sceneItemName: {sceneItemName}][sceneItemId: {sceneItemId}][transform: {transformData}][obs request id: {m2.RequestId}]");
                });
            });
        }

        public async Task ToggleSceneItemVisibility(string sceneName, string sceneItemName)
        {
            LogExceptions(async () =>
            {
                await GetSceneItemById(sceneName, sceneItemName, async (sceneItemId) =>
                {
                    var m1 = new RequestMessage("GetSceneItemEnabled", string.Empty, new Dictionary<string, object>() { { "sceneName", sceneName }, { "sceneItemId", sceneItemId } });
                    await Send(m1, async (d) =>
                    {
                        if (d == null || !d.TryGetValue("sceneItemEnabled", out object? visible) || visible == null)
                            return;

                        var m2 = new RequestMessage("SetSceneItemEnabled", string.Empty, new Dictionary<string, object>() { { "sceneName", sceneName }, { "sceneItemId", sceneItemId }, { "sceneItemEnabled", visible.ToString().ToLower() == "true" ? false : true } });
                        await Send(m2);
                    });
                });
            }, "Failed to toggle OBS scene item visibility.");
        }

        public async Task UpdateSceneItemVisibility(string sceneName, string sceneItemName, bool isVisible)
        {
            LogExceptions(async () =>
            {
                await GetSceneItemById(sceneName, sceneItemName, async (sceneItemId) =>
                {
                    var m = new RequestMessage("SetSceneItemEnabled", string.Empty, new Dictionary<string, object>() { { "sceneName", sceneName }, { "sceneItemId", sceneItemId }, { "sceneItemEnabled", isVisible } });
                    await Send(m);
                });
            }, "Failed to update OBS scene item visibility.");
        }

        public async Task UpdateSceneItemIndex(string sceneName, string sceneItemName, int index)
        {
            LogExceptions(async () =>
            {
                await GetSceneItemById(sceneName, sceneItemName, async (sceneItemId) =>
                {
                    var m = new RequestMessage("SetSceneItemIndex", string.Empty, new Dictionary<string, object>() { { "sceneName", sceneName }, { "sceneItemId", sceneItemId }, { "sceneItemIndex", index } });
                    await Send(m);
                });
            }, "Failed to update OBS scene item index.");
        }

        private async Task GetSceneItemById(string sceneName, string sceneItemName, Action<long> action)
        {
            var m1 = new RequestMessage("GetSceneItemId", string.Empty, new Dictionary<string, object>() { { "sceneName", sceneName }, { "sourceName", sceneItemName } });
            await Send(m1, async (d) =>
            {
                if (d == null || !d.TryGetValue("sceneItemId", out object? value) || value == null || !long.TryParse(value.ToString(), out long sceneItemId))
                    return;

                _logger.Debug($"Fetched scene item id from OBS [scene: {sceneName}][scene item: {sceneItemName}][scene item id: {sceneItemId}][obs request id: {m1.RequestId}]");
                action.Invoke(sceneItemId);
            });
        }

        private string GenerateUniqueIdentifier()
        {
            return Guid.NewGuid().ToString("N");
        }

        private void LogExceptions(Action action, string description)
        {
            try
            {
                action.Invoke();
            }
            catch (Exception e)
            {
                _logger.Error(e, description);
            }
        }
    }

    public class RequestData
    {
        public RequestMessage Message { get; }
        public string ParentId { get; }
        public Dictionary<string, object> ResponseValues { get; set; }
        public Action<Dictionary<string, object>>? Callback { get; set; }

        public RequestData(RequestMessage message, string parentId)
        {
            Message = message;
            ParentId = parentId;
        }
    }
}