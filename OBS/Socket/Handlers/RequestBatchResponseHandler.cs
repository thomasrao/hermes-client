using System.Text.Json;
using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Context;
using TwitchChatTTS.OBS.Socket.Data;
using TwitchChatTTS.OBS.Socket.Manager;

namespace TwitchChatTTS.OBS.Socket.Handlers
{
    public class RequestBatchResponseHandler : IWebSocketHandler
    {
        private OBSRequestBatchManager _manager { get; }
        private IServiceProvider _serviceProvider { get; }
        private ILogger _logger { get; }
        private JsonSerializerOptions _options;
        public int OperationCode { get; set; } = 9;

        public RequestBatchResponseHandler(OBSRequestBatchManager manager, JsonSerializerOptions options, IServiceProvider serviceProvider, ILogger logger)
        {
            _manager = manager;
            _serviceProvider = serviceProvider;
            _logger = logger;
            _options = options;
        }

        public async Task Execute<Data>(SocketClient<WebSocketMessage> sender, Data data)
        {
            if (data is not RequestBatchResponseMessage message || message == null)
                return;

            using (LogContext.PushProperty("obsrid", message.RequestId))
            {
            
                var results = message.Results.ToList();
                _logger.Debug($"Received request batch response of {results.Count} messages.");

                var requestData = _manager.Take(message.RequestId);
                if (requestData == null || !results.Any())
                {
                    _logger.Verbose($"Received request batch response of {results.Count} messages.");
                    return;
                }

                IList<Task> tasks = new List<Task>();
                int count = Math.Min(results.Count, requestData.RequestTypes.Count);
                for (int i = 0; i < count; i++)
                {
                    Type type = requestData.RequestTypes[i];
                    
                    using (LogContext.PushProperty("type", type.Name))
                    {
                        try
                        {
                            var handler = GetResponseHandlerForRequestType(type);
                            _logger.Verbose($"Request handled by {handler.GetType().Name}.");
                            tasks.Add(handler.Execute(sender, results[i]));
                        }
                        catch (Exception ex)
                        {
                            _logger.Error(ex, "Failed to process an item in a request batch message.");
                        }
                    }
                }

                _logger.Verbose($"Waiting for processing to complete.");
                await Task.WhenAll(tasks);

                _logger.Debug($"Finished processing all request in this batch.");
            }
        }

        private IWebSocketHandler? GetResponseHandlerForRequestType(Type type)
        {
            if (type == typeof(RequestMessage))
                return _serviceProvider.GetRequiredKeyedService<IWebSocketHandler>("obs-requestresponse");
            else if (type == typeof(RequestBatchMessage))
                return _serviceProvider.GetRequiredKeyedService<IWebSocketHandler>("obs-requestbatcresponse");
            else if (type == typeof(IdentifyMessage))
                return _serviceProvider.GetRequiredKeyedService<IWebSocketHandler>("obs-identified");
            return null;
        }
    }
}