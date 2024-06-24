using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;
using HermesSocketLibrary.Socket.Data;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace TwitchChatTTS.Hermes.Socket.Handlers
{
    public class LoginAckHandler : IWebSocketHandler
    {
        private readonly User _user;
        private readonly ILogger _logger;
        public int OperationCode { get; } = 2;

        public LoginAckHandler(User user, ILogger logger)
        {
            _user = user;
            _logger = logger;
        }

        public async Task Execute<Data>(SocketClient<WebSocketMessage> sender, Data data)
        {
            if (data is not LoginAckMessage message || message == null)
                return;

            if (sender is not HermesSocketClient client)
                return;

            if (message.AnotherClient)
            {
                _logger.Warning("Another client has connected to the same account.");
            }
            else
            {
                client.UserId = message.UserId;
                _logger.Information($"Logged in as {_user.TwitchUsername}.");
            }

            await client.Send(3, new RequestMessage()
            {
                Type = "get_tts_voices",
                Data = null
            });

            await client.Send(3, new RequestMessage()
            {
                Type = "get_tts_users",
                Data = new Dictionary<string, object>() { { "user", _user.HermesUserId } }
            });

            await client.Send(3, new RequestMessage()
            {
                Type = "get_chatter_ids",
                Data = null
            });

            await client.Send(3, new RequestMessage()
            {
                Type = "get_emotes",
                Data = null
            });
        }
    }
}