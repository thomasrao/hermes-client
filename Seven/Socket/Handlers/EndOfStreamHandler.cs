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
        private readonly ILogger _logger;
        private readonly User _user;
        private readonly IServiceProvider _serviceProvider;
        private readonly string[] _errorCodes;
        private readonly int[] _reconnectDelay;

        public int OperationCode { get; } = 7;


        public EndOfStreamHandler(User user, IServiceProvider serviceProvider, ILogger logger)
        {
            _logger = logger;
            _user = user;
            _serviceProvider = serviceProvider;

            _errorCodes = [
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
            _reconnectDelay = [
                1000,
                -1,
                -1,
                -1,
                0,
                3000,
                1000,
                300000,
                1000,
                0,
                0,
                1000,
                1000
            ];
        }

        public async Task Execute<Data>(SocketClient<WebSocketMessage> sender, Data data)
        {
            if (data is not EndOfStreamMessage message || message == null)
                return;

            var code = message.Code - 4000;
            if (code >= 0 && code < _errorCodes.Length)
                _logger.Warning($"Received end of stream message (reason: {_errorCodes[code]}, code: {message.Code}, message: {message.Message}).");
            else
                _logger.Warning($"Received end of stream message (code: {message.Code}, message: {message.Message}).");

            await sender.DisconnectAsync();

            if (code >= 0 && code < _reconnectDelay.Length && _reconnectDelay[code] < 0)
            {
                _logger.Error($"7tv client will remain disconnected due to a bad client implementation.");
                return;
            }

            if (string.IsNullOrWhiteSpace(_user.SevenEmoteSetId))
            {
                _logger.Warning("Could not find the 7tv emote set id. Not reconnecting.");
                return;
            }

            var context = _serviceProvider.GetRequiredService<ReconnectContext>();
            if (_reconnectDelay[code] > 0)
                await Task.Delay(_reconnectDelay[code]);
            
            var manager = _serviceProvider.GetRequiredService<SevenManager>();
            await manager.Connect();

            if (context.SessionId != null)
            {
                await sender.Send(34, new ResumeMessage() { SessionId = context.SessionId });
                _logger.Debug("Resumed connection to 7tv websocket.");
            }
            else
            {
                _logger.Debug("Resumed connection to 7tv websocket on a different session.");
            }
        }
    }
}