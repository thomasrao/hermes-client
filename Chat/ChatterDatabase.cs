namespace TwitchChatTTS.Chat
{
    public class ChatterDatabase
    {
        private readonly IDictionary<string, long> _chatters;
        //private readonly HashSet<long> _chatterIds;


        public ChatterDatabase()
        {
            _chatters = new Dictionary<string, long>();
            //_chatterIds = new HashSet<long>();
        }

        public void Add(string username, long chatterId)
        {
            // if (_chatterIds.TryGetValue(chatterId, out var _)) {
            //     // TODO: send message to update username for id.
            // } else
            //     _chatterIds.Add(chatterId);

            if (_chatters.ContainsKey(username))
                _chatters[username] = chatterId;
            else
                _chatters.Add(username, chatterId);
        }

        public void Clear()
        {
            _chatters.Clear();
        }

        public long? Get(string emoteName)
        {
            return _chatters.TryGetValue(emoteName, out var chatterId) ? chatterId : null;
        }

        public void Remove(string emoteName)
        {
            _chatters.Remove(emoteName);
        }
    }
}