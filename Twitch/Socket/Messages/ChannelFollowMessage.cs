namespace TwitchChatTTS.Twitch.Socket.Messages
{
    public class ChannelFollowMessage
    {
        public string BroadcasterUserId { get; set; }
        public string BroadcasterUserLogin { get; set; }
        public string BroadcasterUserName { get; set; }
        public string UserId { get; set; }
        public string UserLogin { get; set; }
        public string UserName { get; set; }
        public DateTime FollowedAt { get; set; }
    }
}