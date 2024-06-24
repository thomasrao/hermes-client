namespace TwitchChatTTS.Twitch.Redemptions
{
    public class RedeemableAction
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public IDictionary<string, string> Data { get; set; }
    }
}