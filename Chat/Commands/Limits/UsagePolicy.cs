using Serilog;

namespace TwitchChatTTS.Chat.Commands.Limits
{
    public class UsagePolicy<K> : IUsagePolicy<K> where K : notnull
    {
        private readonly ILogger _logger;

        private readonly UsagePolicyNode<K> _root;


        public UsagePolicy(ILogger logger)
        {
            _logger = logger;
            _root = new UsagePolicyNode<K>(string.Empty, null, null, logger);
        }


        public void Remove(string group, string policy)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(group, nameof(group));
            ArgumentException.ThrowIfNullOrWhiteSpace(policy, nameof(policy));

            string[] path = (group + '.' + policy).Split('.');
            _root.Remove(path);
        }

        public void Set(string group, string policy, int count, TimeSpan span)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(group, nameof(group));
            ArgumentException.ThrowIfNullOrWhiteSpace(policy, nameof(policy));
            if (count <= 0)
                throw new InvalidOperationException("Count cannot be 0 or lower.");
            if (span.TotalMilliseconds == 0)
                throw new InvalidOperationException("Time span cannot be 0 milliseconds.");

            string[] path = (group + '.' + policy).Split('.');
            _root.Set(path, count, span);
        }

        public bool TryUse(K key, string group, string policy)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(group, nameof(group));
            ArgumentException.ThrowIfNullOrWhiteSpace(policy, nameof(policy));

            string[] path = (group + '.' + policy).Split('.');
            UsagePolicyNode<K>? node = _root.Get(path);
            _logger.Debug($"Fetched policy node [is null: {node == null}]");
            if (node == null)
                return false;
            return node.TryUse(key, DateTime.UtcNow);
        }

        public bool TryUse(K key, IEnumerable<string> groups, string policy)
        {
            ArgumentNullException.ThrowIfNull(groups, nameof(groups));
            ArgumentException.ThrowIfNullOrWhiteSpace(policy, nameof(policy));

            foreach (string group in groups)
            {
                if (TryUse(key, group, policy))
                {
                    _logger.Debug($"Checking policy node [policy: {group}.{policy}][result: True]");
                    return true;
                }
                _logger.Debug($"Checking policy node [policy: {group}.{policy}][result: False]");
            }
            return false;
        }


        private class UsagePolicyLimit
        {
            public int Count { get; set; }
            public TimeSpan Span { get; set; }


            public UsagePolicyLimit(int count, TimeSpan span)
            {
                Count = count;
                Span = span;
            }
        }

        private class UserUsageData
        {
            public DateTime[] Uses { get; set; }
            public int Index { get; set; }

            public UserUsageData(int size, int index)
            {
                Uses = new DateTime[size];
                Index = index;
            }
        }

        private class UsagePolicyNode<T> where T : notnull
        {
            public string Name { get; set; }
            public UsagePolicyLimit? Limit { get; private set; }
            private UsagePolicyNode<T>? _parent { get; }
            private IDictionary<T, UserUsageData> _usages { get; }
            private IList<UsagePolicyNode<T>> _children { get; }
            private ILogger _logger;
            private object _lock { get; }

            public UsagePolicyNode(string name, UsagePolicyLimit? data, UsagePolicyNode<T>? parent, ILogger logger)
            {
                //ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
                Name = name;
                Limit = data;
                _parent = parent;
                _usages = new Dictionary<T, UserUsageData>();
                _children = new List<UsagePolicyNode<T>>();
                _logger = logger;
                _lock = new object();
            }


            public UsagePolicyNode<T>? Get(IEnumerable<string> path)
            {
                if (!path.Any())
                    return this;

                var nextName = path.First();
                var next = _children.FirstOrDefault(c => c.Name == nextName);
                if (next == null)
                    return this;
                return next.Get(path.Skip(1));
            }

            public UsagePolicyNode<T>? Remove(IEnumerable<string> path)
            {
                if (!path.Any())
                {
                    if (_parent == null)
                        throw new InvalidOperationException("Cannot remove root node");

                    _parent._children.Remove(this);
                    return this;
                }

                var nextName = path.First();
                var next = _children.FirstOrDefault(c => c.Name == nextName);
                _logger.Debug($"internal remove node [is null: {next == null}][path: {string.Join('.', path)}]");
                if (next == null)
                    return null;
                return next.Remove(path.Skip(1));
            }

            public void Set(IEnumerable<string> path, int count, TimeSpan span)
            {
                if (!path.Any())
                {
                    Limit = new UsagePolicyLimit(count, span);
                    return;
                }

                var nextName = path.First();
                var next = _children.FirstOrDefault(c => c.Name == nextName);
                _logger.Debug($"internal set node [is null: {next == null}][path: {string.Join('.', path)}]");
                if (next == null)
                {
                    next = new UsagePolicyNode<T>(nextName, null, this, _logger);
                    _children.Add(next);
                }
                next.Set(path.Skip(1), count, span);
            }

            public bool TryUse(T key, DateTime timestamp)
            {
                if (_parent == null)
                    return false;
                if (Limit == null || Limit.Count <= 0)
                    return _parent.TryUse(key, timestamp);

                UserUsageData? usage;
                lock (_lock)
                {
                    if (!_usages.TryGetValue(key, out usage))
                    {
                        usage = new UserUsageData(Limit.Count, 1 % Limit.Count);
                        usage.Uses[0] = timestamp;
                        _usages.Add(key, usage);
                        _logger.Debug($"internal use node create");
                        return true;
                    }

                    if (usage.Uses.Length != Limit.Count)
                    {
                        var sizeDiff = Math.Max(0, usage.Uses.Length - Limit.Count);
                        var temp = usage.Uses.Skip(sizeDiff);
                        var tempSize = usage.Uses.Length - sizeDiff;
                        usage.Uses = temp.Union(new DateTime[Math.Max(0, Limit.Count - tempSize)]).ToArray();
                    }
                }

                // Attempt on parent node if policy has been abused.
                if (timestamp - usage.Uses[usage.Index] < Limit.Span)
                {
                    _logger.Debug($"internal use node spam [span: {(timestamp - usage.Uses[usage.Index]).TotalMilliseconds}][index: {usage.Index}]");
                    return _parent.TryUse(key, timestamp);
                }

                _logger.Debug($"internal use node normal [span: {(timestamp - usage.Uses[usage.Index]).TotalMilliseconds}][index: {usage.Index}]");
                lock (_lock)
                {
                    usage.Uses[usage.Index] = timestamp;
                    usage.Index = (usage.Index + 1) % Limit.Count;
                }

                return true;
            }
        }
    }
}