namespace TwitchChatTTS.Twitch.Redemptions
{
    public class Redemption
    {
        public string Id { get; set; }
        public string RedemptionId { get; set; }
        public string ActionName { get; set; }
        public int Order { get; set; }
        public bool State { get; set; }
    }
}