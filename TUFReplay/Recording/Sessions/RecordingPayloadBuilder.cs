using TUFReplay.Activity.Models;
using TUFReplay.Replay.Models;

namespace TUFReplay.Recording.Sessions;

public static class RecordingPayloadBuilder
{
  public static RunRecord Apply(RunRecord run, RecordedRunPayload data)
  {
    run.NoFailMode = data.NoFailMode;
    run.GameplayStartSongPosition = data.GameplayStartSongPosition;
    run.LevelPitchPercent = data.LevelPitchPercent;
    run.EffectivePitch = data.EffectivePitch;
    run.XAccuracy = data.XAccuracy;
    run.JudgmentDifficulty = data.JudgmentDifficulty;
    run.JudgmentCounts = data.JudgmentCounts ?? new JudgmentCounts();
    run.GameplayHash = data.GameplayHash == null ? null : (byte[])data.GameplayHash.Clone();
    run.GameplayHashVersion = data.GameplayHashVersion;
    run.InputCount = data.Inputs.Count;
    run.HitContextCount = data.HitContexts.Count;
    run.ReplayArtifact = null;
    run.ReplayUnavailableReason = null;
    if (!data.TryCreateArtifact(run.Id, out ReplayArtifact artifact))
      run.ReplayUnavailableReason = ReplayUnavailableReasons.CaptureIncomplete;
    else
      run.ReplayArtifact = artifact;
    return run;
  }
}
