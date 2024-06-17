public class TTSVoice
{
    public string Label { get; set; }
    public int Value { get; set; }
    public string? Gender { get; set; }
    public string? Language { get; set; }
}

public class TTSChatterSelectedVoice
{
    public long ChatterId { get; set; }
    public string Voice { get; set; }
}