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
        private ILogger _logger { get; }
        public int OperationCode { get; set; } = 0;
        private HelloContext _context { get; }

        public HelloHandler(ILogger logger, HelloContext context)
        {
            _logger = logger;
            _context = context;
        }

        public async Task Execute<Data>(SocketClient<WebSocketMessage> sender, Data message)
        {
            if (message is not HelloMessage obj || obj == null)
                return;

            _logger.Verbose("OBS websocket password: " + _context.Password);
            if (obj.Authentication == null || string.IsNullOrWhiteSpace(_context.Password))
            {
                await sender.Send(1, new IdentifyMessage(obj.RpcVersion, string.Empty, 1023 | 262144));
                return;
            }

            var salt = obj.Authentication.Salt;
            var challenge = obj.Authentication.Challenge;
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
            await sender.Send(1, new IdentifyMessage(obj.RpcVersion, hash, 1023 | 262144));
        }
    }
}