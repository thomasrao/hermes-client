namespace TwitchChatTTS.Twitch.Socket.Messages
{
    public class ChannelSubscriptionMessage
    {
        public string BroadcasterUserId { get; set; }
        public string BroadcasterUserLogin { get; set; }
        public string BroadcasterUserName { get; set; }
        public string ChatterUserId { get; set; }
        public string ChatterUserLogin { get; set; }
        public string ChatterUserName { get; set; }
        public string Tier { get; set; }
        public TwitchChatMessageInfo Message { get; set; }
        public int CumulativeMonths { get; set; }
        public int StreakMonths { get; set; }
        public int DurationMonths { get; set; }
    }
}