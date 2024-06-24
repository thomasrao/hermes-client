using System.Security.Cryptography;
using System.Text;
using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;
using Serilog;
using TwitchChatTTS.OBS.Socket.Data;
using TwitchChatTTS.OBS.Socket.Context;

namespace TwitchChatTTS.OBS.Socket.Handlers
{
    public class HelloHandler : IWebSocketHandler
    {
        private readonly HelloContext _context;
        private readonly ILogger _logger;
        public int OperationCode { get; } = 0;

        public HelloHandler(HelloContext context, ILogger logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task Execute<Data>(SocketClient<WebSocketMessage> sender, Data data)
        {
            if (data is not HelloMessage message || message == null)
                return;

            _logger.Verbose("OBS websocket password: " + _context.Password);
            if (message.Authentication == null || string.IsNullOrWhiteSpace(_context.Password))
            {
                await sender.Send(1, new IdentifyMessage(message.RpcVersion, string.Empty, 1023 | 262144));
                return;
            }

            var salt = message.Authentication.Salt;
            var challenge = message.Authentication.Challenge;
            _logger.Verbose("Salt: " + salt);
            _logger.Verbose("Challenge: " + challenge);

            string secret = _context.Password + salt;
            byte[] bytes = Encoding.UTF8.GetBytes(secret);
            string hash = null;
            using (var sha = SHA256.Create())
            {
                bytes = sha.ComputeHash(bytes);
                hash = Convert.ToBase64String(bytes);

                secret = hash + challenge;
                bytes = Encoding.UTF8.GetBytes(secret);
                bytes = sha.ComputeHash(bytes);
                hash = Convert.ToBase64String(bytes);
            }

            _logger.Verbose("Final hash: " + hash);
            await sender.Send(1, new IdentifyMessage(message.RpcVersion, hash, 1023 | 262144));
        }
    }
}