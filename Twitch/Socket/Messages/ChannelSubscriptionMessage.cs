namespace TwitchChatTTS.Twitch.Socket.Messages
{
    public class ChannelSubscriptionData
    {
        public string? UserId { get; set; }
        public string? UserLogin { get; set; }
        public string? UserName { get; set; }
        public string BroadcasterUserId { get; set; }
        public string BroadcasterUserLogin { get; set; }
        public string BroadcasterUserName { get; set; }
        public string Tier { get; set; }
    }

    public class ChannelSubscriptionMessage : ChannelSubscriptionData
    {
        public bool IsGifted { get; set; }
    }
}