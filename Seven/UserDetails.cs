namespace TwitchChatTTS.Seven
{
    public class UserDetails
    {
        public string Id { get; set; }
        public string Platform { get; set; }
        public string Username { get; set; }
        public int EmoteCapacity { get; set; }
        public int? EmoteSetId { get; set; }
        public EmoteSet EmoteSet { get; set; }
        public SevenUser User { get; set; }
    }

    public class SevenUser
    {
        public string Id { get; set; }
        public string Username { get; set; }
    }
}