namespace TwitchChatTTS.Hermes
{
    public class TTSWordFilter
    {
        public string? Id { get; set; }
        public string? Search { get; set; }
        public string? Replace { get; set; }
        public string? UserId { get; set; }

        public bool IsRegex { get; set; }


        public TTSWordFilter() {
            IsRegex = true;
        }
    }
}