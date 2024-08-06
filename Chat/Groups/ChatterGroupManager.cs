
using System.Collections.Concurrent;
using HermesSocketLibrary.Requests.Messages;
using Serilog;

namespace TwitchChatTTS.Chat.Groups
{
    public class ChatterGroupManager : IChatterGroupManager
    {
        private readonly IDictionary<string, Group> _groups;
        private readonly IDictionary<long, ICollection<string>> _chatters;
        private readonly ILogger _logger;


        public ChatterGroupManager(ILogger logger)
        {
            _logger = logger;
            _groups = new ConcurrentDictionary<string, Group>();
            _chatters = new ConcurrentDictionary<long, ICollection<string>>();
        }

        public void Add(Group group)
        {
            _groups.Add(group.Name, group);
        }

        public void Add(long chatter, string groupName)
        {
            _chatters.Add(chatter, new List<string>() { groupName });
        }

        public void Add(long chatter, ICollection<string> groupNames)
        {
            if (_chatters.TryGetValue(chatter, out var list))
            {
                foreach (var group in groupNames)
                    list.Add(group);
            }
            else
                _chatters.Add(chatter, groupNames);
        }

        public void Clear()
        {
            _groups.Clear();
            _chatters.Clear();
        }

        public Group? Get(string groupName)
        {
            if (_groups.TryGetValue(groupName, out var group))
                return group;
            return null;
        }

        public IEnumerable<string> GetGroupNamesFor(long chatter)
        {
            if (_chatters.TryGetValue(chatter, out var groups))
                return groups.Select(g => _groups[g].Name);

            return Array.Empty<string>();
        }

        public int GetPriorityFor(long chatter)
        {
            if (!_chatters.TryGetValue(chatter, out var groups))
                return 0;

            return GetPriorityFor(groups);
        }

        public int GetPriorityFor(IEnumerable<string> groupNames)
        {
            var values = groupNames.Select(g => _groups.TryGetValue(g, out var group) ? group : null).Where(g => g != null);
            if (values.Any())
                return values.Max(g => g.Priority);
            return 0;
        }

        public bool Remove(long chatterId, string groupId)
        {
            if (_chatters.TryGetValue(chatterId, out var groups))
            {
                groups.Remove(groupId);
                _logger.Debug($"Removed chatter from group [chatter id: {chatterId}][group name: {_groups[groupId]}][group id: {groupId}]");
                return true;
            }
            _logger.Debug($"Failed to remove chatter from group [chatter id: {chatterId}][group name: {_groups[groupId]}][group id: {groupId}]");
            return false;
        }
    }
}