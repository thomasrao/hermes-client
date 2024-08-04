namespace TwitchChatTTS.Twitch.Socket.Messages
{
    public class ChannelChatClearUserMessage : ChannelChatClearMessage
    {
        public string TargetUserId { get; set; }
        public string TargetUserLogin { get; set; }
        public string TargetUserName { get; set; }
    }
}