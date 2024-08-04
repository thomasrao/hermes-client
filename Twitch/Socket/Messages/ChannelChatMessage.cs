namespace TwitchChatTTS.Twitch.Socket.Messages
{
    public class ChannelChatMessage
    {
        public string BroadcasterUserId { get; set; }
        public string BroadcasterUserLogin { get; set; }
        public string BroadcasterUserName { get; set; }
        public string ChatterUserId { get; set; }
        public string ChatterUserLogin { get; set; }
        public string ChatterUserName { get; set; }
        public string MessageId { get; set; }
        public TwitchChatMessageInfo Message { get; set; }
        public string MessageType { get; set; }
        public TwitchBadge[] Badges { get; set; }
        public TwitchReplyInfo? Reply { get; set; }
        public string? ChannelPointsCustomRewardId { get; set; }
        public string? ChannelPointsAnimationId { get; set; }
    }

    public class TwitchChatMessageInfo
    {
        public string Text { get; set; }
        public TwitchChatFragment[] Fragments { get; set; }
    }

    public class TwitchChatFragment
    {
        public string Type { get; set; }
        public string Text { get; set; }
        public TwitchCheerInfo? Cheermote { get; set; }
        public TwitchEmoteInfo? Emote { get; set; }
        public TwitchMentionInfo? Mention { get; set; }
    }

    public class TwitchCheerInfo
    {
        public string Prefix { get; set; }
        public int Bits { get; set; }
        public int Tier { get; set; }
    }

    public class TwitchEmoteInfo
    {
        public string Id { get; set; }
        public string EmoteSetId { get; set; }
        public string OwnerId { get; set; }
        public string[] Format { get; set; }
    }

    public class TwitchMentionInfo
    {
        public string UserId { get; set; }
        public string UserName { get; set; }
        public string UserLogin { get; set; }
    }

    public class TwitchBadge
    {
        public string SetId { get; set; }
        public string Id { get; set; }
        public string Info { get; set; }
    }

    public class TwitchReplyInfo
    {
        public string ParentMessageId { get; set; }
        public string ParentMessageBody { get; set; }
        public string ParentUserId { get; set; }
        public string ParentUserName { get; set; }
        public string ParentUserLogin { get; set; }
        public string ThreadMessageId { get; set; }
        public string ThreadUserName { get; set; }
        public string ThreadUserLogin { get; set; }
    }
}