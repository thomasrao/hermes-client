using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TwitchChatTTS.Seven.Socket.Context;
using TwitchChatTTS.Seven.Socket.Data;

namespace TwitchChatTTS.Seven.Socket.Handlers
{
    public class EndOfStreamHandler : IWebSocketHandler
    {
        private ILogger Logger { get; }
        private Configuration Configuration { get; }
        private IServiceProvider ServiceProvider { get; }
        private string[] ErrorCodes { get; }
        private int[] ReconnectDelay { get; }

        public int OperationCode { get; set; } = 7;
        

        public EndOfStreamHandler(ILogger<EndOfStreamHandler> logger, Configuration configuration, IServiceProvider serviceProvider) {
            Logger = logger;
            Configuration = configuration;
            ServiceProvider = serviceProvider;

            ErrorCodes = [
                "Server Error",
                "Unknown Operation",
                "Invalid Payload",
                "Auth Failure",
                "Already Identified",
                "Rate Limited",
                "Restart",
                "Maintenance",
                "Timeout",
                "Already Subscribed",
                "Not Subscribed",
                "Insufficient Privilege",
                "Inactivity?"
            ];
            ReconnectDelay = [
                1000,
                -1,
                -1,
                -1,
                -1,
                3000,
                1000,
                300000,
                1000,
                -1,
                -1,
                1000,
                1000
            ];
        }

        public async Task Execute<Data>(SocketClient<WebSocketMessage> sender, Data message)
        {
            if (message is not EndOfStreamMessage obj || obj == null)
                return;
            
            var code = obj.Code - 4000;
            if (code >= 0 && code < ErrorCodes.Length)
                Logger.LogWarning($"Received end of stream message (reason: {ErrorCodes[code]}, code: {obj.Code}, message: {obj.Message}).");
            else
                Logger.LogWarning($"Received end of stream message (code: {obj.Code}, message: {obj.Message}).");
            
            await sender.DisconnectAsync();

            if (code >= 0 && code < ReconnectDelay.Length && ReconnectDelay[code] < 0) {
                Logger.LogError($"7tv client will remain disconnected due to a bad client implementation.");
                return;
            }

            if (string.IsNullOrWhiteSpace(Configuration.Seven?.UserId))
                return;

            var context = ServiceProvider.GetRequiredService<ReconnectContext>();
            await Task.Delay(ReconnectDelay[code]);

            //var base_url = "@" + string.Join(",", Configuration.Seven.SevenId.Select(sub => sub.Type + "<" + string.Join(",", sub.Condition?.Select(e => e.Key + "=" + e.Value) ?? new string[0]) + ">"));
            var base_url = $"@emote_set.*<object_id={Configuration.Seven.UserId.Trim()}>";
            string url = $"{SevenApiClient.WEBSOCKET_URL}{base_url}";
            Logger.LogDebug($"7tv websocket reconnecting to {url}.");

            await sender.ConnectAsync(url);
            if (context.SessionId != null) {
                await sender.Send(34, new ResumeMessage() { SessionId = context.SessionId });
                Logger.LogInformation("Resumed connection to 7tv websocket.");
            } else {
                Logger.LogDebug("7tv websocket session id not available.");
            }
        }
    }
}