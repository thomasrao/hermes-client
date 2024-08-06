namespace TwitchChatTTS.Twitch.Socket.Messages
{
    public class ChannelSubscriptionGiftMessage : ChannelSubscriptionData
    {
        public int Total { get; set; }
        public int? CumulativeTotal { get; set; }
        public bool IsAnonymous { get; set; }
    }
}