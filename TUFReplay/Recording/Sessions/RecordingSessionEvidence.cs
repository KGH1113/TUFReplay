using TUFReplay.Recording.Capture;
using TUFReplay.Recording.Input;
using static TUFReplay.Recording.Telemetry.RecordingRuntimeTelemetry;

namespace TUFReplay.Recording.Sessions;

public partial class RecordingSession
{
  private IRecordingEvidenceSink _evidenceSink;
  private int _evidenceCommittedHits;
  private int _evidenceInputs;
  private int _evidenceHits;
  private double _evidenceRate = double.NaN;
  private bool _evidenceNoFail;
  private int _evidenceDifficulty = -1;
  private int _evidenceHoldBehavior = -1;

  /// <summary>Attach before the first input. Late attachment never backfills a local replay.</summary>
  public bool AttachEvidence(IRecordingEvidenceSink sink)
  {
    lock (_lock)
    {
      if (!IsRecording || IsCapturingInput || Data.Inputs.Count != 0 || Data.HitContexts.Count != 0
          || _evidenceSink != null || sink == null) return false;
      _evidenceCommittedHits = 0;
      _evidenceInputs = 0; _evidenceHits = 0;
      _evidenceRate = double.NaN;
      _evidenceHoldBehavior = (int)Persistence.holdBehavior;
      Data.SubmissionHoldBehavior = _evidenceHoldBehavior;
      Data.GameVersion = UnityEngine.Application.version;
      _evidenceSink = sink;
      return true;
    }
  }

  public void LinkSubmissionRun(System.Guid runId)
  {
    lock (_lock)
      Data.SubmissionRunId = runId.ToString();
  }

  /// <summary>Called after the winning hit postfix, so its resolved judgment is immutable.</summary>
  public string FinishEvidence(IRecordingEvidenceSink expected)
  {
    lock (_lock)
    {
      if (_evidenceSink != expected || expected == null) return null;
      if (!Data.WonTimeUs.HasValue) { AbortEvidenceLocked("run_not_cleared"); return null; }
      CommitEvidenceHitLocked();
      RecordInputTracker.CopyDiagnosticsTo(Data);
      RefreshNoFailModeLocked();
      RefreshPitchLocked();
      WriteEvidenceStateLocked(RecordingStateKind.RecorderHealth);
      if (Data.JudgmentDifficulty != TUFReplay.Activity.Models.RunJudgmentDifficulty.Strict
          || Data.InputOverflowDropped > 0 || Data.InputUnmappedEvents > 0
          || Data.InputReadFailures > 0 || Data.InputDegradedEvents > 0)
      {
        AbortEvidenceLocked("recorder_health_failed");
        return null;
      }
      _evidenceSink = null;
      Data.SubmissionKeyCount = SubmissionKeyCount.Count(Data.Inputs, Data.WonTimeUs.Value);
      return Data.ToActivityMetaJson(_evidenceInputs, _evidenceHits, Data.WonTimeUs, System.DateTime.UtcNow.ToString("O"));
    }
  }

  public void AbortEvidence(string reason)
  {
    lock (_lock) AbortEvidenceLocked(reason);
  }

  private void CommitEvidenceHitLocked()
  {
    if (_evidenceSink == null || _evidenceCommittedHits >= Data.HitContexts.Count) return;
    var hit = Data.HitContexts[Data.HitContexts.Count - 1];
    if (!Data.WonTimeUs.HasValue || hit.TimeUs <= Data.WonTimeUs.Value)
    {
      _evidenceSink.Write(hit);
      _evidenceHits++;
    }
    _evidenceCommittedHits = Data.HitContexts.Count;
  }

  private void AbortEvidenceLocked(string reason)
  {
    _evidenceSink?.Invalidate(reason);
    _evidenceSink = null;
    _evidenceCommittedHits = 0;
  }

  private void WriteEvidenceStateLocked(RecordingStateKind state)
  {
    _evidenceSink?.Write(new RecordingStateRecord(state, Data.WonTimeUs ?? CurrentTimelineTimeUsLocked(),
      EffectiveTimelineRateLocked(), Data.NoFailMode, (int?)Data.JudgmentDifficulty ?? -1,
      Data.InputOverflowDropped, Data.InputUnmappedEvents, (int)Persistence.holdBehavior));
  }

  private void ObserveEvidenceSettingsLocked(double rate)
  {
    if (_evidenceSink == null || Data.WonTimeUs.HasValue) return;
    bool noFail = IsNoFailModeActive();
    int difficulty = (int?)GetCurrentJudgmentDifficulty() ?? -1;
    int holdBehavior = (int)Persistence.holdBehavior;
    if (difficulty != (int)TUFReplay.Activity.Models.RunJudgmentDifficulty.Strict)
    {
      AbortEvidenceLocked("strict_required");
      return;
    }
    if (_evidenceRate == rate && _evidenceNoFail == noFail && _evidenceDifficulty == difficulty
        && _evidenceHoldBehavior == holdBehavior) return;
    _evidenceRate = rate; _evidenceNoFail = noFail; _evidenceDifficulty = difficulty;
    _evidenceHoldBehavior = holdBehavior;
    _evidenceSink.Write(new RecordingStateRecord(RecordingStateKind.RuntimeSettings,
      CurrentTimelineTimeUsLocked(), rate, noFail, difficulty, holdBehavior: holdBehavior));
  }
}
