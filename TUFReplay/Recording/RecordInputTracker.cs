using System.Collections.Generic;
using TUFReplay.Recording.NativeInput;

namespace TUFReplay.Recording;

public static class RecordInputTracker
{
  private static readonly INativeInputStateReader StateReader = NativeInputStateReaderFactory.Create();
  private static readonly Dictionary<int, bool> PreviousStates = new Dictionary<int, bool>();

  private static bool _capturing;
  private static long _samples;
  private static long _transitions;
  private static long _readFailures;

  public static void StartCapture()
  {
    Reset();
    _capturing = true;
    Main.Instance.Log(
      "[Recording/InputDebug] Native state capture started. reader=" + StateReader.Name +
      ", supportedKeys=" + StateReader.KeyCodes.Count
    );
  }

  public static void StopCapture()
  {
    _capturing = false;
  }

  public static void Reset()
  {
    _capturing = false;
    PreviousStates.Clear();
    _samples = 0;
    _transitions = 0;
    _readFailures = 0;
  }

  public static void Sample(RecordingSession session)
  {
    if (!_capturing) return;
    if (session == null || !session.IsRecording || !session.IsCapturingInput) return;
    if (!TryGetSongPosition(out double songPosition)) return;

    _samples++;
    StateReader.Refresh();

    IReadOnlyList<int> keyCodes = StateReader.KeyCodes;
    for (int i = 0; i < keyCodes.Count; i++)
    {
      int key = keyCodes[i];
      if (!StateReader.TryGetIsDown(key, out bool isDown))
      {
        _readFailures++;
        continue;
      }

      PreviousStates.TryGetValue(key, out bool wasDown);
      if (isDown == wasDown) continue;

      PreviousStates[key] = isDown;

      RecordInputFlags flags = RecordInputFlags.Async;
      if (isDown) flags |= RecordInputFlags.Down;

      session.AddInputAtSongPosition(songPosition, key, flags);
      _transitions++;
    }
  }

  public static string DebugSnapshot()
  {
    return
      "capturing=" + _capturing +
      ", mode=native-state-sample" +
      ", reader=" + StateReader.Name +
      ", supportedKeys=" + StateReader.KeyCodes.Count +
      ", samples=" + _samples +
      ", transitions=" + _transitions +
      ", readFailures=" + _readFailures;
  }

  private static bool TryGetSongPosition(out double songPosition)
  {
    songPosition = 0d;
    if (ADOBase.conductor == null) return false;

    songPosition = ADOBase.conductor.songposition_minusi;
    return true;
  }
}
