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
            _logger.Debug($"Sending OBS request batch of {messages.Count()} messages [obsid: {uid}].");

            // Keep track of requests to know what we requested.
            foreach (var message in messages)
            {
                message.RequestId = GenerateUniqueIdentifier();
                var data = new RequestData(message, uid);
                _requests.Add(message.RequestId, data);
            }
            _logger.Debug($"Generated uid for all OBS request messages in batch [obsid: {uid}]: {string.Join(", ", messages.Select(m => m.RequestType + "=" + m.RequestId))}");

            var client = _serviceProvider.GetRequiredKeyedService<SocketClient<WebSocketMessage>>("obs");
            await client.Send(8, new RequestBatchMessage(uid, messages));
        }

        public async Task Send(RequestMessage message, Action<Dictionary<string, object>>? callback = null)
        {
            string uid = GenerateUniqueIdentifier();
            _logger.Debug($"Sending an OBS request [obsid: {uid}]");

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
            var m1 = new RequestMessage("GetSceneItemId", string.Empty, new Dictionary<string, object>() { { "sceneName", sceneName }, { "sourceName", sceneItemName } });
            await Send(m1, async (d) =>
            {
                if (!d.TryGetValue("sceneItemId", out object value) || !long.TryParse(value.ToString(), out long sceneItemId))
                    return;

                _logger.Debug($"Fetched scene item id from OBS [scene: {sceneName}][sceneItemName: {sceneItemName}][obsid: {m1.RequestId}]: {sceneItemId}");
                var m2 = new RequestMessage("GetSceneItemTransform", string.Empty, new Dictionary<string, object>() { { "sceneName", sceneName }, { "sceneItemId", sceneItemId } });
                await Send(m2, async (d) =>
                {
                    if (d == null)
                        return;
                    if (!d.TryGetValue("sceneItemTransform", out object transformData))
                        return;

                    _logger.Verbose($"Current transformation data [scene: {sceneName}][sceneItemName: {sceneItemName}][sceneItemId: {sceneItemId}][obsid: {m2.RequestId}]: {transformData}");
                    var transform = JsonSerializer.Deserialize<OBSTransformationData>(transformData.ToString(), new JsonSerializerOptions()
                    {
                        PropertyNameCaseInsensitive = false,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });
                    if (transform == null)
                    {
                        _logger.Warning($"Could not deserialize the transformation data received by OBS [scene: {sceneName}][sceneItemName: {sceneItemName}][sceneItemId: {sceneItemId}][obsid: {m2.RequestId}].");
                        return;
                    }

                    //double fr = (transform.Rotation + rotation) % 360;
                    double w = transform.Width;
                    double h = transform.Height;

                    // double ox = w * Math.Cos(r) - h * Math.Sin(r);
                    // double oy = w * Math.Sin(r) + h * Math.Cos(r);
                    //var oo = (fr > 45 && fr < 225 ? 0 : 1);
                    // var ww = fr >= 135 && fr < 225 ? h : w;
                    // var hh = fr >= 315 || fr < 45 ? h : w;
                    //double dx = h * Math.Sin(r);
                    //double dy = w * Math.Cos(fr > 90 && fr < 270 ? Math.PI - r : r); // * (fr >= 135 && fr < 225 || fr >= 315 || fr <= 45 ? -1 : 1);

                    int a = transform.Alignment;
                    bool hasBounds = transform.BoundsType != "OBS_BOUNDS_NONE";

                    if (hasBounds)
                    {
                        // Take care of bounds, for most cases.
                        // 'Crop to Bounding Box' might be unsupported.
                        w = transform.BoundsWidth;
                        h = transform.BoundsHeight;
                        a = transform.BoundsAlignment;
                    }
                    else if (transform.CropBottom + transform.CropLeft + transform.CropRight + transform.CropTop > 0)
                    {
                        w -= transform.CropLeft + transform.CropRight;
                        h -= transform.CropTop + transform.CropBottom;
                    }

                    if (a != (int)OBSAlignment.Center)
                    {
                        if (hasBounds)
                            transform.BoundsAlignment = a = (int)OBSAlignment.Center;
                        else
                            transform.Alignment = a = (int)OBSAlignment.Center;
                        transform.PositionX = transform.PositionX + w / 2;
                        transform.PositionY = transform.PositionY + h / 2;
                    }

                    action?.Invoke(transform);

                    // double ax = w * Math.Cos(ir) - h * Math.Sin(ir);
                    // double ay = w * Math.Sin(ir) + h * Math.Cos(ir);
                    // _logger.Information($"ax: {ax}  ay: {ay}");

                    // double bx = w * Math.Cos(r) - h * Math.Sin(r);
                    // double by = w * Math.Sin(r) + h * Math.Cos(r);
                    // _logger.Information($"bx: {bx}  by: {by}");

                    // double ddx = bx - ax;
                    // double ddy = by - ay;
                    // _logger.Information($"dx: {ddx}  dy: {ddy}");

                    // double arctan = Math.Atan(ddy / ddx);
                    // _logger.Information("Angle: " + arctan);

                    // var xs = new int[] { 0, 0, 1, 1 };
                    // var ys = new int[] { 0, 1, 1, 0 };
                    // int i = ((int)Math.Floor(fr / 90) + 8) % 4;
                    // double dx = xs[i] * w * Math.Cos(rad) - ys[i] * h * Math.Sin(rad);
                    // double dy = xs[i] * w * Math.Sin(rad) + ys[i] * h * Math.Cos(rad);


                    //transform.Rotation = fr;
                    //_logger.Information($"w: {w}  h: {h}  fr: {fr}  r: {r}  rot: {rotation}");
                    //_logger.Information($"dx: {dx}  ox: {ox}  oox: {oox}");
                    //_logger.Information($"dy: {dy}  oy: {oy}  ooy: {ooy}");

                    var m3 = new RequestMessage("SetSceneItemTransform", string.Empty, new Dictionary<string, object>() { { "sceneName", sceneName }, { "sceneItemId", sceneItemId }, { "sceneItemTransform", transform } });
                    await Send(m3);
                });
            });
        }

        private string GenerateUniqueIdentifier()
        {
            return Guid.NewGuid().ToString("N");
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