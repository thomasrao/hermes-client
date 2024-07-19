using System.Security.Cryptography;
using System.Text;
using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;
using Serilog;
using TwitchChatTTS.OBS.Socket.Data;

namespace TwitchChatTTS.OBS.Socket.Handlers
{
    public class HelloHandler : IWebSocketHandler
    {
        private readonly Configuration _configuration;
        private readonly ILogger _logger;
        public int OperationCode { get; } = 0;

        public HelloHandler(Configuration configuration, ILogger logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task Execute<Data>(SocketClient<WebSocketMessage> sender, Data data)
        {
            if (data is not HelloMessage message || message == null)
                return;
            
            string? password = string.IsNullOrWhiteSpace(_configuration.Obs?.Password) ? null : _configuration.Obs.Password.Trim();
            _logger.Verbose("OBS websocket password: " + password);
            if (message.Authentication == null || string.IsNullOrEmpty(password))
            {
                await sender.Send(1, new IdentifyMessage(message.RpcVersion, null, 1023 | 262144));
                return;
            }

            var salt = message.Authentication.Salt;
            var challenge = message.Authentication.Challenge;
            _logger.Verbose("Salt: " + salt);
            _logger.Verbose("Challenge: " + challenge);

            string secret = password + salt;
            byte[] bytes = Encoding.UTF8.GetBytes(secret);
            string? hash = null;
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