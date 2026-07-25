using System;
using System.IO;
using TUFReplay.Infrastructure.Unity;
using UnityEngine;

namespace TUFReplay.Features.Calibration;

internal sealed class MicrophoneCalibrationLevel
{
  private const double OpenTimeoutSeconds = 30d;

  private byte[] _gameplayHash;
  private double _openStartedAt;

  public string Path { get; private set; }

  public string ReferenceWaveformPath =>
    System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path) ?? string.Empty, "calibration_old.waveform");

  public bool TryPrepare(string payloadPath)
  {
    string levelPath = System.IO.Path.Combine(payloadPath, "Assets", "calibration", "level.adofai");
    string assetDirectory = System.IO.Path.GetDirectoryName(levelPath) ?? string.Empty;
    if (
      !File.Exists(levelPath)
      || !File.Exists(System.IO.Path.Combine(assetDirectory, "calibration_old.ogg"))
      || !File.Exists(System.IO.Path.Combine(assetDirectory, "calibration_old.waveform"))
    )
      return false;
    Path = LevelPathIdentity.Canonicalize(levelPath);
    return true;
  }

  public void Open()
  {
    _openStartedAt = Time.realtimeSinceStartupAsDouble;
    ReplayLevelOpenService.OpenEditor(Path);
  }

  public bool HasOpenTimedOut() => Time.realtimeSinceStartupAsDouble - _openStartedAt > OpenTimeoutSeconds;

  public bool IsEditorReady()
  {
    scnEditor editor = scnEditor.instance;
    if (
      editor == null
      || !editor.initialized
      || editor.isLoading
      || !LevelPathIdentity.Equals(Path, LevelPathIdentity.Current())
      || !GameplayChartHash.TryCompute(editor.levelData, out byte[] currentHash, out _)
    )
      return false;
    _gameplayHash = currentHash;
    return true;
  }

  public bool IsCurrent() =>
    GameplayChartHash.IsSupported(GameplayChartHash.Version, _gameplayHash)
    && GameplayChartHash.TryComputeCurrent(out byte[] currentHash, out _)
    && GameplayChartHash.Equals(_gameplayHash, currentHash);

  public void Reset()
  {
    Path = null;
    _gameplayHash = null;
    _openStartedAt = 0d;
  }

  public static bool IsGameplayActive()
  {
    if (string.Equals(ADOBase.sceneName, "scnEditor", StringComparison.Ordinal))
      return scnEditor.instance?.playMode == true;

    bool gameplayScene =
      string.Equals(ADOBase.sceneName, "scnGame", StringComparison.Ordinal)
      || string.Equals(ADOBase.sceneName, "scnCLS", StringComparison.Ordinal)
      || string.Equals(ADOBase.sceneName, "scnCalibration", StringComparison.Ordinal)
      || string.Equals(ADOBase.sceneName, "scnMinesweeper", StringComparison.Ordinal);
    if (!gameplayScene || ADOBase.controller == null)
      return false;

    States state = ADOBase.controller.state;
    return state == States.Countdown || state == States.Checkpoint || state == States.PlayerControl;
  }
}
