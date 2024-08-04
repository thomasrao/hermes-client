namespace TwitchChatTTS.Chat.Commands.Limits
{
    public interface ICommandLimitManager
    {

        bool HasReachedLimit(long chatterId, string name, string group);
        void RemoveUsageLimit(string name, string group);
        void SetUsageLimit(int count, TimeSpan span, string name, string group);
        bool TryUse(long chatterId, string name, string group);
    }

    public class CommandLimitManager : ICommandLimitManager
    {
        // group + name -> chatter id -> usage
        private readonly IDictionary<string, IDictionary<long, Usage>> _usages;
        // group + name -> limit
        private readonly IDictionary<string, Limit> _limits;


        public CommandLimitManager()
        {
            _usages = new Dictionary<string, IDictionary<long, Usage>>();
            _limits = new Dictionary<string, Limit>();
        }


        public bool HasReachedLimit(long chatterId, string name, string group)
        {
            throw new NotImplementedException();
        }

        public void RemoveUsageLimit(string name, string group)
        {
            throw new NotImplementedException();
        }

        public void SetUsageLimit(int count, TimeSpan span, string name, string group)
        {
            throw new NotImplementedException();
        }

        public bool TryUse(long chatterId, string name, string group)
        {
            var path = $"{group}.{name}";
            if (!_limits.TryGetValue(path, out var limit))
                return true;

            if (!_usages.TryGetValue(path, out var groupUsage))
            {
                groupUsage = new Dictionary<long, Usage>();
                _usages.Add(path, groupUsage);
            }

            if (!groupUsage.TryGetValue(chatterId, out var usage))
            {
                usage = new Usage()
                {
                    Usages = new long[limit.Count],
                    Index = 0
                };
                groupUsage.Add(chatterId, usage);
            }

            int first = (usage.Index + 1) % limit.Count;
            long timestamp = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
            if (timestamp - usage.Usages[first] < limit.Span)
            {
                return false;
            }

            usage.Usages[usage.Index] = timestamp;
            usage.Index = first;
            return true;
        }

        private class Usage
        {
            public long[] Usages { get; set; }
            public int Index { get; set; }
        }

        private struct Limit
        {
            public int Count { get; set; }
            public int Span { get; set; }
        }
    }
}