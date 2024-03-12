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
        private IServiceProvider ServiceProvider { get; }
        private string[] ErrorCodes { get; }
        private int[] ReconnectDelay { get; }

        public int OperationCode { get; set; } = 7;
        

        public EndOfStreamHandler(ILogger<EndOfStreamHandler> logger, IServiceProvider serviceProvider) {
            Logger = logger;
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

            var context = ServiceProvider.GetRequiredService<ReconnectContext>();
            await Task.Delay(ReconnectDelay[code]);

            Logger.LogInformation($"7tv client reconnecting.");
            await sender.ConnectAsync($"{context.Protocol ?? "wss"}://{context.Url}");
            if (context.SessionId is null) {
                await sender.Send(33, new object());
            } else {
                await sender.Send(34, new ResumeMessage() {
                    SessionId = context.SessionId
                });
            }
        }
    }
}