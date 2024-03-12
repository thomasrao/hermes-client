using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;
using Microsoft.Extensions.Logging;
using TwitchChatTTS.Seven.Socket.Context;
using TwitchChatTTS.Seven.Socket.Data;

namespace TwitchChatTTS.Seven.Socket.Handlers
{
    public class SevenHelloHandler : IWebSocketHandler
    {
        private ILogger Logger { get; }
        private SevenHelloContext Context { get; }
        public int OperationCode { get; set; } = 1;

        public SevenHelloHandler(ILogger<SevenHelloHandler> logger, SevenHelloContext context) {
            Logger = logger;
            Context = context;
        }

        public async Task Execute<Data>(SocketClient<WebSocketMessage> sender, Data message)
        {
            if (message is not SevenHelloMessage obj || obj == null)
                return;

            if (sender is not SevenSocketClient seven || seven == null)
                return;
            
            seven.Connected = true;
            seven.ConnectionDetails = obj;

            // if (Context.Subscriptions == null || !Context.Subscriptions.Any()) {
            //     Logger.LogWarning("No subscriptions have been set for the 7tv websocket client.");
            //     return;
            // }

            //await Task.Delay(TimeSpan.FromMilliseconds(1000));
            //await sender.Send(33, new IdentifyMessage());
            //await Task.Delay(TimeSpan.FromMilliseconds(5000));
            //await sender.SendRaw("{\"op\":35,\"d\":{\"type\":\"emote_set.*\",\"condition\":{\"object_id\":\"64505914b9fc508169ffe7cc\"}}}");
            //await sender.SendRaw(File.ReadAllText("test.txt"));

            // foreach (var sub in Context.Subscriptions) {
            //     if (string.IsNullOrWhiteSpace(sub.Type)) {
            //         Logger.LogWarning("Non-existent or empty subscription type found on the 7tv websocket client.");
            //         continue;
            //     }

            //     Logger.LogDebug($"Subscription Type: {sub.Type} | Condition: {string.Join(", ", sub.Condition?.Select(e => e.Key + "=" + e.Value) ?? new string[0])}");
            //     await sender.Send(35, new SubscribeMessage() {
            //         Type = sub.Type,
            //         Condition = sub.Condition
            //     });
            // }
        }
    }
}