using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using TwitchChatTTS.Seven.Socket.Context;
using TwitchChatTTS.Seven.Socket.Data;

namespace TwitchChatTTS.Seven.Socket.Handlers
{
    public class EndOfStreamHandler : IWebSocketHandler
    {
        private ILogger Logger { get; }
        private User User { get; }
        private IServiceProvider ServiceProvider { get; }
        private string[] ErrorCodes { get; }
        private int[] ReconnectDelay { get; }

        public int OperationCode { get; set; } = 7;


        public EndOfStreamHandler(ILogger logger, User user, IServiceProvider serviceProvider)
        {
            Logger = logger;
            User = user;
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
                Logger.Warning($"Received end of stream message (reason: {ErrorCodes[code]}, code: {obj.Code}, message: {obj.Message}).");
            else
                Logger.Warning($"Received end of stream message (code: {obj.Code}, message: {obj.Message}).");

            await sender.DisconnectAsync();

            if (code >= 0 && code < ReconnectDelay.Length && ReconnectDelay[code] < 0)
            {
                Logger.Error($"7tv client will remain disconnected due to a bad client implementation.");
                return;
            }

            if (string.IsNullOrWhiteSpace(User.SevenEmoteSetId))
                return;

            var context = ServiceProvider.GetRequiredService<ReconnectContext>();
            await Task.Delay(ReconnectDelay[code]);

            var base_url = $"@emote_set.*<object_id={User.SevenEmoteSetId}>";
            string url = $"{SevenApiClient.WEBSOCKET_URL}{base_url}";
            Logger.Debug($"7tv websocket reconnecting to {url}.");

            await sender.ConnectAsync(url);
            if (context.SessionId != null)
            {
                await sender.Send(34, new ResumeMessage() { SessionId = context.SessionId });
                Logger.Information("Resumed connection to 7tv websocket.");
            }
            else
            {
                Logger.Information("Resumed connection to 7tv websocket on a different session.");
            }
        }
    }
}