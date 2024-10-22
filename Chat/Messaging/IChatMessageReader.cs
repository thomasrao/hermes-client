using TwitchChatTTS.Twitch.Socket;
using TwitchChatTTS.Twitch.Socket.Messages;

namespace TwitchChatTTS.Chat.Messaging
{
    public interface IChatMessageReader
    {
        Task Read(TwitchWebsocketClient sender, long broadcasterId, long? chatterId, string? chatterLogin, string? messageId, TwitchReplyInfo? reply, TwitchChatFragment[] fragments, int priority);
    }
}