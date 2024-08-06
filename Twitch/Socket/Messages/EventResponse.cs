namespace TwitchChatTTS.Twitch.Socket.Messages
{
    public class EventResponse<T>
    {
        public T[]? Data { get; set; }
        public int Total { get; set; }
        public int TotalCost { get; set; }
        public int MaxTotalCost { get; set; }
        public EventResponsePagination? Pagination { get; set; }
    }

    public class EventResponsePagination {
        public string Cursor { get; set; }
    }
}