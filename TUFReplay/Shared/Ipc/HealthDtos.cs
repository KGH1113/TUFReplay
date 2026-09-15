namespace TUFReplay.Shared.Ipc;

using TUFReplay.Replay.Models;
using TUFReplay.Shared.Build;

public sealed class HealthResponseDto
{
  public const int CurrentProtocolVersion = 7;

  public bool Ok;
  public string Mod;
  public string ModVersion;
  public int ProtocolVersion;
  public int ServerVersion;
  public string BuildFlavor;
  public int AutoSubmissionProtocolVersion;
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
      BuildFlavor = TUFReplayBuildFlavor.Name,
      AutoSubmissionProtocolVersion = TUFReplayBuildFlavor.AutoSubmissionProtocolVersion,
      ReplayEngineId = ReplayFormat.EngineId,
      ReplayFormatVersion = ReplayFormat.FormatVersion,
    };
  }
}
