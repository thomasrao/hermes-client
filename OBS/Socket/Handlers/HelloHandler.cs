using System.Security.Cryptography;
using System.Text;
using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;
using Microsoft.Extensions.Logging;
using TwitchChatTTS.OBS.Socket.Data;
using TwitchChatTTS.OBS.Socket.Context;

namespace TwitchChatTTS.OBS.Socket.Handlers
{
    public class HelloHandler : IWebSocketHandler
    {
        private ILogger Logger { get; }
        public int OperationCode { get; set; } = 0;
        private HelloContext Context { get; }

        public HelloHandler(ILogger<HelloHandler> logger, HelloContext context) {
            Logger = logger;
            Context = context;
        }

        public async Task Execute<Data>(SocketClient<WebSocketMessage> sender, Data message)
        {
            if (message is not HelloMessage obj || obj == null)
                return;

            Logger.LogTrace("OBS websocket password: " + Context.Password);
            if (obj.Authentication == null || Context.Password == null) // TODO: send re-identify message.
                return;
            
            var salt = obj.Authentication.Salt;
            var challenge = obj.Authentication.Challenge;
            Logger.LogTrace("Salt: " + salt);
            Logger.LogTrace("Challenge: " + challenge);
            
            string secret = Context.Password + salt;
            byte[] bytes = Encoding.UTF8.GetBytes(secret);
            string hash = null;
            using (var sha = SHA256.Create()) {
                bytes = sha.ComputeHash(bytes);
                hash = Convert.ToBase64String(bytes);

                secret = hash + challenge;
                bytes = Encoding.UTF8.GetBytes(secret);
                bytes = sha.ComputeHash(bytes);
                hash = Convert.ToBase64String(bytes);
            }

            Logger.LogTrace("Final hash: " + hash);
            await sender.Send(1, new IdentifyMessage(obj.RpcVersion, hash, 1023 | 262144));
        }
    }
}