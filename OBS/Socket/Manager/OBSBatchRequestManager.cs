using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using TwitchChatTTS.OBS.Socket.Data;

namespace TwitchChatTTS.OBS.Socket.Manager
{
    public class OBSRequestBatchManager
    {
        private IDictionary<string, OBSRequestBatchData> _requests;
        private IServiceProvider _serviceProvider;
        private ILogger _logger;

        public OBSRequestBatchManager(IServiceProvider serviceProvider, ILogger logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        
        public async Task Send(long broadcasterId, IEnumerable<WebSocketMessage> messages) {
            string uid = GenerateUniqueIdentifier();
            var data = new OBSRequestBatchData(broadcasterId, uid, new List<Type>());
            _logger.Debug($"Sending request batch of {messages.Count()} messages.");

            foreach (WebSocketMessage message in messages)
                data.RequestTypes.Add(message.GetType());

            var client = _serviceProvider.GetRequiredKeyedService<SocketClient<WebSocketMessage>>("obs");
            await client.Send(8, new RequestBatchMessage(uid, messages));
        }

        public OBSRequestBatchData? Take(string id) {
            if (_requests.TryGetValue(id, out var request)) {
                _requests.Remove(id);
                return request;
            }
            return null;
        }

        private string GenerateUniqueIdentifier()
        {
            return Guid.NewGuid().ToString("X");
        }
    }

    public class OBSRequestBatchData
    {
        public long BroadcasterId { get; }
        public string RequestId { get; }
        public IList<Type> RequestTypes { get; }

        public OBSRequestBatchData(long bid, string rid, IList<Type> types) {
            BroadcasterId = bid;
            RequestId = rid;
            RequestTypes = types;
        }
    }
}