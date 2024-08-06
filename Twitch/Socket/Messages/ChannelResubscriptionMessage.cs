namespace TwitchChatTTS.Twitch.Socket.Messages
{
    public class ChannelResubscriptionMessage : ChannelSubscriptionData
    {
        public TwitchChatMessageInfo Message { get; set; }
        public int CumulativeMonths { get; set; }
        public int StreakMonths { get; set; }
        public int DurationMonths { get; set; }
    }
}