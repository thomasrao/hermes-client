namespace TwitchChatTTS.Chat.Groups
{
    public interface IChatterGroupManager
    {
        void Add(Group group);
        void Add(long chatter, string group);
        void Add(long chatter, ICollection<string> groupIds);
        void Clear();
        Group? Get(string groupId);
        IEnumerable<string> GetGroupNamesFor(long chatter);
        int GetPriorityFor(long chatter);
        int GetPriorityFor(IEnumerable<string> groupIds);
        bool Remove(long chatter, string groupId);
    }
}