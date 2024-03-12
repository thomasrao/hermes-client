namespace TwitchChatTTS.Seven.Socket.Data
{
    public class ChangeMapMessage
    {
        public object Id { get; set; }
        public byte Kind { get; set; }
        public bool? Contextual { get; set; }
        public object Actor { get; set; }
        public IEnumerable<ChangeField>? Added { get; set; }
        public IEnumerable<ChangeField>? Updated { get; set; }
        public IEnumerable<ChangeField>? Removed { get; set; }
        public IEnumerable<ChangeField>? Pushed { get; set; }
        public IEnumerable<ChangeField>? Pulled { get; set; }
    }

    public class ChangeField {
        public string Key { get; set; }
        public int? Index { get; set; }
        public bool Nested { get; set; }
        public object OldValue { get; set; }
        public object Value { get; set; }
    }

    public class EmoteField {
        public string Id { get; set; }
        public string Name { get; set; }
        public string ActorId { get; set; }
        public int Flags { get; set; }
    }
}