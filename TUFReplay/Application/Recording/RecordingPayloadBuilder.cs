using TUFReplay.Domain.Activity;
using TUFReplay.Domain.ReplayData;

namespace TUFReplay.Application.Recording;

public static class RecordingPayloadBuilder
{
  public static RunRecord Apply(RunRecord run, RecordedRunPayload data)
  {
    run.NoFailMode = data.NoFailMode;
    run.GameplayStartSongPosition = data.GameplayStartSongPosition;
    run.LevelPitchPercent = data.LevelPitchPercent;
    run.EffectivePitch = data.EffectivePitch;
    run.InputCount = data.Inputs.Count;
    run.HitContextCount = data.HitContexts.Count;
    run.InputCsv = data.ToInputCsvBytes();
    run.HitContextCsv = data.ToHitContextCsvBytes();
    run.MetaJson = data.ToActivityMetaJson();
    return run;
  }
}
