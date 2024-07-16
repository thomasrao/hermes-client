namespace TwitchChatTTS.Chat.Emotes
{
    public class EmoteDatabase : IEmoteDatabase
    {
        private readonly IDictionary<string, string> _emotes;
        public IDictionary<string, string> Emotes { get => _emotes.AsReadOnly(); }

        public EmoteDatabase()
        {
            _emotes = new Dictionary<string, string>();
        }

        public void Add(string emoteName, string emoteId)
        {
            if (_emotes.ContainsKey(emoteName))
                _emotes[emoteName] = emoteId;
            else
                _emotes.Add(emoteName, emoteId);
        }

        public void Clear()
        {
            _emotes.Clear();
        }

        public string? Get(string emoteName)
        {
            return _emotes.TryGetValue(emoteName, out string? emoteId) ? emoteId : null;
        }

        public void Remove(string emoteName)
        {
            _emotes.Remove(emoteName);
        }
    }

    public class EmoteSet
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public int Flags { get; set; }
        public bool Immutable { get; set; }
        public bool Privileged { get; set; }
        public IList<Emote> Emotes { get; set; }
        public int EmoteCount { get; set; }
        public int Capacity { get; set; }
    }

    public class Emote
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public int Flags { get; set; }
    }
}