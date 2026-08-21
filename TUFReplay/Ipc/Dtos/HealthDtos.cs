namespace TUFReplay.Ipc.Dtos;

public sealed class HealthResponseDto
{
  public const int CurrentProtocolVersion = 6;

  public bool Ok;
  public string Mod;
  public string ModVersion;
  public int ProtocolVersion;
  public int ServerVersion;

  public static HealthResponseDto Create()
  {
    return new HealthResponseDto
    {
      Ok = true,
      Mod = "TUFReplay",
      ModVersion = Main.Instance.Version.ToString(),
      ProtocolVersion = CurrentProtocolVersion,
      ServerVersion = 1,
    };
  }
}
