namespace TwitchChatTTS.Chat.Groups.Permissions
{
    public interface IGroupPermissionManager
    {
        void Set(string path, bool? allow);
        bool? CheckIfAllowed(string path);
        bool? CheckIfAllowed(IEnumerable<string> groups, string path);
        void Clear();
        bool Remove(string path);
    }
}