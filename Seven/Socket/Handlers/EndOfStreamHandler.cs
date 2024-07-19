using System.Net.WebSockets;
using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;
using TwitchChatTTS.Seven.Socket.Data;

namespace TwitchChatTTS.Seven.Socket.Handlers
{
    public class EndOfStreamHandler : IWebSocketHandler
    {
        public int OperationCode { get; } = 7;

        public async Task Execute<Data>(SocketClient<WebSocketMessage> sender, Data data)
        {
            if (data is not EndOfStreamMessage message || message == null)
                return;

            var code = message.Code - 4000;
            await sender.DisconnectAsync(new SocketDisconnectionEventArgs(WebSocketCloseStatus.Empty.ToString(), code.ToString()));
        }
    }
}