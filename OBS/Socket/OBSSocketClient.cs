using CommonSocketLibrary.Common;
using CommonSocketLibrary.Abstract;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System.Text.Json;
using System.Collections.Concurrent;
using TwitchChatTTS.OBS.Socket.Data;
using System.Timers;
using System.Net.WebSockets;

namespace TwitchChatTTS.OBS.Socket
{
    public class OBSSocketClient : WebSocketClient
    {
        private readonly IDictionary<string, RequestData> _requests;
        private readonly IDictionary<string, long> _sourceIds;
        private string? URL;

        private readonly Configuration _configuration;
        private System.Timers.Timer _reconnectTimer;

        public bool Connected { get; set; }
        public bool Identified { get; set; }
        public bool Streaming { get; set; }

        
        public OBSSocketClient(
            Configuration configuration,
            [FromKeyedServices("obs")] IEnumerable<IWebSocketHandler> handlers,
            [FromKeyedServices("obs")] MessageTypeManager<IWebSocketHandler> typeManager,
            ILogger logger
        ) : base(handlers, typeManager, new JsonSerializerOptions()
        {
            PropertyNameCaseInsensitive = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }, logger)
        {
            _configuration = configuration;

            _reconnectTimer = new System.Timers.Timer(TimeSpan.FromSeconds(30));
            _reconnectTimer.Elapsed += async (sender, e) => await Reconnect(e);
            _reconnectTimer.Enabled = false;

            _requests = new ConcurrentDictionary<string, RequestData>();
            _sourceIds = new Dictionary<string, long>();
        }

        public void Initialize()
        {
            _logger.Information($"Initializing OBS websocket client.");
            OnConnected += (sender, e) =>
            {
                Connected = true;
                _reconnectTimer.Enabled = false;
                _logger.Information("OBS websocket client connected.");
            };

            OnDisconnected += (sender, e) =>
            {
                _reconnectTimer.Enabled = Identified;
                _logger.Information($"OBS websocket client disconnected [status: {e.Status}][reason: {e.Reason}] " + (Identified ? "Will be attempting to reconnect every 30 seconds." : "Will not be attempting to reconnect."));

                Connected = false;
                Identified = false;
                Streaming = false;
            };

            if (!string.IsNullOrWhiteSpace(_configuration.Obs?.Host) && _configuration.Obs?.Port != null)
                URL = $"ws://{_configuration.Obs.Host?.Trim()}:{_configuration.Obs.Port}";
        }


        public void AddSourceId(string sourceName, long sourceId)
        {
            if (!_sourceIds.TryGetValue(sourceName, out _))
                _sourceIds.Add(sourceName, sourceId);
            else
                _sourceIds[sourceName] = sourceId;
            _logger.Debug($"Added OBS scene item to cache [scene item: {sourceName}][scene item id: {sourceId}]");
        }

        public void ClearCache()
        {
            _sourceIds.Clear();
        }

        public async Task Connect()
        {
            if (string.IsNullOrWhiteSpace(URL))
            {
                _logger.Warning("Lacking connection info for OBS websockets. Not connecting to OBS.");
                return;
            }

            _logger.Debug($"OBS websocket client attempting to connect to {URL}");

            try
            {
                await ConnectAsync(URL);
            }
            catch (Exception)
            {
                _logger.Warning("Connecting to obs failed. Skipping obs websockets.");
            }
        }

        public async Task ExecuteRequest(RequestResponseMessage message) {
            if (!_handlers.TryGetValue(7, out var handler) || handler == null)
            {
                _logger.Error("Failed to find the request response handler for OBS.");
                return;
            }

            await handler.Execute(this, message);
        }

        private async Task Reconnect(ElapsedEventArgs e)
        {
            if (Connected)
            {
                try
                {
                    await DisconnectAsync(new SocketDisconnectionEventArgs(WebSocketCloseStatus.Empty.ToString(), ""));
                }
                catch (Exception)
                {
                    _logger.Error("Failed to disconnect from OBS websocket server.");
                }
            }

            try
            {
                await Connect();
            }
            catch (WebSocketException wse) when (wse.Message.Contains("502"))
            {
                _logger.Error("OBS websocket server cannot be found. Be sure the server is on by looking at OBS > Tools > Websocket Server Settings.");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to reconnect to OBS websocket server.");
            }
        }

        public async Task Send(IEnumerable<RequestMessage> messages)
        {
            if (!Connected)
            {
                _logger.Warning("OBS websocket client is not connected. Not sending a message.");
                return;
            }

            string uid = GenerateUniqueIdentifier();
            var list = messages.ToList();
            _logger.Debug($"Sending OBS request batch of {list.Count} messages [obs request batch id: {uid}].");

            // Keep track of requests to know what we requested.
            foreach (var message in list)
            {
                message.RequestId = GenerateUniqueIdentifier();
                var data = new RequestData(message, uid);
                _requests.Add(message.RequestId, data);
            }

            _logger.Debug($"Generated uid for all OBS request messages in batch [obs request batch id: {uid}][obs request ids: {string.Join(", ", list.Select(m => m.RequestType + "=" + m.RequestId))}]");
            await Send(8, new RequestBatchMessage(uid, list));
        }

        public async Task Send(RequestMessage message, Action<Dictionary<string, object>>? callback = null)
        {
            if (!Connected)
            {
                _logger.Warning("OBS websocket client is not connected. Not sending a message.");
                return;
            }

            string uid = GenerateUniqueIdentifier();
            _logger.Debug($"Sending an OBS request [type: {message.RequestType}][obs request id: {uid}]");

            // Keep track of requests to know what we requested.
            message.RequestId = uid;
            var data = new RequestData(message, uid)
            {
                Callback = callback
            };
            _requests.Add(message.RequestId, data);

            await Send(6, message);
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

        public async Task UpdateStreamingState()
        {
            await Send(new RequestMessage("GetStreamStatus"));
        }

        public async Task UpdateTransformation(string sceneName, string sceneItemName, Action<OBSTransformationData> action)
        {
            if (action == null)
                return;

            await GetSceneItemByName(sceneName, sceneItemName, async (sceneItemId) =>
            {
                var m2 = new RequestMessage("GetSceneItemTransform", new Dictionary<string, object>() { { "sceneName", sceneName }, { "sceneItemId", sceneItemId } });
                await Send(m2, async (d) =>
                {
                    if (d == null || !d.TryGetValue("sceneItemTransform", out object? transformData) || transformData == null)
                        return;

                    _logger.Verbose($"Current transformation data [scene: {sceneName}][sceneItemName: {sceneItemName}][sceneItemId: {sceneItemId}][transform: {transformData}][obs request id: {m2.RequestId}]");
                    var transform = JsonSerializer.Deserialize<OBSTransformationData>(transformData.ToString()!, new JsonSerializerOptions()
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

                    action?.Invoke(transform);

                    var m3 = new RequestMessage("SetSceneItemTransform", string.Empty, new Dictionary<string, object>() { { "sceneName", sceneName }, { "sceneItemId", sceneItemId }, { "sceneItemTransform", transform } });
                    await Send(m3);
                    _logger.Debug($"New transformation data [scene: {sceneName}][sceneItemName: {sceneItemName}][sceneItemId: {sceneItemId}][obs request id: {m3.RequestId}]");
                });
            });
        }

        public async Task ToggleSceneItemVisibility(string sceneName, string sceneItemName)
        {
            await GetSceneItemByName(sceneName, sceneItemName, async (sceneItemId) =>
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
        }

        public async Task UpdateSceneItemVisibility(string sceneName, string sceneItemName, bool isVisible)
        {
            await GetSceneItemByName(sceneName, sceneItemName, async (sceneItemId) =>
            {
                var m = new RequestMessage("SetSceneItemEnabled", string.Empty, new Dictionary<string, object>() { { "sceneName", sceneName }, { "sceneItemId", sceneItemId }, { "sceneItemEnabled", isVisible } });
                await Send(m);
            });
        }

        public async Task UpdateSceneItemIndex(string sceneName, string sceneItemName, int index)
        {
            await GetSceneItemByName(sceneName, sceneItemName, async (sceneItemId) =>
            {
                var m = new RequestMessage("SetSceneItemIndex", string.Empty, new Dictionary<string, object>() { { "sceneName", sceneName }, { "sceneItemId", sceneItemId }, { "sceneItemIndex", index } });
                await Send(m);
            });
        }

        public async Task GetGroupList(Action<IEnumerable<string>>? action)
        {
            var m = new RequestMessage("GetGroupList", string.Empty, new Dictionary<string, object>());
            await Send(m, (d) =>
            {
                if (d == null || !d.TryGetValue("groups", out object? value) || value == null)
                    return;

                var list = (IEnumerable<string>)value;
                _logger.Debug("Fetched the list of groups in OBS.");
                if (list != null)
                    action?.Invoke(list);
            });
        }

        public async Task GetGroupSceneItemList(string groupName, Action<IEnumerable<OBSSceneItem>>? action)
        {
            var m = new RequestMessage("GetGroupSceneItemList", string.Empty, new Dictionary<string, object>() { { "sceneName", groupName } });
            await Send(m, (d) =>
            {
                if (d == null || !d.TryGetValue("sceneItems", out object? value) || value == null)
                    return;

                var list = (IEnumerable<OBSSceneItem>)value;
                _logger.Debug($"Fetched the list of OBS scene items in a group [group: {groupName}]");
                if (list != null)
                    action?.Invoke(list);
            });
        }

        public async Task GetGroupSceneItemList(IEnumerable<string> groupNames)
        {
            var messages = groupNames.Select(group => new RequestMessage("GetGroupSceneItemList", string.Empty, new Dictionary<string, object>() { { "sceneName", group } }));
            await Send(messages);
            _logger.Debug($"Fetched the list of OBS scene items in all groups [groups: {string.Join(", ", groupNames)}]");
        }

        private async Task GetSceneItemByName(string sceneName, string sceneItemName, Action<long> action)
        {
            if (_sourceIds.TryGetValue(sceneItemName, out long sourceId))
            {
                _logger.Debug($"Fetched scene item id from cache [scene: {sceneName}][scene item: {sceneItemName}][scene item id: {sourceId}]");
                action.Invoke(sourceId);
                return;
            }

            var m = new RequestMessage("GetSceneItemId", string.Empty, new Dictionary<string, object>() { { "sceneName", sceneName }, { "sourceName", sceneItemName } });
            await Send(m, async (d) =>
            {
                if (d == null || !d.TryGetValue("sceneItemId", out object? value) || value == null || !long.TryParse(value.ToString(), out long sceneItemId))
                    return;

                _logger.Debug($"Fetched scene item id from OBS [scene: {sceneName}][scene item: {sceneItemName}][scene item id: {sceneItemId}][obs request id: {m.RequestId}]");
                AddSourceId(sceneItemName, sceneItemId);
                action.Invoke(sceneItemId);
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
        public Dictionary<string, object>? ResponseValues { get; set; }
        public Action<Dictionary<string, object>>? Callback { get; set; }

        public RequestData(RequestMessage message, string parentId)
        {
            Message = message;
            ParentId = parentId;
        }
    }
}