using System.Diagnostics.CodeAnalysis;

[Serializable]
public class Account {
  [AllowNull]
  public string Id { get; set; }
  [AllowNull]
  public string Username { get; set; }
}