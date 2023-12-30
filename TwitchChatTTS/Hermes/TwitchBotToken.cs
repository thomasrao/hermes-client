[Serializable]
public class TwitchBotToken {
  public string client_id { get; set; }
  public string client_secret { get; set; }
  public string access_token { get; set; }
  public string refresh_token { get; set; }
  public string broadcaster_id { get; set; }
}