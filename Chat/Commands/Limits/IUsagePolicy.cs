namespace TwitchChatTTS.Chat.Commands.Limits
{
    public interface IUsagePolicy<K>
    {
        void Remove(string group, string policy);
        void Set(string group, string policy, int count, TimeSpan span);
        bool TryUse(K key, string group, string policy);
        public bool TryUse(K key, IEnumerable<string> groups, string policy);
    }
}