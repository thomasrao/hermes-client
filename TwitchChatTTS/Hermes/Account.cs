using System.Diagnostics.CodeAnalysis;

[Serializable]
public class Account {
  [AllowNull]
  public string id { get; set; }
  [AllowNull]
  public string username { get; set; }
}