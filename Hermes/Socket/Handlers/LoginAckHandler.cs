using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;
using HermesSocketLibrary.Socket.Data;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace TwitchChatTTS.Hermes.Socket.Handlers
{
    public class LoginAckHandler : IWebSocketHandler
    {
        private IServiceProvider _serviceProvider;
        private ILogger _logger;
        public int OperationCode { get; set; } = 2;

        public LoginAckHandler(IServiceProvider serviceProvider, ILogger logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task Execute<Data>(SocketClient<WebSocketMessage> sender, Data message)
        {
            if (message is not LoginAckMessage obj || obj == null)
                return;

            if (sender is not HermesSocketClient client)
                return;

            if (obj.AnotherClient)
            {
                _logger.Warning("Another client has connected to the same account.");
            }
            else
            {
                var user = _serviceProvider.GetRequiredService<User>();
                client.UserId = obj.UserId;
                _logger.Information($"Logged in as {user.TwitchUsername} (id: {client.UserId}).");
            }

            await client.Send(3, new RequestMessage()
            {
                Type = "get_tts_voices",
                Data = null
            });

            var token = _serviceProvider.GetRequiredService<User>();
            await client.Send(3, new RequestMessage()
            {
                Type = "get_tts_users",
                Data = new Dictionary<string, object>() { { "user", token.HermesUserId } }
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