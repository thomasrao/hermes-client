namespace TwitchChatTTS.Chat.Groups.Permissions
{
    public class GroupPermission
    {
        public string Id { get; set; }
        public string GroupId { get; set; }
        public string Path { get; set; }
        public bool? Allow { get; set; }
    }
}