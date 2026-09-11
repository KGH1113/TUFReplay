namespace TUFReplay.Shared.Ipc;

using TUFReplay.Replay.Models;

public sealed class HealthResponseDto
{
  public const int CurrentProtocolVersion = 7;

  public bool Ok;
  public string Mod;
  public string ModVersion;
  public int ProtocolVersion;
  public int ServerVersion;
  public string ReplayEngineId;
  public int ReplayFormatVersion;

  public static HealthResponseDto Create()
  {
    return new HealthResponseDto
    {
      Ok = true,
      Mod = "TUFReplay",
      ModVersion = Main.Instance.Version.ToString(),
      ProtocolVersion = CurrentProtocolVersion,
      ServerVersion = 1,
      ReplayEngineId = ReplayFormat.EngineId,
      ReplayFormatVersion = ReplayFormat.FormatVersion,
    };
  }
}
