namespace TwitchChatTTS.Twitch.Socket.Messages
{
    public class ChannelRaidMessage
    {
        public string FromBroadcasterUserId { get; set; }
        public string FromBroadcasterUserLogin { get; set; }
        public string FromBroadcasterUserName { get; set; }
        public string ToBroadcasterUserId { get; set; }
        public string ToBroadcasterUserLogin { get; set; }
        public string ToBroadcasterUserName { get; set; }
        public int Viewers { get; set; }
    }
}