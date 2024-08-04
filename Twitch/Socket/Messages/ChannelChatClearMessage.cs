namespace TwitchChatTTS.Twitch.Socket.Messages
{
    public class ChannelChatClearMessage
    {
        public string BroadcasterUserId { get; set; }
        public string BroadcasterUserLogin { get; set; }
        public string BroadcasterUserName { get; set; }
    }
}