using System.Text.Json;
using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Context;
using TwitchChatTTS.OBS.Socket.Data;

namespace TwitchChatTTS.OBS.Socket.Handlers
{
    public class RequestBatchResponseHandler : IWebSocketHandler
    {
        private readonly ILogger _logger;
        public int OperationCode { get; } = 9;

        public RequestBatchResponseHandler(ILogger logger)
        {
            _logger = logger;
        }

        public async Task Execute<Data>(SocketClient<WebSocketMessage> sender, Data data)
        {
            if (data is not RequestBatchResponseMessage message || message == null)
                return;
            if (sender is not OBSSocketClient obs)
                return;

            var results = message.Results.ToList();
            _logger.Debug($"Received request batch response of {results.Count} messages.");

            int count = results.Count;
            for (int i = 0; i < count; i++)
            {
                if (results[i] == null)
                    continue;

                try
                {
                    _logger.Debug($"Request response from OBS request batch #{i + 1}/{count}: {results[i]}");
                    var response = JsonSerializer.Deserialize<RequestResponseMessage>(results[i].ToString()!, new JsonSerializerOptions()
                    {
                        PropertyNameCaseInsensitive = false,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });
                    if (response == null)
                        continue;

                    await obs.ExecuteRequest(response);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to process an item in a request batch message.");
                }
            }

            _logger.Debug($"Finished processing all request in this batch.");
        }
    }
}