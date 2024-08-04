namespace TwitchChatTTS.Twitch.Socket.Messages
{
    public class ChannelBanMessage
    {
        public string UserId { get; set; }
        public string UserLogin { get; set; }
        public string UserName { get; set; }
        public string BroadcasterUserId { get; set; }
        public string BroadcasterUserLogin { get; set; }
        public string BroadcasterUserName { get; set; }
        public string ModeratorUserId { get; set; }
        public string ModeratorUserLogin { get; set; }
        public string ModeratorUserName { get; set; }
        public string Reason { get; set; }
        public DateTime BannedAt { get; set; }
        public DateTime? EndsAt { get; set; }
        public bool IsPermanent { get; set; }
    }
}