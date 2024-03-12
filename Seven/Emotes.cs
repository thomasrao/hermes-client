using System.Collections.Concurrent;

namespace TwitchChatTTS.Seven
{
    public class EmoteCounter {
        public IDictionary<string, IDictionary<long, int>> Counters { get; set; }

        public EmoteCounter() {
            Counters = new ConcurrentDictionary<string, IDictionary<long, int>>();
        }

        public void Add(long userId, IEnumerable<string> emoteIds) {
            foreach (var emote in emoteIds) {
                if (Counters.TryGetValue(emote, out IDictionary<long, int>? subcounters)) {
                    if (subcounters.TryGetValue(userId, out int counter))
                        subcounters[userId] = counter + 1;
                    else
                        subcounters.Add(userId, 1);
                } else {
                    Counters.Add(emote, new ConcurrentDictionary<long, int>());
                    Counters[emote].Add(userId, 1);
                }
            }
        }

        public void Clear() {
            Counters.Clear();
        }

        public int Get(long userId, string emoteId) {
            if (Counters.TryGetValue(emoteId, out IDictionary<long, int>? subcounters)) {
                if (subcounters.TryGetValue(userId, out int counter))
                    return counter;
            }
            return -1;
        }
    }

    public class EmoteDatabase {
        private IDictionary<string, string> Emotes { get; }

        public EmoteDatabase() {
            Emotes = new Dictionary<string, string>();
        }

        public void Add(string emoteName, string emoteId) {
            if (Emotes.ContainsKey(emoteName))
                Emotes[emoteName] = emoteId;
            else
                Emotes.Add(emoteName, emoteId);
        }

        public void Clear() {
            Emotes.Clear();
        }

        public string? Get(string emoteName) {
            return Emotes.TryGetValue(emoteName, out string? emoteId) ? emoteId : null;
        }

        public void Remove(string emoteName) {
            if (Emotes.ContainsKey(emoteName))
                Emotes.Remove(emoteName);
        }
    }

    public class EmoteSet {
        public string Id { get; set; }
        public string Name { get; set; }
        public int Flags { get; set; }
        public bool Immutable { get; set; }
        public bool Privileged { get; set; }
        public IList<Emote> Emotes { get; set; }
        public int EmoteCount { get; set; }
        public int Capacity { get; set; }

    }

    public class Emote {
        public string Id { get; set; }
        public string Name { get; set; }
        public int Flags { get; set; }
    }
}