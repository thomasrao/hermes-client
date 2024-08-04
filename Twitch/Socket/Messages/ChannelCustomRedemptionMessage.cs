namespace TwitchChatTTS.Twitch.Socket.Messages
{
    public class ChannelCustomRedemptionMessage
    {
        public string BroadcasterUserId { get; set; }
        public string BroadcasterUserLogin { get; set; }
        public string BroadcasterUserName { get; set; }
        public string Id { get; set; }
        public string UserId { get; set; }
        public string UserLogin { get; set; }
        public string UserName { get; set; }
        public string Status { get; set; }
        public DateTime RedeemedAt { get; set; }
        public RedemptionReward Reward { get; set; }
    }

    public class RedemptionReward
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Prompt { get; set; }
        public int Cost { get; set; }
    }
}