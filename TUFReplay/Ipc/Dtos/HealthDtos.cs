namespace TUFReplay.Ipc.Dtos;

public sealed class HealthResponseDto
{
  public bool Ok;
  public string Mod;
  public int ServerVersion;

  public static HealthResponseDto Create()
  {
    return new HealthResponseDto
    {
      Ok = true,
      Mod = "TUFReplay",
      ServerVersion = 1,
    };
  }
}
