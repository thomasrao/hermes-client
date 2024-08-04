namespace TwitchChatTTS.Twitch.Socket.Messages
{
    public class ChannelChatDeleteMessage : ChannelChatClearUserMessage
    {
        public string MessageId { get; set; }
    }
}