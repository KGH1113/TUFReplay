using System;
using System.Diagnostics;
using System.Threading;
using TUFReplay.Composition;
using TUFReplay.Shared.Capture;
using TUFReplay.Shared.Threading;
using TUFReplay.Webcam.Diagnostics;
using UnityEngine;

namespace TUFReplay.Webcam.Playback;

internal static class CameraRenderDiagnostics
{
  private static readonly SerialBackgroundQueue Logs = new SerialBackgroundQueue();
  private static CameraRenderStatistics _statistics;

  internal static void Tick(bool setupVisible)
  {
    EnsureFrame();
    // Flush only completed frames, before this frame's preview/UI work.
    if (_statistics.WindowSeconds >= 5)
      Flush("interval");
    _statistics.SetContext(
      Main.Settings?.WebcamEnabled == true,
      Main.Settings?.WebcamLiveVisible == true,
      ADOBase.isScnGame || (scnEditor.instance != null && scnEditor.instance.playMode),
      Application.isFocused,
      setupVisible
    );
  }

  private static void EnsureFrame()
  {
    if (_statistics == null)
      _statistics = new CameraRenderStatistics(Stopwatch.Frequency);
    int frameId = Time.frameCount;
    if (_statistics.FrameId != frameId)
      _statistics.BeginFrame(frameId, Stopwatch.GetTimestamp());
  }

  internal static void Record(CameraRenderPhase phase, long elapsedTicks)
  {
    EnsureFrame();
    _statistics.Record(phase, elapsedTicks);
  }

  internal static void RecordOverlayUpdate(long elapsedTicks, bool visible, bool replay)
  {
    EnsureFrame();
    _statistics.RecordOverlayUpdate(elapsedTicks, visible, replay);
  }

  internal static void RecordUpload(int width, int height, bool resized)
  {
    EnsureFrame();
    _statistics.RecordUpload(width, height, resized);
  }

  private static void Flush(string reason)
  {
    if (_statistics.CompletedFrames == 0)
      return;
    // Snapshot immutable values here; JSON encoding and Player.log writes run
    // on a worker, outside the timed UI/copy/upload paths.
    var data = new
    {
      schemaVersion = 2,
      renderer = "ugui",
      reason,
      measuredThreadId = Thread.CurrentThread.ManagedThreadId,
      windowEndUtc = DateTime.UtcNow.ToString("O"),
      stopwatchFrequency = Stopwatch.Frequency,
      timing = "game_thread_elapsed_not_gpu",
      targetFrameRate = Application.targetFrameRate,
      vSyncCount = QualitySettings.vSyncCount,
      timeScale = Time.timeScale,
      screenWidth = Screen.width,
      screenHeight = Screen.height,
      metrics = _statistics.DiagnosticState(),
    };
    _statistics.ResetWindow();
    _ = Logs.Enqueue(() => CaptureDiagnostics.Record("render.performance", data));
  }

  internal static void Shutdown()
  {
    if (_statistics == null)
      return;
    Flush("shutdown");
    _statistics = null;
  }
}
