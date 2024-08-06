namespace TwitchChatTTS.Twitch.Socket.Messages
{
    public class ChannelAdBreakMessage
    {
        public string DurationSeconds { get; set; }
        public DateTime StartedAt { get; set; }
        public string IsAutomatic { get; set; }
        public string BroadcasterUserId { get; set; }
        public string BroadcasterUserLogin { get; set; }
        public string BroadcasterUserName { get; set; }
        public string RequesterUserId { get; set; }
        public string RequesterUserLogin { get; set; }
        public string RequesterUserName { get; set; }
    }
}