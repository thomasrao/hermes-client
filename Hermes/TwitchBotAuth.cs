public class TwitchBotAuth
{
    public string? UserId { get; set; }
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public string? BroadcasterId { get; set; }
    public long? ExpiresIn
    {
        get => _expiresIn;
        set
            { 
                _expiresIn = value;
                if (value != null)
                    ExpiresAt = DateTime.UtcNow + TimeSpan.FromSeconds((double) value);
            }
    }
    public DateTime ExpiresAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    private long? _expiresIn;
}