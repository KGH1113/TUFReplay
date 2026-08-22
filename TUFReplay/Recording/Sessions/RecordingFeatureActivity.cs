using TUFReplay.Activity.Models;
using TUFReplay.Composition;
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
    return _currentRun != null;
  }

  private bool OpenActivityLevel(int levelTileCount)
  {
    string levelPath = CanonicalLevelPath();
    if (levelPath == null)
      return false;

    if (
      _activity.OpenLevel(
        levelPath,
        Session.TufLevelId,
        levelTileCount,
        _gameplayHash,
        _gameplayHashVersion,
        _legacyGameplayHash,
        _legacyGameplayHashVersion
      )
    )
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

    RunRecord run = Session.CompleteRunRecord(_currentRun, lastTile, result);
    _runSaved = true;

    if (run.InputCount <= 0)
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

    _activity.SaveRun(run);
    FeatureRegistry.MicrophoneRecording?.NotifyRunPersisted(run.Id);

    Main.Instance.Log(
      "[Recording] Saved activity run. result="
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
