using System.Collections.Generic;
using TUFReplay.Activity.Models;
using TUFReplay.Composition;
using TUFReplay.ModApi.Models;
using TUFReplay.ModApi.Recording;
using TUFReplay.Recording.Activity;

namespace TUFReplay.Recording.Sessions;

public partial class RecordingFeature
{
  private bool PrepareActivityRun(int levelTileCount)
  {
    if (!Session.IsRecording)
      return false;
    if (_currentRun != null)
      return true;

    if (!_calibrationRun && !OpenActivityLevel(levelTileCount))
      return false;

    int startTile = RecordingSession.GetCurrentTile();
    _currentRun = _calibrationRun
      ? CalibrationRunDraftFactory.Create(Session.Data, startTile, levelTileCount)
      : _activity.CreateRunDraft(Session.Data, startTile, levelTileCount);
    // Other mods save their state as the run starts, before its first recorded input.
    if (_currentRun != null && !_calibrationRun)
      _runStates = RunStateCapture.Begin(_currentRun.Id);
    return _currentRun != null;
  }

  private IReadOnlyList<RunStateRecord> CloseRunStates()
  {
    RunStateCapture capture = _runStates;
    _runStates = null;
    return capture?.Close();
  }

  private void DiscardRunStates()
  {
    _runStates?.Discard();
    _runStates = null;
  }

  private bool OpenActivityLevel(int levelTileCount)
  {
    string levelPath = CanonicalLevelPath();
    if (levelPath == null)
      return false;

    if (_activity.OpenLevel(levelPath, Session.TufLevelId, levelTileCount, _gameplayHash, _gameplayHashVersion))
      return true;

    Main.Instance.Log("[Recording] Activity database is busy; recording will retry when gameplay starts.");
    return false;
  }

  private RunRecord CompleteCalibrationRun(string result, int? lastTile)
  {
    if (_currentRun == null || _runSaved)
      return null;
    _runSaved = true;
    return Session.CompleteRunRecord(_currentRun, lastTile, result);
  }

  private bool SaveActivityRun(string result, int? lastTile)
  {
    if (_runSaved)
      return false;
    if (_currentRun == null)
      return false;

    RunRecord run = _currentRun;
    var completed = Session.CompleteRunPayload(run, lastTile, result);
    _runSaved = true;
    IReadOnlyList<RunStateRecord> runStates = CloseRunStates();

    if (run.InputCount <= 0 && completed.GetInputFailureReason() == null)
    {
      Main.Instance.Log("[Recording] Skipped activity run without native input. result=" + result);
      return false;
    }

    if (!run.LastTile.HasValue || run.LastTile.Value <= run.StartTile)
    {
      Main.Instance.Log(
        "[Recording] Skipped activity run without forward progress. result="
          + result
          + ", startTile="
          + run.StartTile
          + ", lastTile="
          + (run.LastTile.HasValue ? run.LastTile.Value.ToString() : "null")
      );
      return false;
    }

    run.RunStates = runStates;
    var microphone = FeatureRegistry.MicrophoneRecording;
    _runPersistence = _activity.SaveRun(
      run,
      completed,
      () => microphone?.NotifyRunPersisted(run.Id),
      exception =>
      {
        microphone?.NotifyRunPersistenceFailed(run.Id);
        Main.Instance?.Log("[Recording] Activity save failed. runId=" + run.Id + ", error=" + exception.Message);
      }
    );

    Main.Instance.Log(
      "[Recording] Queued activity run. result="
        + result
        + ", startTile="
        + run.StartTile
        + ", lastTile="
        + (run.LastTile.HasValue ? run.LastTile.Value.ToString() : "null")
        + ", inputs="
        + run.InputCount
        + ", hitContexts="
        + run.HitContextCount
    );
    return true;
  }
}
