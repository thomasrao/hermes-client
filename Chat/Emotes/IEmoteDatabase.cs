namespace TwitchChatTTS.Chat.Emotes
{
    public interface IEmoteDatabase
    {
        void Add(string emoteName, string emoteId);
        void Clear();
        string? Get(string emoteName);
        void Remove(string emoteName);
    }
}